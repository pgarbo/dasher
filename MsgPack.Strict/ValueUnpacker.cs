using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace MsgPack.Strict
{
    public static class ValueUnpacker
    {
        #region Primitive type getters

        /*
        mTypeHash[typeof(sbyte)]=OpCodes.Ldind_I1;
        mTypeHash[typeof(byte)]=OpCodes.Ldind_U1;
        mTypeHash[typeof(char)]=OpCodes.Ldind_U2;
        mTypeHash[typeof(short)]=OpCodes.Ldind_I2;
        mTypeHash[typeof(ushort)]=OpCodes.Ldind_U2;
        mTypeHash[typeof(int)]=OpCodes.Ldind_I4;
        mTypeHash[typeof(uint)]=OpCodes.Ldind_U4;
        mTypeHash[typeof(long)]=OpCodes.Ldind_I8;
        mTypeHash[typeof(ulong)]=OpCodes.Ldind_I8;
        mTypeHash[typeof(bool)]=OpCodes.Ldind_I1;
        mTypeHash[typeof(double)]=OpCodes.Ldind_R8;
        mTypeHash[typeof(float)]=OpCodes.Ldind_R4;
        */

        private static readonly Dictionary<Type, MethodInfo> _typeGetters = new Dictionary<Type, MethodInfo>
        {
            // TODO DateTime, TimeSpan
            // TODO IReadOnlyList<T>, etc.
            {typeof(sbyte),   typeof(ValueUnpacker).GetMethod(nameof(TryReadSByte),   BindingFlags.Static | BindingFlags.Public)},
            {typeof(byte),    typeof(ValueUnpacker).GetMethod(nameof(TryReadByte),    BindingFlags.Static | BindingFlags.Public)},
            {typeof(short),   typeof(ValueUnpacker).GetMethod(nameof(TryReadShort),   BindingFlags.Static | BindingFlags.Public)},
            {typeof(ushort),  typeof(ValueUnpacker).GetMethod(nameof(TryReadUShort),  BindingFlags.Static | BindingFlags.Public)},
            {typeof(int),     typeof(ValueUnpacker).GetMethod(nameof(TryReadInt),     BindingFlags.Static | BindingFlags.Public)},
            {typeof(uint),    typeof(ValueUnpacker).GetMethod(nameof(TryReadUInt),    BindingFlags.Static | BindingFlags.Public)},
            {typeof(long),    typeof(ValueUnpacker).GetMethod(nameof(TryReadLong),    BindingFlags.Static | BindingFlags.Public)},
            {typeof(ulong),   typeof(ValueUnpacker).GetMethod(nameof(TryReadULong),   BindingFlags.Static | BindingFlags.Public)},
            {typeof(float),   typeof(ValueUnpacker).GetMethod(nameof(TryReadFloat),   BindingFlags.Static | BindingFlags.Public)},
            {typeof(double),  typeof(ValueUnpacker).GetMethod(nameof(TryReadDouble),  BindingFlags.Static | BindingFlags.Public)},
            {typeof(bool),    typeof(ValueUnpacker).GetMethod(nameof(TryReadBool),    BindingFlags.Static | BindingFlags.Public)},
            {typeof(string),  typeof(ValueUnpacker).GetMethod(nameof(TryReadString),  BindingFlags.Static | BindingFlags.Public)},
            {typeof(decimal), typeof(ValueUnpacker).GetMethod(nameof(TryReadDecimal), BindingFlags.Static | BindingFlags.Public)},
        };

        public static MethodInfo GetUnpackerMethodForType(Type type)
        {
            MethodInfo methodInfo;
            if (_typeGetters.TryGetValue(type, out methodInfo))
                return methodInfo;
            
            var complexMethod =  typeof(ValueUnpacker).GetMethod(nameof(TryReadComplex), BindingFlags.Static | BindingFlags.Public);
            return complexMethod.MakeGenericMethod(type);
        }

        public static bool TryReadSByte(Unpacker unpacker, out sbyte value) => unpacker.ReadSByte(out value);
        public static bool TryReadByte(Unpacker unpacker, out byte value) => unpacker.ReadByte(out value);
        public static bool TryReadShort(Unpacker unpacker, out short value) => unpacker.ReadInt16(out value);
        public static bool TryReadUShort(Unpacker unpacker, out ushort value) => unpacker.ReadUInt16(out value);
        public static bool TryReadInt(Unpacker unpacker, out int value) => unpacker.ReadInt32(out value);
        public static bool TryReadUInt(Unpacker unpacker, out uint value) => unpacker.ReadUInt32(out value);
        public static bool TryReadLong(Unpacker unpacker, out long value) => unpacker.ReadInt64(out value);
        public static bool TryReadULong(Unpacker unpacker, out ulong value) => unpacker.ReadUInt64(out value);
        public static bool TryReadBool(Unpacker unpacker, out bool value) => unpacker.ReadBoolean(out value);
        public static bool TryReadFloat(Unpacker unpacker, out float value) => unpacker.ReadSingle(out value);
        public static bool TryReadDouble(Unpacker unpacker, out double value) => unpacker.ReadDouble(out value);
        public static bool TryReadString(Unpacker unpacker, out string value) => unpacker.ReadString(out value);

        public static bool TryReadDecimal(Unpacker unpacker, out decimal value)
        {
            string s;
            if (!unpacker.ReadString(out s))
            {
                value = default(decimal);
                return false;
            }
            return decimal.TryParse(s, out value);
        }
        #endregion
        #region Complex types
        public static bool TryReadComplex<T>(Unpacker unpacker, out T value)
        {
            value = (T)StrictDeserialiser.Get(typeof(T)).Deserialise(unpacker);
            return true;
        }
        #endregion

        #region List types - TO REMOVE
        public static bool TryReadIReadOnlyList<T>(Unpacker unpacker, out IReadOnlyList<T> value)
        {
            List<T> val;
            var res = TryReadList<T>(unpacker, out val);
            value = val;
            return res;
        }

        public static bool TryReadList<T>(Unpacker unpacker, out List<T> value)
        {
            long arrLen = 0;

            unpacker.ReadArrayLength(out arrLen);
            value = new List<T>();
            var unpackerMethod = GetUnpackerMethodForType(typeof(T));
            for (int i = 0; i < arrLen; i++)
            {
                var parameters = new object[] { unpacker, null };
                unpackerMethod.Invoke(null, parameters);
                var readValue = (T)parameters[1];
                value.Add(readValue);
            }
            return true;
        }

        public static bool TryReadListA<T>(Unpacker unpacker, out T[] value)
        {
            long arrLen = 0;

            unpacker.ReadArrayLength(out arrLen);
            value = new T[arrLen];
            var unpackerMethod = GetUnpackerMethodForType(typeof(T));
            for (int i = 0; i < arrLen; i++)
            {
                var parameters = new object[] { unpacker, null };
                unpackerMethod.Invoke(null, parameters);
                var readValue = (T)parameters[1];
                value[i]= readValue;
            }
            return true;
        }

        public static void ListEmit()
        {
            List<int> retVal;
            Unpacker unpacker = null;
            long arrLen;
            unpacker.ReadArrayLength(out arrLen);
            var value = new List<int>();
            for (int i = 0; i < arrLen; i++)
            {
                int readValue;
                TryReadInt(unpacker, out readValue);
                value.Add(readValue);
            }
            retVal = value;
            Debug.WriteLine(retVal);
        }

        public static void ArrayEmit()
        {
            int[] retVal;
            Unpacker unpacker = null;
            long arrLen;
            unpacker.ReadArrayLength(out arrLen);
            var value = new int[arrLen];
            for (int i = 0; i < arrLen; i++)
            {
                int readValue;
                TryReadInt(unpacker, out readValue);
                value[i] = readValue;
            }
            retVal = value;
            Debug.WriteLine(retVal);
        }

        public static void ArrayEmitToList()
        {
            List<int> retVal;
            Unpacker unpacker = null;
            long arrLen;
            unpacker.ReadArrayLength(out arrLen);
            var value = new int[arrLen];
            for (int i = 0; i < arrLen; i++)
            {
                int readValue;
                TryReadInt(unpacker, out readValue);
                value[i] = readValue;
            }
            retVal = new List<int>(value);
            Debug.WriteLine(retVal);
        }
#endregion

    }
}