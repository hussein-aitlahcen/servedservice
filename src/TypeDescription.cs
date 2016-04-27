using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading.Tasks;

namespace ServedService
{
    internal sealed class TypeDescription
    {
        internal sealed class MethodDescription
        {
            private delegate void MethodProxy(Stream input, Stream output);

            private readonly object _instance;
            private readonly Type _inputType;
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
                if (inputParams.Count > 1)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Invalid parameters count on method {0}, consider using a single parameter, even if it has to be a complex type",
                            methodInfo.Name));
                }
                
                _protoSerializer = GenerateProtoSerializer();
                
                if (inputParams.Count > 0)
                {
                    _inputType = inputParams.First().ParameterType;
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
                var protoExtractor = GenerateProtoExtractor();

                var paramFactory = (Func<Stream, object>)protoExtractor.CreateDelegate(typeof(Func<Stream, object>));
                var wrapperDelegate = (Action<object, object, Stream>)wrapper.CreateDelegate(typeof(Action<object, object, Stream>));

                return (input, output) =>
                {
                    wrapperDelegate(_instance, paramFactory(input), output);
                };
            }

            private MethodProxy GenerateParameterlessMethod()
            {
                var wrapper = GenerateParameterlessWrapper();
                var wrapperDelegate = (Action<object, Stream>)wrapper.CreateDelegate(typeof(Action<object, Stream>));

                return (input, output) => 
                {
                    wrapperDelegate(_instance, output);
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

            private DynamicMethod GenerateProtoExtractor()
            {
                var protoExtractor = new DynamicMethod("ProtoExtract_" + _inputType.Name, _inputType, new[] { typeof(Stream) });
                var protoDeserializeMethod = typeof(ProtoBuf.Serializer).GetMethod("Deserialize");
                var il = protoExtractor.GetILGenerator();
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.EmitCall(OpCodes.Call, protoDeserializeMethod.MakeGenericMethod(_inputType), null);
                    il.Emit(OpCodes.Ret);
                }
                return protoExtractor;
            }

            private DynamicMethod GenerateParameterlessWrapper()
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
                return wrapper;
            }

            private DynamicMethod GenerateParameteredWrapper()
            {
                var wrapper = new DynamicMethod(_method.Name, typeof(void), new[] { typeof(object), typeof(object), typeof(Stream) });
                var il = wrapper.GetILGenerator();
                {
                    /*
                     * void wrapper(object instance, object param, Stream output) {
                     *      var result = ((InstanceType)instance).Method((ParamType)param)
                     *      Protobuf.Serializer.Serialize(output, result);
                     * }
                     */
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Castclass, _instanceType);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Castclass, _inputType);
                    il.EmitCall(OpCodes.Callvirt, _method, null);
                    if(_outputType != typeof(PlaceHolderType))
                        il.EmitCall(OpCodes.Call, _protoSerializer, null);
                    else
                        il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ret);
                }
                return wrapper;
            }

            public void Execute(Stream input, Stream output)
            {
                _methodProxy(input, output);
            }
        }

        private Dictionary<string, MethodDescription> _methods; 

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
