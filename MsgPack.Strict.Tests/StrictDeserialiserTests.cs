using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MsgPack.Serialization;
using Xunit;

namespace MsgPack.Strict.Tests
{
    // TODO enum fields
    // TODO class/ctor private
    // TODO mismatch between ctor args and properties (?)
    // TODO test deserialising to struct (zero allocation if all properties values?)

    public sealed class StrictDeserialiserTests
    {
        #region Test Types

        public sealed class UserScore
        {
            public UserScore(string name, int score)
            {
                Name = name;
                Score = score;
            }

            public string Name { get; }
            public int Score { get; }
        }

        public sealed class UserScoreWithDefaultScore
        {
            public UserScoreWithDefaultScore(string name, int score = 100)
            {
                Name = name;
                Score = score;
            }

            public string Name { get; }
            public int Score { get; }
        }

        public sealed class UserScoreWrapper
        {
            public double Weight { get; }
            public UserScore UserScore { get; }

            public UserScoreWrapper(double weight, UserScore userScore)
            {
                Weight = weight;
                UserScore = userScore;
            }
        }

        public sealed class TestDefaultParams
        {
            public byte B { get; }
            public sbyte Sb { get; }
            public short S { get; }
            public ushort Us { get; }
            public int I { get; }
            public uint Ui { get; }
            public long L { get; }
            public ulong Ul { get; }
            public string Str { get; }
            public float F { get; }
            public double D { get; }
            public decimal Dc { get; }
            public bool Bo { get; }
            public object O { get; }

            public TestDefaultParams(
                sbyte sb = -12,
                byte b = 12,
                short s = -1234,
                ushort us = 1234,
                int i = -12345,
                uint ui = 12345,
                long l = -12345678900L,
                ulong ul = 12345678900UL,
                string str = "str",
                float f = 1.23f,
                double d = 1.23,
                decimal dc = 1.23M,
                bool bo = true,
                object o = null)
            {
                B = b;
                Sb = sb;
                S = s;
                Us = us;
                I = i;
                Ui = ui;
                L = l;
                Ul = ul;
                Str = str;
                F = f;
                D = d;
                Dc = dc;
                Bo = bo;
                O = o;
            }
        }

        public sealed class MultipleConstructors
        {
            public int Number { get; }
            public string Text { get; }

            public MultipleConstructors(int number, string text)
            {
                Number = number;
                Text = text;
            }

            public MultipleConstructors(int number)
            {
                Number = number;
            }
        }

        public sealed class NoPublicConstructors
        {
            public int Number { get; }

            internal NoPublicConstructors(int number)
            {
                Number = number;
            }
        }


        public sealed class UserScoreList
        {
            public UserScoreList(string name, IReadOnlyList<int> scores)
            {
                Name = name;
                Scores = scores;
            }

            public string Name { get; }
            public IReadOnlyList<int> Scores { get; }
        }

        public sealed class UserScoreListArray
        {
            public UserScoreListArray(string name, int[] scores)
            {
                Name = name;
                Scores = scores;
            }

            public string Name { get; }
            public int[] Scores { get; }
        }

        public sealed class UserScoreListComplex
        {
            public UserScoreListComplex(string name, List<UserScoreList> scores)
            {
                Name = name;
                Scores = scores;
            }
            public string Name { get; }
            public List<UserScoreList> Scores { get; }
        }

        public sealed class UserScoreListComplexComplex
        {
            public UserScoreListComplexComplex(string name, List<UserScoreListComplex> scores)
            {
                Name = name;
                Scores = scores;
            }
            public string Name { get; }
            public List<UserScoreListComplex> Scores { get; }
        }

        public sealed class UserScoreListOfList
        {
            public UserScoreListOfList(string name, List<List<int>> scores)
            {
                Name = name;
                Scores = scores;
            }
            public string Name { get; }
            public List<List<int>> Scores { get; }
        }

        public sealed class UserScoreArray2d
        {
            public UserScoreArray2d(string name, int[][] scores)
            {
                Name = name;
                Scores = scores;
            }
            public string Name { get; }
            public int[][] Scores { get; }
        }
        #endregion

        [Fact]
        public void ExactMatch()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Score").Pack(123));

            var after = StrictDeserialiser.Get<UserScore>().Deserialise(bytes);

            Assert.Equal("Bob", after.Name);
            Assert.Equal(123, after.Score);
        }

        [Fact]
        public void ReorderedFields()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Score").Pack(123)
                .Pack("Name").Pack("Bob"));

            var after = StrictDeserialiser.Get<UserScore>().Deserialise(bytes);

            Assert.Equal("Bob", after.Name);
            Assert.Equal(123, after.Score);
        }

        [Fact]
        public void MixedUpCapitalisation()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("NaMe").Pack("Bob")
                .Pack("ScorE").Pack(123));

            var after = StrictDeserialiser.Get<UserScore>().Deserialise(bytes);

            Assert.Equal("Bob", after.Name);
            Assert.Equal(123, after.Score);
        }

        [Fact]
        public void ThrowsOnUnexpectedField()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(3)
                .Pack("Name").Pack("Bob")
                .Pack("Score").Pack(123)
                .Pack("SUPRISE").Pack("Unexpected"));

            var deserialiser = StrictDeserialiser.Get<UserScore>();
            var ex = Assert.Throws<StrictDeserialisationException>(
                () => deserialiser.Deserialise(bytes));

            Assert.Equal(typeof(UserScore), ex.TargetType);
            Assert.Equal("Encountered unexpected field \"SUPRISE\".", ex.Message);
        }

        [Fact]
        public void ThrowsOnMissingField()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(1)
                .Pack("Name").Pack("Bob"));

            var deserialiser = StrictDeserialiser.Get<UserScore>();
            var ex = Assert.Throws<StrictDeserialisationException>(
                () => deserialiser.Deserialise(bytes));

            Assert.Equal(typeof(UserScore), ex.TargetType);
            Assert.Equal("Missing required field \"score\".", ex.Message);
        }

        [Fact]
        public void ThrowsOnIncorrectDataType()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Score").Pack(123.4)); // double, should be int

            var deserialiser = StrictDeserialiser.Get<UserScore>();
            var ex = Assert.Throws<StrictDeserialisationException>(
                () => deserialiser.Deserialise(bytes));

            Assert.Equal(typeof(UserScore), ex.TargetType);
            Assert.Equal("Unexpected type for \"Score\". Expected int, got double.", ex.Message);
        }

        [Fact]
        public void ThrowsOnDuplicateField()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(3)
                .Pack("Name").Pack("Bob")
                .Pack("Score").Pack(123)
                .Pack("Score").Pack(321));

            var deserialiser = StrictDeserialiser.Get<UserScore>();
            var ex = Assert.Throws<StrictDeserialisationException>(
                () => deserialiser.Deserialise(bytes));

            Assert.Equal(typeof(UserScore), ex.TargetType);
            Assert.Equal("Encountered duplicate field \"Score\".", ex.Message);
        }

        [Fact]
        public void ThrowsOnNonMapData()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(2)
                .Pack("Name").Pack(123));

            var deserialiser = StrictDeserialiser.Get<UserScore>();
            Assert.Throws<MessageTypeException>(() => deserialiser.Deserialise(bytes));
        }

        [Fact]
        public void ThrowsOnEmptyData()
        {
            var bytes = new byte[0];

            var deserialiser = StrictDeserialiser.Get<UserScore>();
            var ex = Assert.Throws<StrictDeserialisationException>(
                () => deserialiser.Deserialise(bytes));

            Assert.Equal(typeof(UserScore), ex.TargetType);
            Assert.Equal("Data stream ended.", ex.Message);
        }

        [Fact]
        public void UsesDefaultValuesIfNotInMessage()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(0));

            var deserialiser = StrictDeserialiser.Get<TestDefaultParams>();
            var after = deserialiser.Deserialise(bytes);

            Assert.Equal(-12, after.Sb);
            Assert.Equal(12, after.B);
            Assert.Equal(-1234, after.S);
            Assert.Equal(1234, after.Us);
            Assert.Equal(-12345, after.I);
            Assert.Equal(12345u, after.Ui);
            Assert.Equal(-12345678900L, after.L);
            Assert.Equal(12345678900UL, after.Ul);
            Assert.Equal("str", after.Str);
            Assert.Equal(1.23f, after.F);
            Assert.Equal(1.23, after.D);
            Assert.Equal(1.23M, after.Dc);
            Assert.Equal(true, after.Bo);
            Assert.Equal(null, after.O);
        }

        [Fact]
        public void SpecifiedValueOverridesDefaultValue()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Score").Pack(12345)); // score has a default of 100

            var deserialiser = StrictDeserialiser.Get<UserScoreWithDefaultScore>();
            var after = deserialiser.Deserialise(bytes);

            Assert.Equal("Bob", after.Name);
            Assert.Equal(12345, after.Score);
        }

        [Fact]
        public void ThrowsOnMultipleConstructors()
        {
            var ex = Assert.Throws<StrictDeserialisationException>(
                () => StrictDeserialiser.Get<MultipleConstructors>());
            Assert.Equal("Type must have a single public constructor.", ex.Message);
        }

        [Fact]
        public void ThrowsNoPublicConstructors()
        {
            var ex = Assert.Throws<StrictDeserialisationException>(
                () => StrictDeserialiser.Get<NoPublicConstructors>());
            Assert.Equal("Type must have a single public constructor.", ex.Message);
        }

        [Fact]
        public void HandlesNestedComplexTypes()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Weight").Pack(0.5d)
                .Pack("UserScore").PackMapHeader(2)
                    .Pack("Name").Pack("Bob")
                    .Pack("Score").Pack(123));

            var after = StrictDeserialiser.Get<UserScoreWrapper>().Deserialise(bytes);

            Assert.Equal(0.5d, after.Weight);
            Assert.Equal("Bob", after.UserScore.Name);
            Assert.Equal(123, after.UserScore.Score);
        }
        #region Lists
        [Fact]
        public void HandlesReadOnlyListProperty()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Scores").PackArrayHeader(3).Pack(1).Pack(2).Pack(3));

            var after = StrictDeserialiser.Get<UserScoreList>().Deserialise(bytes);

            Assert.Equal("Bob", after.Name);
            Assert.Equal(1, after.Scores[0]);
            Assert.Equal(2, after.Scores[1]);
            Assert.Equal(3, after.Scores[2]);
        }


        [Fact]
        public void HandlesListOfComplexObject()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Scores").PackArrayHeader(3)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob1")
                    .Pack("Scores").PackArrayHeader(0)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob2")
                    .Pack("Scores").PackArrayHeader(0)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob3")
                    .Pack("Scores").PackArrayHeader(0));

            var after = StrictDeserialiser.Get<UserScoreListComplex>().Deserialise(bytes);
            Assert.Equal("Bob", after.Name);
            Assert.Equal("Bob1", after.Scores[0].Name);
            Assert.Equal("Bob2", after.Scores[1].Name);
            Assert.Equal("Bob3", after.Scores[2].Name);
        }

        [Fact]
        public void HandlesListOfComplexObject2()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Scores").PackArrayHeader(3)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob1")
                    .Pack("Scores").PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob2")
                    .Pack("Scores").PackArrayHeader(0)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob3")
                    .Pack("Scores").PackArrayHeader(0));

            var after = StrictDeserialiser.Get<UserScoreListComplex>().Deserialise(bytes);
            Assert.Equal("Bob", after.Name);
            Assert.Equal("Bob1", after.Scores[0].Name);
            Assert.Equal(1, after.Scores[0].Scores[0]);
            Assert.Equal(2, after.Scores[0].Scores[1]);
            Assert.Equal(3, after.Scores[0].Scores[2]);
            Assert.Equal("Bob2", after.Scores[1].Name);
            Assert.Equal("Bob3", after.Scores[2].Name);
        }

        [Fact]
        public void HandlesListOfComplexObject3()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Scores").PackArrayHeader(3)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob1")
                    .Pack("Scores").PackArrayHeader(3)
                        .PackMapHeader(2)
                        .Pack("Name").Pack("Bob11")
                        .Pack("Scores").PackArrayHeader(3)
                            .Pack(1).Pack(2).Pack(3)
                        .PackMapHeader(2)
                        .Pack("Name").Pack("Bob12")
                        .Pack("Scores").PackArrayHeader(0)
                        .PackMapHeader(2)
                        .Pack("Name").Pack("Bob13")
                        .Pack("Scores").PackArrayHeader(0)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob2")
                    .Pack("Scores").PackArrayHeader(0)
                    .PackMapHeader(2)
                    .Pack("Name").Pack("Bob3")
                    .Pack("Scores").PackArrayHeader(0));

            var after = StrictDeserialiser.Get<UserScoreListComplexComplex>().Deserialise(bytes);
            Assert.Equal("Bob", after.Name);
            Assert.Equal("Bob1", after.Scores[0].Name);
            Assert.Equal("Bob11", after.Scores[0].Scores[0].Name);
            Assert.Equal(1, after.Scores[0].Scores[0].Scores[0]);
            Assert.Equal(2, after.Scores[0].Scores[0].Scores[1]);
            Assert.Equal(3, after.Scores[0].Scores[0].Scores[2]);
            Assert.Equal("Bob12", after.Scores[0].Scores[1].Name);
            Assert.Equal("Bob13", after.Scores[0].Scores[2].Name);
            Assert.Equal("Bob2", after.Scores[1].Name);
            Assert.Equal("Bob3", after.Scores[2].Name);
        }

        [Fact]
        public void HandlesSingleList()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1).Pack(2).Pack(3));
            var after = StrictDeserialiser.Get<List<int>>().Deserialise(bytes);
            Assert.Equal(1, after[0]);
            Assert.Equal(2, after[1]);
            Assert.Equal(3, after[2]);
        }

        [Fact]
        public void HandlesListOfListProperty()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Scores").PackArrayHeader(3)
                    .PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackArrayHeader(3)
                        .Pack(11).Pack(12).Pack(13)
                    .PackArrayHeader(3)
                        .Pack(21).Pack(22).Pack(23)
            );

            var after = StrictDeserialiser.Get<UserScoreListOfList>().Deserialise(bytes);

            Assert.Equal("Bob", after.Name);

            Assert.Equal(1, after.Scores[0][0]);
            Assert.Equal(2, after.Scores[0][1]);
            Assert.Equal(3, after.Scores[0][2]);
            Assert.Equal(11, after.Scores[1][0]);
            Assert.Equal(12, after.Scores[1][1]);
            Assert.Equal(13, after.Scores[1][2]);
            Assert.Equal(21, after.Scores[2][0]);
            Assert.Equal(22, after.Scores[2][1]);
            Assert.Equal(23, after.Scores[2][2]);
        }

        [Fact]
        public void HandlesSingleListOfList()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3)
                    .PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackArrayHeader(3)
                        .Pack(11).Pack(12).Pack(13)
                    .PackArrayHeader(3)
                        .Pack(21).Pack(22).Pack(23)
            );

            var after = StrictDeserialiser.Get<List<List<int>>>().Deserialise(bytes);

            Assert.Equal(1, after[0][0]);
            Assert.Equal(2, after[0][1]);
            Assert.Equal(3, after[0][2]);
            Assert.Equal(11, after[1][0]);
            Assert.Equal(12, after[1][1]);
            Assert.Equal(13, after[1][2]);
            Assert.Equal(21, after[2][0]);
            Assert.Equal(22, after[2][1]);
            Assert.Equal(23, after[2][2]);
        }

        [Fact]
        public void HandlesListOfArray()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3)
                    .PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackArrayHeader(3)
                        .Pack(11).Pack(12).Pack(13)
                    .PackArrayHeader(3)
                        .Pack(21).Pack(22).Pack(23)
            );

            var after = StrictDeserialiser.Get<List<int[]>>().Deserialise(bytes);

            Assert.Equal(1, after[0][0]);
            Assert.Equal(2, after[0][1]);
            Assert.Equal(3, after[0][2]);
            Assert.Equal(11, after[1][0]);
            Assert.Equal(12, after[1][1]);
            Assert.Equal(13, after[1][2]);
            Assert.Equal(21, after[2][0]);
            Assert.Equal(22, after[2][1]);
            Assert.Equal(23, after[2][2]);
        }


        #endregion
        #region Arrays
        [Fact]
        public void HandlesArrayOfInt()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1).Pack(2).Pack(3));
            var after = StrictDeserialiser.Get<int[]>().Deserialise(bytes);
            Assert.Equal(1, after[0]);
            Assert.Equal(2, after[1]);
            Assert.Equal(3, after[2]);
        }

        [Fact]
        public void HandlesArrayOfString()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack("a").Pack("b").Pack("c"));
            var after = StrictDeserialiser.Get<string[]>().Deserialise(bytes);
            Assert.Equal(3, after.Length);
            Assert.Equal("a", after[0]);
            Assert.Equal("b", after[1]);
            Assert.Equal("c", after[2]);
        }

        [Fact]
        public void HandlesArrayOfDouble()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1.1d).Pack(1.2d).Pack(1.3d));
            var after = StrictDeserialiser.Get<double[]>().Deserialise(bytes);
            Assert.Equal(3, after.Length);
            Assert.Equal(1.1d, after[0]);
            Assert.Equal(1.2d, after[1]);
            Assert.Equal(1.3d, after[2]);
        }

        [Fact]
        public void HandlesArrayOfList()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3)
                    .PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackArrayHeader(3)
                        .Pack(11).Pack(12).Pack(13)
                    .PackArrayHeader(3)
                        .Pack(21).Pack(22).Pack(23)
            );

            var after = StrictDeserialiser.Get<List<int>[]>().Deserialise(bytes);

            Assert.Equal(1, after[0][0]);
            Assert.Equal(2, after[0][1]);
            Assert.Equal(3, after[0][2]);
            Assert.Equal(11, after[1][0]);
            Assert.Equal(12, after[1][1]);
            Assert.Equal(13, after[1][2]);
            Assert.Equal(21, after[2][0]);
            Assert.Equal(22, after[2][1]);
            Assert.Equal(23, after[2][2]);
        }

        [Fact]
        public void HandlesArrayOfLong()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1L).Pack(2L).Pack(3L));
            var after = StrictDeserialiser.Get<long[]>().Deserialise(bytes);
            Assert.Equal(3, after.Length);
            Assert.Equal(1L, after[0]);
            Assert.Equal(2L, after[1]);
            Assert.Equal(3L, after[2]);
        }

        [Fact]
        public void HandlesArrayOfComplexType()
        {
            var bytes = TestUtil.PackBytes(packer => packer
            .PackArrayHeader(3)
                .PackMapHeader(2)
                    .Pack("Name").Pack("Bob")
                    .Pack("Score").Pack(123)
                .PackMapHeader(2)
                    .Pack("Name").Pack("John")
                    .Pack("Score").Pack(234)
                .PackMapHeader(2)
                    .Pack("Name").Pack("Sam")
                    .Pack("Score").Pack(345)
            );
            var after = StrictDeserialiser.Get<UserScore[]>().Deserialise(bytes);
            Assert.Equal(3, after.Length);
            Assert.Equal("Bob", after[0].Name);
            Assert.Equal(123, after[0].Score);
            Assert.Equal("John", after[1].Name);
            Assert.Equal(234, after[1].Score);
            Assert.Equal("Sam", after[2].Name);
            Assert.Equal(345, after[2].Score);
        }

        [Fact]
        public void HandlesArrayAsParam()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Scores").PackArrayHeader(3).Pack(1).Pack(2).Pack(3));

            var after = StrictDeserialiser.Get<UserScoreListArray>().Deserialise(bytes);

            Assert.Equal("Bob", after.Name);
            Assert.Equal(1, after.Scores[0]);
            Assert.Equal(2, after.Scores[1]);
            Assert.Equal(3, after.Scores[2]);
        }

        [Fact]
        public void HandlesJagged2DArrayAsParam()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackMapHeader(2)
                .Pack("Name").Pack("Bob")
                .Pack("Scores").PackArrayHeader(3)
                    .PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackArrayHeader(2)
                        .Pack(11).Pack(12)
                    .PackArrayHeader(1)
                        .Pack(21)
            );

            var after = StrictDeserialiser.Get<UserScoreArray2d>().Deserialise(bytes);

            Assert.Equal("Bob", after.Name);

            Assert.Equal(1, after.Scores[0][0]);
            Assert.Equal(2, after.Scores[0][1]);
            Assert.Equal(3, after.Scores[0][2]);
            Assert.Equal(11, after.Scores[1][0]);
            Assert.Equal(12, after.Scores[1][1]);
            Assert.Equal(21, after.Scores[2][0]);
        }

        [Fact]
        public void HandlesJagged2DArray()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3)
                    .PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackArrayHeader(2)
                        .Pack(11).Pack(12)
                    .PackArrayHeader(1)
                        .Pack(21)
            );

            var after = StrictDeserialiser.Get<int[][]>().Deserialise(bytes);

            Assert.Equal(1, after[0][0]);
            Assert.Equal(2, after[0][1]);
            Assert.Equal(3, after[0][2]);
            Assert.Equal(11, after[1][0]);
            Assert.Equal(12, after[1][1]);
            Assert.Equal(21, after[2][0]);
        }

        [Fact]
        public void Handles2DArray()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3)
                    .PackArrayHeader(3)
                        .Pack(1).Pack(2).Pack(3)
                    .PackArrayHeader(3)
                        .Pack(11).Pack(12).Pack(13)
                    .PackArrayHeader(3)
                        .Pack(21).Pack(22).Pack(23)
            );

            var after = StrictDeserialiser.Get<int[,]>().Deserialise(bytes);

            Assert.Equal(1, after[0, 0]);
            Assert.Equal(2, after[0, 1]);
            Assert.Equal(3, after[0, 2]);
            Assert.Equal(11, after[1, 0]);
            Assert.Equal(12, after[1, 1]);
            Assert.Equal(13, after[1, 2]);
            Assert.Equal(21, after[2, 0]);
            Assert.Equal(22, after[2, 1]);
            Assert.Equal(23, after[2, 2]);
        }

        #endregion
        #region collection interfaces

        [Fact]
        public void HandlesIReadonlyList()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1).Pack(2).Pack(3));
            var after = StrictDeserialiser.Get<IReadOnlyList<int>>().Deserialise(bytes);
            Assert.Equal(1, after[0]);
            Assert.Equal(2, after[1]);
            Assert.Equal(3, after[2]);
        }

        [Fact]
        public void HandlesIList()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1).Pack(2).Pack(3));
            var after = StrictDeserialiser.Get<IList<int>>().Deserialise(bytes);
            Assert.Equal(1, after[0]);
            Assert.Equal(2, after[1]);
            Assert.Equal(3, after[2]);
        }

        [Fact]
        public void HandlesIReadonlyCollection()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1).Pack(2).Pack(3));
            var after = StrictDeserialiser.Get<IReadOnlyCollection<int>>().Deserialise(bytes);
            var enumerator = after.GetEnumerator();
            enumerator.MoveNext();
            Assert.Equal(1, enumerator.Current);
            enumerator.MoveNext();
            Assert.Equal(2, enumerator.Current);
            enumerator.MoveNext();
            Assert.Equal(3, enumerator.Current);
        }

        [Fact]
        public void HandlesIEnumerable()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1).Pack(2).Pack(3));
            var after = StrictDeserialiser.Get<IEnumerable<int>>().Deserialise(bytes);
            var enumerator = after.GetEnumerator();
            enumerator.MoveNext();
            Assert.Equal(1, enumerator.Current);
            enumerator.MoveNext();
            Assert.Equal(2, enumerator.Current);
            enumerator.MoveNext();
            Assert.Equal(3, enumerator.Current);
        }

        [Fact]
        public void HandlesICollection()
        {
            var bytes = TestUtil.PackBytes(packer => packer.PackArrayHeader(3).Pack(1).Pack(2).Pack(3));
            var after = StrictDeserialiser.Get<ICollection<int>>().Deserialise(bytes);

            var enumerator = after.GetEnumerator();
            enumerator.MoveNext();
            Assert.Equal(1, enumerator.Current);
            enumerator.MoveNext();
            Assert.Equal(2, enumerator.Current);
            enumerator.MoveNext();
            Assert.Equal(3, enumerator.Current);
        }
        #endregion

        #region Performance tests

        //[Fact]
        public void PerformanceOfListAndArrayCreation()
        {
            const int million = 1000000;
            const int capacity = 10 * million;
            var stream = new MemoryStream();
            var packer = Packer.Create(stream);
            packer.PackArrayHeader(capacity);
            for (int i = 0; i < capacity; i++)
            {
                packer.Pack(i);
            }
            stream.Position = 0;
            var bytes = stream.GetBuffer();

            Stopwatch sw = new Stopwatch();

            {
                sw.Restart();
                var receivedStream = new MemoryStream(bytes);
                var unpacker = Unpacker.Create(receivedStream);
                long arrLen;
                unpacker.ReadArrayLength(out arrLen);
                var arr = new int[capacity];
                for (int i = 0; i < capacity; i++)
                {
                    int val;
                    unpacker.ReadInt32(out val);
                    arr[i] = val;
                }
                sw.Stop();
                long x = 0;
                for (int i = 0; i < capacity; i++)
                    x += arr[i];
                Debug.WriteLine("array " + sw.ElapsedMilliseconds);
            }

            {
                sw.Restart();
                var receivedStream = new MemoryStream(bytes);
                var unpacker = Unpacker.Create(receivedStream);
                long arrLen;
                unpacker.ReadArrayLength(out arrLen);
                var arr2 = new int[capacity];
                for (int i = 0; i < capacity; i++)
                {
                    int val;
                    unpacker.ReadInt32(out val);
                    arr2[i] = val;
                }
                var list = new List<int>(arr2);
                sw.Stop();
                long x = 0;
                for (int i = 0; i < capacity; i++)
                    x += list[i];
                Debug.WriteLine("List from array " + sw.ElapsedMilliseconds);
            }

            {
                sw.Restart();
                var receivedStream = new MemoryStream(bytes);
                var unpacker = Unpacker.Create(receivedStream);
                long arrLen;
                unpacker.ReadArrayLength(out arrLen);
                var list = new List<int>();
                for (int i = 0; i < capacity; i++)
                {
                    int val;
                    unpacker.ReadInt32(out val);
                    list.Add(val);
                }
                sw.Stop();
                long x = 0;
                for (int i = 0; i < capacity; i++)
                    x += list[i];
                Debug.WriteLine("List " + sw.ElapsedMilliseconds);
            }

            {
                sw.Restart();
                var receivedStream = new MemoryStream(bytes);
                var unpacker = Unpacker.Create(receivedStream);
                long arrLen;
                unpacker.ReadArrayLength(out arrLen);
                var list = new List<int>();
                for (int i = 0; i < capacity; i++)
                {
                    int val;
                    unpacker.ReadInt32(out val);
                    list.Add(val);
                }
                var arr = list.ToArray();
                sw.Stop();
                long x = 0;
                for (int i = 0; i < capacity; i++)
                    x += arr[i];
                Debug.WriteLine("array from List " + sw.ElapsedMilliseconds);

            }

            {
                sw.Restart();
                var afterArr = StrictDeserialiser.Get<int[]>().Deserialise(bytes);
                sw.Stop();
                Debug.WriteLine("Deserializer array " + sw.ElapsedMilliseconds);
            }

            {
                sw.Restart();
                var afterList = StrictDeserialiser.Get<List<int>>().Deserialise(bytes);
                sw.Stop();
                Debug.WriteLine("StrictDeserializer List " + sw.ElapsedMilliseconds);
            }

            {
                var sendSerializer = MessagePackSerializer.Get<List<int>>();
                var receiveSerializer = MessagePackSerializer.Get<List<int>>();
                var value = new List<int>();
                for (int i = 0; i < capacity; i++)
                {
                    value.Add(i);
                }
                var bytes1 = sendSerializer.PackSingleObject(value);
                sw.Restart();
                var after = receiveSerializer.UnpackSingleObject(bytes1);
                sw.Stop();
                Debug.WriteLine("MSG Pack Deserializer List " + sw.ElapsedMilliseconds);
            }
        }

        #endregion
    }
}
