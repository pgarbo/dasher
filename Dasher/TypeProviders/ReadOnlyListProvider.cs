using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Dasher.TypeProviders
{
    internal sealed class ReadOnlyListProvider : ITypeProvider
    {
        public bool CanProvide(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

        public void Serialise(ILGenerator ilg, LocalBuilder value, LocalBuilder packer, DasherContext context)
        {
            var type = value.LocalType;
            var elementType = type.GetGenericArguments().Single();

            // read list length
            var count = ilg.DeclareLocal(typeof(int));
            ilg.Emit(OpCodes.Ldloc, value);
            ilg.Emit(OpCodes.Callvirt, typeof(IReadOnlyCollection<>).MakeGenericType(elementType).GetProperty(nameof(IReadOnlyList<int>.Count)).GetMethod);
            ilg.Emit(OpCodes.Stloc, count);

            // write array header
            ilg.Emit(OpCodes.Ldloc, packer);
            ilg.Emit(OpCodes.Ldloc, count);
            ilg.Emit(OpCodes.Call, typeof(UnsafePacker).GetMethod(nameof(UnsafePacker.PackArrayHeader)));

            // begin loop
            var loopStart = ilg.DefineLabel();
            var loopTest = ilg.DefineLabel();
            var loopEnd = ilg.DefineLabel();

            var i = ilg.DeclareLocal(typeof(int));
            ilg.Emit(OpCodes.Ldc_I4_0);
            ilg.Emit(OpCodes.Stloc, i);

            ilg.Emit(OpCodes.Br, loopTest);
            ilg.MarkLabel(loopStart);

            // loop body
            ilg.Emit(OpCodes.Ldloc, value);
            ilg.Emit(OpCodes.Ldloc, i);
            ilg.Emit(OpCodes.Callvirt, type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Single(p => p.Name == "Item" && p.GetIndexParameters().Length == 1).GetMethod);
            var elementValue = ilg.DeclareLocal(elementType);
            ilg.Emit(OpCodes.Stloc, elementValue);

            ITypeProvider provider;
            if (!context.TryGetTypeProvider(elementValue.LocalType, out provider))
                throw new Exception($"Cannot serialise IReadOnlyList<> element type {value.LocalType}.");

            provider.Serialise(ilg, elementValue, packer, context);

            // loop counter increment
            ilg.Emit(OpCodes.Ldloc, i);
            ilg.Emit(OpCodes.Ldc_I4_1);
            ilg.Emit(OpCodes.Add);
            ilg.Emit(OpCodes.Stloc, i);

            // loop test
            ilg.MarkLabel(loopTest);
            ilg.Emit(OpCodes.Ldloc, i);
            ilg.Emit(OpCodes.Ldloc, count);
            ilg.Emit(OpCodes.Clt);
            ilg.Emit(OpCodes.Brtrue, loopStart);

            // after loop
            ilg.MarkLabel(loopEnd);
        }

        public void Deserialise(ILGenerator ilg, string name, Type targetType, LocalBuilder value, LocalBuilder unpacker, LocalBuilder contextLocal, DasherContext context, UnexpectedFieldBehaviour unexpectedFieldBehaviour)
        {
            var elementType = value.LocalType.GetGenericArguments().Single();

            ITypeProvider elementProvider;
            if (!context.TryGetTypeProvider(elementType, out elementProvider))
                throw new Exception($"Unable to deserialise values of type {elementType} from MsgPack data.");

            // read list length
            var count = ilg.DeclareLocal(typeof(int));
            ilg.Emit(OpCodes.Ldloc, unpacker);
            ilg.Emit(OpCodes.Ldloca, count);
            ilg.Emit(OpCodes.Call, typeof(Unpacker).GetMethod(nameof(Unpacker.TryReadArrayLength)));

            // verify read correctly
            var lbl1 = ilg.DefineLabel();
            ilg.Emit(OpCodes.Brtrue, lbl1);
            {
                ilg.Emit(OpCodes.Ldstr, "Expecting collection data to be encoded as array");
                ilg.LoadType(targetType);
                ilg.Emit(OpCodes.Newobj, typeof(DeserialisationException).GetConstructor(new[] { typeof(string), typeof(Type) }));
                ilg.Emit(OpCodes.Throw);
            }
            ilg.MarkLabel(lbl1);

            // create an array to store values
            ilg.Emit(OpCodes.Ldloc, count);
            ilg.Emit(OpCodes.Newarr, elementType);

            var array = ilg.DeclareLocal(elementType.MakeArrayType());
            ilg.Emit(OpCodes.Stloc, array);

            // begin loop
            var loopStart = ilg.DefineLabel();
            var loopTest = ilg.DefineLabel();
            var loopEnd = ilg.DefineLabel();

            var i = ilg.DeclareLocal(typeof(int));
            ilg.Emit(OpCodes.Ldc_I4_0);
            ilg.Emit(OpCodes.Stloc, i);

            ilg.Emit(OpCodes.Br, loopTest);
            ilg.MarkLabel(loopStart);

            // loop body
            var element = ilg.DeclareLocal(elementType);

            elementProvider.Deserialise(ilg, name, targetType, element, unpacker, contextLocal, context, unexpectedFieldBehaviour);

            ilg.Emit(OpCodes.Ldloc, array);
            ilg.Emit(OpCodes.Ldloc, i);
            ilg.Emit(OpCodes.Ldloc, element);
            ilg.Emit(OpCodes.Stelem, elementType);

            // loop counter increment
            ilg.Emit(OpCodes.Ldloc, i);
            ilg.Emit(OpCodes.Ldc_I4_1);
            ilg.Emit(OpCodes.Add);
            ilg.Emit(OpCodes.Stloc, i);

            // loop test
            ilg.MarkLabel(loopTest);
            ilg.Emit(OpCodes.Ldloc, i);
            ilg.Emit(OpCodes.Ldloc, count);
            ilg.Emit(OpCodes.Clt);
            ilg.Emit(OpCodes.Brtrue, loopStart);

            // after loop
            ilg.MarkLabel(loopEnd);

            ilg.Emit(OpCodes.Ldloc, array);
            ilg.Emit(OpCodes.Stloc, value);
        }
    }
}