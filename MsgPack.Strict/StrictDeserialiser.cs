﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace MsgPack.Strict
{
    public sealed class StrictDeserialiser<T>
    {
        private readonly StrictDeserialiser _inner;

        internal StrictDeserialiser(StrictDeserialiser inner)
        {
            _inner = inner;
        }

        public T Deserialise(byte[] bytes) => (T)_inner.Deserialise(bytes);
    }

    public sealed class StrictDeserialiser
    {
        #region Instance accessors

        private static readonly ConcurrentDictionary<Type, StrictDeserialiser> _deserialiserByType = new ConcurrentDictionary<Type, StrictDeserialiser>();

        public static StrictDeserialiser<T> Get<T>()
        {
            return new StrictDeserialiser<T>(Get(typeof(T)));
        }

        public static StrictDeserialiser Get(Type type)
        {
            StrictDeserialiser deserialiser;
            if (_deserialiserByType.TryGetValue(type, out deserialiser))
                return deserialiser;

            _deserialiserByType.TryAdd(type, new StrictDeserialiser(type));
            var present = _deserialiserByType.TryGetValue(type, out deserialiser);
            Debug.Assert(present);
            return deserialiser;
        }

        #endregion

        private readonly Func<Unpacker, object> _func;

        private StrictDeserialiser(Type type)
        {
            _func = type.IsCollectionOrArray() ? BuildCollectionUnpacker(type) : BuildUnpacker(type);
        }

        public object Deserialise(byte[] bytes)
        {
            return Deserialise(Unpacker.Create(new MemoryStream(bytes)));
        }

        public object Deserialise(Unpacker unpacker)
        {
            return _func(unpacker);
        }

        private static Func<Unpacker, object> BuildUnpacker(Type type)
        {
            #region Verify and prepare for target type

            if (type.IsPrimitive)
                throw new Exception("TEST THIS CASE 1");

            var ctor = type.GetDeserializationConstructor();

            var parameters = ctor.GetParameters();

            #endregion

            #region Initialise code gen

            var method = new DynamicMethod(
                $"Deserialiser{type.Name}",
                typeof(object),
                new[] { typeof(Unpacker) });

            var ilg = method.GetILGenerator();

            #endregion

            #region Initialise locals

            var valueLocals = new LocalBuilder[parameters.Length];
            var valueSetLocals = new LocalBuilder[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                valueLocals[i] = ilg.DeclareLocal(parameter.ParameterType);
                valueSetLocals[i] = ilg.DeclareLocal(typeof(int));

                if (parameter.HasDefaultValue)
                {
                    // set default values on params
                    StoreValue(ilg, parameter.DefaultValue);
                    ilg.Emit(OpCodes.Stloc, valueLocals[i]);
                    // set 'valueSet' to true
                    // note we use the second LSb to indicate a default value
                    ilg.Emit(OpCodes.Ldc_I4_2);
                    ilg.Emit(OpCodes.Stloc, valueSetLocals[i]);
                }
                else
                {
                    // set 'valueSet' to false
                    ilg.Emit(OpCodes.Ldc_I4_0);
                    ilg.Emit(OpCodes.Stloc, valueSetLocals[i]);
                }
            }

            #endregion

            Action throwException = () =>
            {
                ilg.Emit(OpCodes.Ldtoken, type);
                ilg.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
                ilg.Emit(OpCodes.Newobj, typeof(StrictDeserialisationException).GetConstructor(new[] { typeof(string), typeof(Type) }));
                ilg.Emit(OpCodes.Throw);
            };

            #region Read map length

            var mapSize = ilg.DeclareLocal(typeof(long));
            {
                // MsgPack messages may be single values, arrays, maps, or any arbitrary
                // combination of these types. Our convention is to require messages to
                // be encoded as maps where the key is the property name.
                //
                // MsgPack maps begin with a header indicating the number of pairs
                // within the map. We read this here.
                ilg.Emit(OpCodes.Ldarg_0); // unpacker
                ilg.Emit(OpCodes.Ldloca, mapSize);
                ilg.Emit(OpCodes.Callvirt, typeof(Unpacker).GetMethod("ReadMapLength"));

                // If false was returned, the data stream ended
                var ifLabel = ilg.DefineLabel();
                ilg.Emit(OpCodes.Brtrue, ifLabel);
                {
                    ilg.Emit(OpCodes.Ldstr, "Data stream ended.");
                    throwException();
                }
                ilg.MarkLabel(ifLabel);
            }

            #endregion

            // For each key/value pair in the map...
            {
                // Create a loop counter, initialised to zero
                var loopIndex = ilg.DeclareLocal(typeof(long));
                ilg.Emit(OpCodes.Ldc_I4_0);
                ilg.Emit(OpCodes.Conv_I8);
                ilg.Emit(OpCodes.Stloc, loopIndex);

                // Create labels to jump to within the loop
                var lblLoopTest = ilg.DefineLabel();   // Comparing counter to map size
                var lblLoopExit = ilg.DefineLabel();   // The first instruction after the loop
                var lblLoopStart = ilg.DefineLabel();  // The first instruction within the loop

                // Run the test first
                ilg.Emit(OpCodes.Br, lblLoopTest);

                // Mark the first instruction within the loop
                ilg.MarkLabel(lblLoopStart);

                // Although MsgPack allows map keys to be of any arbitrary type, our convention
                // is to require keys to be strings. We read the key here.
                var key = ilg.DeclareLocal(typeof(string));
                {
                    ilg.Emit(OpCodes.Ldarg_0); // unpacker
                    ilg.Emit(OpCodes.Ldloca, key);
                    ilg.Emit(OpCodes.Callvirt, typeof(Unpacker).GetMethod("ReadString"));

                    // If false was returned, the data stream ended
                    var ifLabel = ilg.DefineLabel();
                    ilg.Emit(OpCodes.Brtrue, ifLabel);
                    {
                        ilg.Emit(OpCodes.Ldstr, "Data stream ended.");
                        throwException();
                    }
                    ilg.MarkLabel(ifLabel);
                }

                // Build a chain of if/elseif/elseif... blocks for each of the expected fields.
                // It could be slightly more efficient here to generate a O(log(N)) tree-based lookup,
                // but that would take quite some engineering. Let's see if there's a significant perf
                // hit here or not first.
                var lblEndIfChain = ilg.DefineLabel();
                Label? nextLabel = null;
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    // Mark the beginning of the next block, as used if the previous block's condition failed
                    if (nextLabel != null)
                        ilg.MarkLabel(nextLabel.Value);
                    nextLabel = ilg.DefineLabel();

                    // Compare map's key with this parameter's name in a case insensitive way
                    ilg.Emit(OpCodes.Ldloc, key);
                    ilg.Emit(OpCodes.Ldstr, parameters[parameterIndex].Name);
                    ilg.Emit(OpCodes.Ldc_I4_5);
                    ilg.Emit(OpCodes.Callvirt, typeof(string).GetMethod("Equals", new[] { typeof(string), typeof(StringComparison) }));

                    // If the key doesn't match this property, go to the next block
                    ilg.Emit(OpCodes.Brfalse, nextLabel.Value);

                    // Verify we haven't already seen a value for this parameter
                    {
                        // Mask out the LSb and see if it is set. If so, we've seen this property
                        // already in this message, which is invalid.
                        ilg.Emit(OpCodes.Ldloc, valueSetLocals[parameterIndex]);
                        ilg.Emit(OpCodes.Ldc_I4_1);
                        ilg.Emit(OpCodes.And);
                        var notSeenLabel = ilg.DefineLabel();
                        ilg.Emit(OpCodes.Brfalse, notSeenLabel);
                        {
                            ilg.Emit(OpCodes.Ldstr, "Encountered duplicate field \"{0}\".");
                            ilg.Emit(OpCodes.Ldloc, key);
                            ilg.Emit(OpCodes.Call, typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) }));
                            throwException();
                        }

                        ilg.MarkLabel(notSeenLabel);

                        // Record the fact that we've seen this property
                        ilg.Emit(OpCodes.Ldloc, valueSetLocals[parameterIndex]);
                        ilg.Emit(OpCodes.Ldc_I4_1);
                        ilg.Emit(OpCodes.Or);
                        ilg.Emit(OpCodes.Stloc, valueSetLocals[parameterIndex]);
                    }

                    // Read value
                    // The unpacker method expects, on the stack, the unpacker and the address of the value to store to
                    ilg.Emit(OpCodes.Ldarg_0); // unpacker
                    ilg.Emit(OpCodes.Ldloca, valueLocals[parameterIndex]);
                    var unpackerMethod = ValueUnpacker.GetUnpackerMethodForType(parameters[parameterIndex].ParameterType);
                    ilg.Emit(OpCodes.Call, unpackerMethod);

                    // If the unpacker method failed (returned false), throw
                    var typeGetterSuccess = ilg.DefineLabel();
                    ilg.Emit(OpCodes.Brtrue, typeGetterSuccess);
                    {
                        // TODO throw better exception
                        ilg.Emit(OpCodes.Ldstr, "TEST THIS CASE 4");
                        ilg.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] {typeof(string)}));
                        ilg.Emit(OpCodes.Throw);
                    }
                    ilg.MarkLabel(typeGetterSuccess);

                    ilg.Emit(OpCodes.Br, lblEndIfChain);
                }

                if (nextLabel != null)
                    ilg.MarkLabel(nextLabel.Value);

                // If we got here then the property was not recognised. Throw.
                ilg.Emit(OpCodes.Ldstr, "Encountered unexpected field \"{0}\".");
                ilg.Emit(OpCodes.Ldloc, key);
                ilg.Emit(OpCodes.Call, typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) }));
                throwException();

                ilg.MarkLabel(lblEndIfChain);

                // Increment the loop index
                ilg.Emit(OpCodes.Ldloc, loopIndex);
                ilg.Emit(OpCodes.Ldc_I4_1);
                ilg.Emit(OpCodes.Conv_I8);
                ilg.Emit(OpCodes.Add);
                ilg.Emit(OpCodes.Stloc, loopIndex);

                // Loop condition
                ilg.MarkLabel(lblLoopTest);
                ilg.Emit(OpCodes.Ldloc, loopIndex);
                ilg.Emit(OpCodes.Ldloc, mapSize);
                // If the loop is done, jump to the first instruction after the loop
                ilg.Emit(OpCodes.Beq, lblLoopExit);

                // Jump back to the start of the loop
                ilg.Emit(OpCodes.Br, lblLoopStart);

                // Mark the end of the loop
                ilg.MarkLabel(lblLoopExit);
            }

            #region Verify all required values either specified or have a default value

            var lblValuesMissing = ilg.DefineLabel();
            var lblValuesOk = ilg.DefineLabel();

            var paramName = ilg.DeclareLocal(typeof(string));
            for (var i = 0; i < valueSetLocals.Length; i++)
            {
                // Store the name of the parameter we are inspecting
                // for use in the exception later.
                ilg.Emit(OpCodes.Ldstr, parameters[i].Name);
                ilg.Emit(OpCodes.Stloc, paramName);

                // If any value is zero then neither a default nor specified value
                // exists for that parameter, and we cannot continue.
                ilg.Emit(OpCodes.Ldloc, valueSetLocals[i]);
                ilg.Emit(OpCodes.Ldc_I4_0);
                ilg.Emit(OpCodes.Beq, lblValuesMissing);
            }

            // If we got here, all value exist and we're good to continue.
            // Jump to the next section.
            ilg.Emit(OpCodes.Br, lblValuesOk);
            {
                // If we got here then one or more values is missing.
                ilg.MarkLabel(lblValuesMissing);
                ilg.Emit(OpCodes.Ldstr, "Missing required field \"{0}\".");
                ilg.Emit(OpCodes.Ldloc, paramName);
                ilg.Emit(OpCodes.Call, typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) }));
                throwException();
            }
            ilg.MarkLabel(lblValuesOk);

            #endregion

            // Push all values onto the execution stack
            foreach (var valueLocal in valueLocals)
                ilg.Emit(OpCodes.Ldloc, valueLocal);

            // Call the target type's constructor
            ilg.Emit(OpCodes.Newobj, ctor);

            // Return the newly constructed object!
            ilg.Emit(OpCodes.Ret);

            // Return a delegate that performs the above operations
            return (Func<Unpacker, object>)method.CreateDelegate(typeof(Func<Unpacker, object>));
        }

        private static Func<Unpacker, object> BuildCollectionUnpacker(Type parameterType)
        {
            var method = new DynamicMethod(
                $"CollectionDeserialiser{parameterType.Name}",
                typeof(object),
                new[] { typeof(Unpacker) });

            var ilg = method.GetILGenerator();
            
            var isList = parameterType.IsList();
            // Get type for element in array or generic collection. Will use T in further comments for this type
            Type collectionElementType = null;
            if (parameterType.IsSupportedGenericCollection())
                collectionElementType = parameterType.GenericTypeArguments[0];
            else if (parameterType.IsArray())
                collectionElementType = parameterType.GetElementType();

            // Get Generic unpacker
            var genericunpackerMethod = ValueUnpacker.GetUnpackerMethodForType(collectionElementType);
            
            // Lists and arrays are stored as array in msgpack
            // Read msgpack array length first
            var arrLen = ilg.DeclareLocal(typeof(long));
            ilg.Emit(OpCodes.Ldarg_0); // unpacker
            ilg.Emit(OpCodes.Ldloca, arrLen);
            ilg.Emit(OpCodes.Callvirt, typeof(Unpacker).GetMethod("ReadArrayLength"));
            ilg.Emit(OpCodes.Pop); //TODO RESULT

            // Declare List or array depending on parameter type. For some reason using only arrays and create new List<T>(array) at the end
            // of the code causes runtime ecxception TODO investigate
            var typeOfListGeneric = typeof(List<>);
            var typeOfListConcrete = typeOfListGeneric.MakeGenericType(collectionElementType);
            var list = ilg.DeclareLocal(typeOfListConcrete);
            var arr = ilg.DeclareLocal(collectionElementType.MakeArrayType());
            if (isList) //TODO remove if solved problem with creating list at the end
            {
                // Create new List<T>() and store in list
                var listCtor = typeOfListConcrete.GetConstructor(new Type[0]);
                ilg.Emit(OpCodes.Newobj, listCtor);
                ilg.Emit(OpCodes.Stloc, list);
            }
            else
            {
                // Create new array of T and store in arr
                ilg.Emit(OpCodes.Ldloc, arrLen);
                ilg.Emit(OpCodes.Conv_Ovf_I);
                ilg.Emit(OpCodes.Newarr, collectionElementType);
                ilg.Emit(OpCodes.Stloc, arr);
            }

            // Create for loop like for (int arrIndex = 0; arrIndex < arrLen; arrIndex++)
            var arrIndex = ilg.DeclareLocal(typeof(int));
            ilg.Emit(OpCodes.Ldc_I4_0);
            ilg.Emit(OpCodes.Stloc, arrIndex);
            var arrayLoopTest = ilg.DefineLabel();
            var arrayLoopStart = ilg.DefineLabel();

            // Jump to loop test condition
            ilg.Emit(OpCodes.Br_S, arrayLoopTest);

            // Loop start
            ilg.MarkLabel(arrayLoopStart);

            // read value from generic unpacker and store in readValue variable of T
            var readValue = ilg.DeclareLocal(collectionElementType);
            ilg.Emit(OpCodes.Ldarg_0);
            ilg.Emit(OpCodes.Ldloca, readValue);
            ilg.Emit(OpCodes.Call, genericunpackerMethod);
            ilg.Emit(OpCodes.Pop); //TODO TEST RESULT
            
            if (isList) //TODO as above, use arr only
            {
                // Call list.Add(readValue);
                ilg.Emit(OpCodes.Ldloc, list);
                ilg.Emit(OpCodes.Ldloc, readValue);
                ilg.Emit(OpCodes.Callvirt, typeOfListConcrete.GetMethod("Add", new[] {collectionElementType}));
            }
            else
            {
                // Assign readValue to current array element
                ilg.Emit(OpCodes.Ldloc, arr);
                ilg.Emit(OpCodes.Ldloc, arrIndex);
                ilg.Emit(OpCodes.Ldloc, readValue);
                ilg.Emit(OpCodes.Stelem_I4, readValue);
            }

            // Increment the loop index
            ilg.Emit(OpCodes.Ldloc, arrIndex);
            ilg.Emit(OpCodes.Ldc_I4_1);
            ilg.Emit(OpCodes.Add);
            ilg.Emit(OpCodes.Stloc, arrIndex);
            // Test for loop condition if arrIndex<arrLen
            ilg.MarkLabel(arrayLoopTest);
            ilg.Emit(OpCodes.Ldloc, arrIndex);
            ilg.Emit(OpCodes.Conv_I8); //arrLen is long hence convert to Int64
            ilg.Emit(OpCodes.Ldloc, arrLen); 
            ilg.Emit(OpCodes.Clt);
            ilg.Emit(OpCodes.Brtrue, arrayLoopStart);
            
            //After loop we need to assign result to local variable for current parameter

            if (isList) //need create new List
            {
                // TODO this code causes exception
                //var ienum = typeof(IEnumerable<>);
                //var ienumOfType = ienum.MakeGenericType(collectionElementType);
                //var listCtor = typeOfListConcrete.GetConstructor(new Type[] { ienumOfType });
                //ilg.Emit(OpCodes.Ldloc, arr);                
                //ilg.Emit(OpCodes.Newobj, listCtor);
                //ilg.Emit(OpCodes.Stloc, valueLocals[parameterIndex]);

                ilg.Emit(OpCodes.Ldloc, list);
                //ilg.Emit(OpCodes.Stloc, valueLocals[parameterIndex]);
            }
            else
            {
                ilg.Emit(OpCodes.Ldloc, arr);
                //ilg.Emit(OpCodes.Stloc, valueLocals[parameterIndex]);
            }
            //ilg.Emit(OpCodes.Newobj, ctor);

            // Return the newly constructed object!
            ilg.Emit(OpCodes.Ret);

            return (Func<Unpacker, object>)method.CreateDelegate(typeof(Func<Unpacker, object>));
        }

        

        private static void StoreValue(ILGenerator ilg, object value)
        {
            if (value == null)
                ilg.Emit(OpCodes.Ldnull);
            else if (value is int)
                ilg.Emit(OpCodes.Ldc_I4, (int)value);
            else if (value is uint)
                ilg.Emit(OpCodes.Ldc_I4, (int)(uint)value);
            else if (value is byte)
                ilg.Emit(OpCodes.Ldc_I4, (int)(byte)value);
            else if (value is sbyte)
                ilg.Emit(OpCodes.Ldc_I4, (int)(sbyte)value);
            else if (value is short)
                ilg.Emit(OpCodes.Ldc_I4, (int)(short)value);
            else if (value is ushort)
                ilg.Emit(OpCodes.Ldc_I4, (int)(ushort)value);
            else if (value is long)
                ilg.Emit(OpCodes.Ldc_I8, (long)value);
            else if (value is ulong)
                ilg.Emit(OpCodes.Ldc_I8, (long)(ulong)value);
            else if (value is string)
                ilg.Emit(OpCodes.Ldstr, (string)value);
            else if (value is bool)
                ilg.Emit(OpCodes.Ldc_I4, (bool)value ? 1 : 0);
            else if (value is float)
                ilg.Emit(OpCodes.Ldc_R4, (float)value);
            else if (value is double)
                ilg.Emit(OpCodes.Ldc_R8, (double)value);
            else if (value is decimal)
            {
                var bits = decimal.GetBits((decimal)value);
                ilg.Emit(OpCodes.Ldc_I4_4);
                ilg.Emit(OpCodes.Newarr, typeof(int));
                for (var i = 0; i < 4; i++)
                {
                    ilg.Emit(OpCodes.Dup);
                    ilg.Emit(OpCodes.Ldc_I4, i); // index
                    ilg.Emit(OpCodes.Ldc_I4, bits[i]); // value
                    ilg.Emit(OpCodes.Stelem_I4);
                }
                ilg.Emit(OpCodes.Newobj, typeof(decimal).GetConstructor(new[] { typeof(int[]) }));
            }
            else
            {
                throw new NotImplementedException($"No support for default values of type {value?.GetType().Name} (yet).");
            }
        }
    }
}