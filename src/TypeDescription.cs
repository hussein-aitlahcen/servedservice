using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading.Tasks;

namespace ServedService
{
    public sealed class TypeDescription
    {
        public sealed class MethodDescription
        {
            private delegate void MethodProxy(Stream input, Stream output);

            private readonly object _instance;
            private readonly Type[] _inputTypes;
            private readonly Type _instanceType;
            private readonly Type _outputType;
            private readonly MethodInfo _method;
            private readonly DynamicMethod _protoSerializer;
            private readonly MethodProxy _methodProxy; 

            public MethodDescription(object instance, MethodInfo methodInfo)
            {
                _instance = instance;
                _instanceType = instance.GetType();
                _outputType = methodInfo.ReturnType;
                _method = methodInfo;

                if (_outputType == typeof (void))
                    _outputType = typeof (PlaceHolderType);

                var instanceType = instance.GetType();
                var inputParams = methodInfo.GetParameters().ToList();

                _protoSerializer = GenerateProtoSerializer();
                
                if (inputParams.Count > 0)
                {
                    _inputTypes = inputParams.Select(param => param.ParameterType).ToArray();
                    _methodProxy = GenerateParameteredMethod();
                }
                else
                {
                    _methodProxy = GenerateParameterlessMethod();
                }
            }

            private MethodProxy GenerateParameteredMethod()
            {
                var wrapper = GenerateParameteredWrapper();
                var paramFactory = GenerateProtoExtractor();
                return (input, output) =>
                {
                    var parameters = paramFactory(input);
                    wrapper(_instance, parameters, output);
                };
            }

            private MethodProxy GenerateParameterlessMethod()
            {
                var wrapper = GenerateParameterlessWrapper();
                return (input, output) => 
                {
                    wrapper(_instance, output);
                };
            }

            private DynamicMethod GenerateProtoSerializer()
            {
                var protoSerializer = new DynamicMethod("ProtoSerializer_" + _outputType.Name, typeof(void), new[] { typeof(Stream), _outputType });
                var protoSerializeMethod = typeof(ProtoBuf.Serializer)
                    .GetMethods()
                    .Where(method => method.Name == "Serialize")
                    .First(
                        method =>
                            method.GetParameters().Any(param => param.ParameterType == typeof(Stream)) &&
                            method.GetGenericArguments().Length > 0);

                var il = protoSerializer.GetILGenerator();
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.EmitCall(OpCodes.Call, protoSerializeMethod.MakeGenericMethod(_outputType), null);
                    il.Emit(OpCodes.Ret);
                }
                return protoSerializer;
            }

            public static TParam DeserializeParam<TParam>(Stream input)
            {
                var reader = new BinaryReader(input);
                var length = reader.ReadInt32();
                var serializedParam = reader.ReadBytes(length);
                using (var paramStream = new MemoryStream(serializedParam))
                    return ProtoBuf.Serializer.Deserialize<TParam>(paramStream);
            }

            private Func<Stream, object[]> GenerateProtoExtractor()
            {
                var protoExtractor = new DynamicMethod("ProtoExtract_" + _method.Name, typeof(object[]), new[] { typeof(Stream) });
                var protoDeserializeMethod = GetType().GetMethods(BindingFlags.Static | BindingFlags.Public).First(method => method.Name == "DeserializeParam");
                var il = protoExtractor.GetILGenerator();
                {
                    var localArray = il.DeclareLocal(typeof(object[]));
                    il.Emit(OpCodes.Ldc_I4, _inputTypes.Length);
                    il.Emit(OpCodes.Newarr, typeof(object));
                    il.Emit(OpCodes.Stloc, localArray);

                    for (var i = 0; i < _inputTypes.Length; i++)
                    {
                        var currentParamType = _inputTypes[i];
                        il.Emit(OpCodes.Ldloc, localArray);
                        il.Emit(OpCodes.Ldc_I4, i);
                        il.Emit(OpCodes.Ldarg_0);
                        il.EmitCall(OpCodes.Call, protoDeserializeMethod.MakeGenericMethod(currentParamType), null);
                        if (currentParamType.IsValueType)
                        {
                            il.Emit(OpCodes.Box, currentParamType);
                        }
                        il.Emit(OpCodes.Stelem_Ref);
                    }
                    il.Emit(OpCodes.Ldloc, localArray);
                    il.Emit(OpCodes.Ret);
                }
                return (Func<Stream, object[]>)protoExtractor.CreateDelegate(typeof(Func<Stream, object[]>));
            }

            private Action<object, Stream> GenerateParameterlessWrapper()
            {
                var wrapper = new DynamicMethod(_method.Name, typeof(void), new[] { typeof(object), typeof(Stream) });
                var il = wrapper.GetILGenerator();
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Castclass, _instanceType);
                    il.EmitCall(OpCodes.Callvirt, _method, null);
                    if (_outputType != typeof(PlaceHolderType))
                        il.EmitCall(OpCodes.Call, _protoSerializer, null);
                    else
                        il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ret);
                }
                return (Action<object, Stream>)wrapper.CreateDelegate(typeof(Action<object, Stream>));
            }

            private Action<object, object[], Stream> GenerateParameteredWrapper()
            {
                var wrapper = new DynamicMethod(_method.Name, typeof(void), new[] { typeof(object), typeof(object[]), typeof(Stream) });
                var il = wrapper.GetILGenerator();
                {
                    /*
                     * void wrapper(object instance, object param1, object param2, object param3, Stream output) {
                     *      var result = ((InstanceType)instance).Method((ParamType1)param, (ParamType2)param2, (ParamType3)param3);
                     *      Protobuf.Serializer.Serialize(output, result);
                     * }
                     */
                    // output
                    il.Emit(OpCodes.Ldarg_2);
                    // instance
                    il.Emit(OpCodes.Ldarg_0);
                    // (instanceType)instance
                    il.Emit(OpCodes.Castclass, _instanceType);
                    for (var i = 0; i < _inputTypes.Length; i++)
                    {
                        var currentParamType = _inputTypes[i];
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Ldc_I4, i);
                        il.Emit(OpCodes.Ldelem_Ref);
                        if (currentParamType.IsValueType)
                            il.Emit(OpCodes.Unbox_Any, currentParamType);
                        else
                            il.Emit(OpCodes.Castclass, currentParamType);
                    }
                    il.EmitCall(OpCodes.Callvirt, _method, null);
                    if(_outputType != typeof(PlaceHolderType))
                        il.EmitCall(OpCodes.Call, _protoSerializer, null);
                    else
                        il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ret);
                }
                return (Action<object, object[], Stream>)wrapper.CreateDelegate(typeof(Action<object, object[], Stream>));
            }

            public void Execute(Stream input, Stream output)
            {
                _methodProxy(input, output);
            }
        }

        private readonly Dictionary<string, MethodDescription> _methods; 

        public TypeDescription(object instance)
        {
            _methods = new Dictionary<string, MethodDescription>();
            foreach (
                var methodInfo in
                    instance.GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                _methods.Add(methodInfo.Name, new MethodDescription(instance, methodInfo));
            }
        }

        public bool HasMethod(string name)
        {
            return _methods.ContainsKey(name);
        }

        public void CallMethod(string name, Stream input, Stream output)
        {
            if(!_methods.ContainsKey(name))
                throw new Exception("Unknow method name : " + name);
            _methods[name].Execute(input, output);
        }
    }
}
