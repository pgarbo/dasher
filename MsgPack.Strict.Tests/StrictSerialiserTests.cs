﻿using System.Collections.Generic;
using System.IO;
using Xunit;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace MsgPack.Strict.Tests
{
    public sealed class StrictSerialiserTests
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

        public struct UserScoreStruct
        {
            public UserScoreStruct(string name, int score)
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

        public sealed class UserScoreDecimal
        {
            public UserScoreDecimal(string name, decimal score)
            {
                Name = name;
                Score = score;
            }

            public string Name { get; }
            public decimal Score { get; }
        }

        public enum TestEnum
        {
            Foo = 1,
            Bar = 2
        }

        public sealed class WithEnumProperty
        {
            public WithEnumProperty(TestEnum testEnum)
            {
                TestEnum = testEnum;
            }

            public TestEnum TestEnum { get; }
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

        public sealed class ListOfList
        {
            public IReadOnlyList<IReadOnlyList<int>> Jagged { get; }

            public ListOfList(IReadOnlyList<IReadOnlyList<int>> jagged)
            {
                Jagged = jagged;
            }
        }

        #endregion

        [Fact]
        public void SerialisesProperties()
        {
            var after = RoundTrip(new UserScore("Bob", 123));

            Assert.Equal("Bob", after.Name);
            Assert.Equal(123, after.Score);
        }

        [Fact]
        public void SerialisesStruct()
        {
            var after = RoundTrip(new UserScoreStruct("Bob", 123));

            Assert.Equal("Bob", after.Name);
            Assert.Equal(123, after.Score);
        }

        [Fact]
        public void HandlesDecimal()
        {
            var after = RoundTrip(new UserScoreDecimal("Bob", 123.456m));

            Assert.Equal("Bob", after.Name);
            Assert.Equal(123.456m, after.Score);
        }

        [Fact]
        public void HandlesComplex()
        {
            var after = RoundTrip(new UserScoreWrapper(1.0, new UserScore("Bob", 123)));

            Assert.Equal(1.0, after.Weight);
            Assert.Equal("Bob", after.UserScore.Name);
            Assert.Equal(123, after.UserScore.Score);
        }

        [Fact]
        public void HandlesList()
        {
            var after = RoundTrip(new UserScoreList("Bob", new[] {1, 2, 3, 4}));

            Assert.Equal("Bob", after.Name);
            Assert.Equal(new[] {1, 2, 3, 4}, after.Scores);
        }

        [Fact]
        public void HandlesListOfList()
        {
            var after = RoundTrip(new ListOfList(new [] {new [] {1, 2, 3}, new [] {4, 5, 6}}));

            Assert.Equal(2, after.Jagged.Count);
            Assert.Equal(new[] {1, 2, 3}, after.Jagged[0]);
            Assert.Equal(new[] {4, 5, 6}, after.Jagged[1]);
        }

        [Fact]
        public void HandlesEnum()
        {
            var after = RoundTrip(new WithEnumProperty(TestEnum.Bar));

            Assert.Equal(TestEnum.Bar, after.TestEnum);
        }

        #region Test helpers

        private static T RoundTrip<T>(T before)
        {
            var stream = new MemoryStream();

            StrictSerialiser.Get<T>().Serialise(stream, before);

            stream.Position = 0;

            return StrictDeserialiser.Get<T>().Deserialise(stream.ToArray());
        }

        #endregion
    }
}