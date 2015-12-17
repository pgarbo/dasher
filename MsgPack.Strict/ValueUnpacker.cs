using System;
using System.Collections.Generic;
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
            // TODO IReadOnlyList<T>
            // TODO complex types
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
            {typeof(List<>), typeof(ValueUnpacker).GetMethod(nameof(TryReadList), BindingFlags.Static | BindingFlags.Public) },
            {typeof(IReadOnlyList<>), typeof(ValueUnpacker).GetMethod(nameof(TryReadIReadOnlyList), BindingFlags.Static | BindingFlags.Public) },
            {typeof(IReadOnlyCollection<>), typeof(ValueUnpacker).GetMethod(nameof(TryReadIReadOnlyCollection), BindingFlags.Static | BindingFlags.Public) },
            {typeof(ICollection<>), typeof(ValueUnpacker).GetMethod(nameof(TryReadICollection), BindingFlags.Static | BindingFlags.Public) },
            {typeof(IList<>), typeof(ValueUnpacker).GetMethod(nameof(TryReadIList), BindingFlags.Static | BindingFlags.Public) },
            {typeof(IEnumerable<>), typeof(ValueUnpacker).GetMethod(nameof(TryReadIEnumerable), BindingFlags.Static | BindingFlags.Public) },
            
        };

        public static MethodInfo GetUnpackerMethodForType(Type type)
        {
            MethodInfo methodInfo;
            if (_typeGetters.TryGetValue(type, out methodInfo))
                return methodInfo;

            if (type.IsArray)
            {
                var arrType = type.GetElementType();
                var arrayMethod = typeof(ValueUnpacker).GetMethod(nameof(TryReadArray), BindingFlags.Static | BindingFlags.Public);
                return arrayMethod.MakeGenericMethod(arrType);
            }

            var genericType = type.GetGenericType();
            if (genericType != null)
                if (_typeGetters.TryGetValue(genericType, out methodInfo))
                    return methodInfo.MakeGenericMethod(type.GenericTypeArguments[0]);

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

        #region List/array types

        public static bool TryReadIReadOnlyList<T>(Unpacker unpacker, out IReadOnlyList<T> value)
        {
            List<T> val;
            var res = TryReadList(unpacker, out val);
            value = val;
            return res;
        }

        public static bool TryReadICollection<T>(Unpacker unpacker, out ICollection<T> value)
        {
            List<T> val;
            var res = TryReadList(unpacker, out val);
            value = val;
            return res;
        }
        public static bool TryReadIEnumerable<T>(Unpacker unpacker, out IEnumerable<T> value)
        {
            List<T> val;
            var res = TryReadList(unpacker, out val);
            value = val;
            return res;
        }

        public static bool TryReadIReadOnlyCollection<T>(Unpacker unpacker, out IReadOnlyCollection<T> value)
        {
            List<T> val;
            var res = TryReadList(unpacker, out val);
            value = val;
            return res;
        }

        public static bool TryReadIList<T>(Unpacker unpacker, out IList<T> value)
        {
            List<T> val;
            var res = TryReadList(unpacker, out val);
            value = val;
            return res;
        }

        public static bool TryReadList<T>(Unpacker unpacker, out List<T> value)
        {
            value = (List<T>)StrictDeserialiser.Get(typeof(List<T>)).Deserialise(unpacker);
            return true;
        }

        public static bool TryReadArray<T>(Unpacker unpacker, out T[] value)
        {
            value = (T[])StrictDeserialiser.Get(typeof(T[])).Deserialise(unpacker);
            return true;
        }
#endregion
    }
}