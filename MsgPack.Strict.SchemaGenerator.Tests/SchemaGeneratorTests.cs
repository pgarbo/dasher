﻿using System;
using Xunit;
using MsgPack.Strict.SchemaGenerator;

namespace MsgPack.Strict.SchemaGenerator.Tests
{
    #region test classes
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
    public enum TestEnum
    {
        Foo = 1,
        Bar = 2
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
        public TestEnum E { get; }
        public UserScore Complex { get; }

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
            TestEnum e = TestEnum.Bar,
            UserScore complex = null,
            bool bo = true)
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
            E = e;
            Complex = complex;
        }
    }
    #endregion

    class SchemaGeneratorTests
    {
        [Fact]
        public void GenerateSchemaForSimpleType()
        {
            var expected = String.Join(
                Environment.NewLine,
                "UserScore",
                "{",
                "    name: System.String",
                "    score: System.Int32",
                "}");
            var actual = SchemaGenerator.GenerateSchema(typeof(UserScore));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GenerateSchemaForSimpleTypeWithDefaults()
        {

            var expected = String.Join(
                Environment.NewLine,
                "UserScoreWithDefaultScore",
                "{",
                "    name: System.String",
                "    score: System.Int32 = 100",
                "}");
            var actual = SchemaGenerator.GenerateSchema(typeof(UserScoreWithDefaultScore));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GenerateSchemaForTypeContainingComplexType()
        {
            var expected = @"TestDefaultParams
{
    sb: System.SByte = -12
    b: System.Byte = 12
    s: System.Int16 = -1234
    us: System.UInt16 = 1234
    i: System.Int32 = -12345
    ui: System.UInt32 = 12345
    l: System.Int64 = -12345678900
    ul: System.UInt64 = 12345678900
    str: System.String = str
    f: System.Single = 1.23
    d: System.Double = 1.23
    dc: System.Decimal = 1.23
    e: MsgPack.Strict.Tests.StrictDeserialiserTests+TestEnum = Bar
    complex: UserScore
    {
        name: System.String
        score: System.Int32
    }
    bo: System.Boolean = True
}";
            var actual = SchemaGenerator.GenerateSchema(typeof(TestDefaultParams));

            Assert.Equal(expected, actual);
        }
    }
}
