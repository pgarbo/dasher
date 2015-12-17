using System;
using System.Collections.Generic;
using System.Reflection;

namespace MsgPack.Strict
{
    public static class TypeExtensions
    {
        public static bool IsCollectionOrArray(this Type type)
        {
            return type.IsSupportedGenericCollection() || type.IsArray();
        }

        public static bool IsSupportedGenericCollection(this Type type)
        {
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>) || _concreteTypeMaps.ContainsKey(genericType))
                    return true;
            }
            return false;
        }

        public static bool IsList(this Type type)
        {
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                    return true;
            }
            return false;
        }

        public static bool IsArray(this Type type)
        {
            if (type.IsArray)
                return true;
            return false;
        }

        //TODO complete and move to utils
        public static ConstructorInfo GetDeserializationConstructor(this Type type)
        {
            type = type.GetConcreteType();
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance);
            if (ctors.Length == 0)
                throw new StrictDeserialisationException("This type does not have public constructor.", type);
            //TODO Handle constructor for System types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return ctors[2]; //TODO better way
            }
            if (ctors.Length > 1)
                throw new StrictDeserialisationException("Type must have a single public constructor.", type);
            var ctor = ctors[0];
            return ctor;
        }

        //TODO move to utils
        private static Dictionary<Type, Type> _concreteTypeMaps = new Dictionary<Type, Type>()
        {
            { typeof(IReadOnlyCollection<>), typeof(List<>) },
            { typeof(IList<>), typeof(List<>) },
            { typeof(IEnumerable<>), typeof(List<>) },
            { typeof(ICollection<>), typeof(List<>) },
            { typeof(IReadOnlyList<>), typeof(List<>) },
        };
        //TODO move to utils
        public static Type GetConcreteType(this Type type)
        {
            if (!type.IsAbstract)
                return type;
            Type concreteType;
            _concreteTypeMaps.TryGetValue(type.GetGenericTypeDefinition(), out concreteType);
            return concreteType;
        }

        public static Type GetGenericType(this Type type)
        {
            if (type.IsGenericType)
                return type.GetGenericTypeDefinition();
            return null;
        }
    }
}