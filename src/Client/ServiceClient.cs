using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeProject.ObjectPool;

namespace ServedService.Client
{
    public sealed class ServiceClient
    {
        private sealed class PooledBuffer : PooledObject
        {
            internal byte[] Buffer { get; private set; }

            public PooledBuffer(int size)
            {
                Buffer = new byte[size];
            }
        }

        private const int MinPoolSize = 10;
        private const int MaxPoolSize = 1000;
        private const int ReceiveBufferSize = 1024;

        private static readonly ObjectPool<PooledBuffer> CachedBuffers = new ObjectPool<PooledBuffer>(MinPoolSize,
            MaxPoolSize, () => new PooledBuffer(ReceiveBufferSize))
        {
            Diagnostics = new ObjectPoolDiagnostics()
            {
                Enabled = true,
            }
        };

        private readonly Dictionary<Type, Func<string, object>> _factory;
        private readonly string _host;
        private readonly int _port;
        private readonly TcpClient _client;
        private readonly PooledBuffer _buffer;

        public ServiceClient(string host, int port)
        {
            _factory = new Dictionary<Type, Func<string, object>>();
            _host = host;
            _port = port;
            _client = new TcpClient(_host, _port);
            _buffer = CachedBuffers.GetObject();
        }

        public T GetService<T>(string nameSpace)
        {
            return Generate<T>(nameSpace);
        }

        private T Generate<T>(string nameSpace)
        {
            if (!Exists<T>())
                RegisterDefinition<T>(nameSpace);
            return (T) _factory[typeof (T)](nameSpace);
        }

        private bool Exists<T>()
        {
            return _factory.ContainsKey(typeof (T));
        }

        public static void SerializeParam<TParam>(MemoryStream serializedParams, TParam param)
        {
            var writer = new BinaryWriter(serializedParams);
            var lastPosition = serializedParams.Position;
            writer.Write(0); // length placeholder
            ProtoBuf.Serializer.Serialize(serializedParams, param);
            var length = (int)(serializedParams.Position - lastPosition - 4);
            serializedParams.Position = lastPosition;
            writer.Write(length);
            serializedParams.Position = serializedParams.Length;
        }

        public TOut CallServiceMethod<TOut>(string nameSpace, string method, MemoryStream serializedParams)
        {
            var stream = _client.GetStream();
            using (var burst = new MemoryStream())
            {
                using (var output = new BinaryWriter(burst))
                {
                    output.Write(0);
                    output.Write(nameSpace);
                    output.Write(method);
                    output.Write(serializedParams.ToArray());
                    var messageLength = (int)burst.Length;
                    burst.Position = 0;
                    output.Write(messageLength - 4);
                    burst.Position = messageLength;
                    stream.Write(burst.ToArray(), 0, (int)burst.Position);
                }
            }
            var bytes = _buffer.Buffer;
            var length = stream.Read(bytes, 0, bytes.Length) - 1;
            var success = bytes[0] == 1;
            if (!success)
                throw new Exception("Remote exception " + nameSpace + "." + method + " : " + Encoding.Default.GetString(bytes, 1, length));
            if (typeof(TOut) == typeof(PlaceHolderType))
                return default(TOut);
            using (var input = new MemoryStream(bytes, 1, length))
            {
                return ProtoBuf.Serializer.Deserialize<TOut>(input);
            }
        }

        private void RegisterDefinition<T>(string nameSpace)
        {
            var contract = typeof (T);
            if (!contract.IsInterface)
                throw new InvalidOperationException("Type T must be an interface");

            // public sealed class ServiceProxy_ContractName
            var asmName = new AssemblyName("ServiceProxy_" + contract.Name);
            var asmBuilder = Thread.GetDomain().DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var modBuilder = asmBuilder.DefineDynamicModule(asmBuilder.GetName().Name, false);
            var typeBuilder = modBuilder.DefineType(contract.FullName,
                TypeAttributes.Public |
                TypeAttributes.Sealed |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null,
                new[] {contract});

            // add the contract implementations
            typeBuilder.AddInterfaceImplementation(contract);

            // private string _namespace;
            // private ServiceClient _client;
            var nameSpaceField = typeBuilder.DefineField("_namespace", typeof (string), FieldAttributes.Private);
            var clientField = typeBuilder.DefineField("_client", typeof (ServiceClient), FieldAttributes.Private);

            /* public ServiceProxy_ContractName(string namespace, ServiceClient client) 
             * {
             *      _namespace = namespace;
             *      _client = client;
             * }
            */
            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                new[] {typeof (string), typeof (ServiceClient) });
            var il = ctor.GetILGenerator();
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, nameSpaceField);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Stfld, clientField);
                il.Emit(OpCodes.Ret);
            }

            var callServiceMethod = GetType().GetMethod("CallServiceMethod");

            var serializeParamMethod = GetType().GetMethod("SerializeParam");

            var streamType = typeof (MemoryStream);
            var streamDisposeMethod = streamType.GetMethod("Dispose");

            foreach (var method in contract.GetMethods())
            {
                var methodParams = method.GetParameters().ToArray();
                var methodParamsTypes = methodParams.Select(param => param.ParameterType).ToArray();
                var methodReturnType = method.ReturnType;

                var methBuilder = typeBuilder.DefineMethod(
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot |
                    MethodAttributes.Virtual | MethodAttributes.Final,
                    methodReturnType,
                    methodParamsTypes);
                typeBuilder.DefineMethodOverride(methBuilder, method);

                // We reduce the complexity of managing both methods that return something and nothing
                // as methods that return something
                if (methodReturnType == typeof (void))
                    methodReturnType = typeof (PlaceHolderType);

                // Generated method will looks like this 
                /*
                 * public MethodReturnType IInterface.MethodName(TypeA paramA, TypeB paramB, TypeX paramX) 
                 * {
                 *      using(var serializedParams = new MemoryStream())
                 *      {
                 *          SerializeParam<TypeA>(stream, paramA);
                 *          SerializeParam<TypeB>(stream, paramB);
                 *          SerializeParam<TypeX>(stream, paramX);
                 *          if(MethodReturnType == typeof(PlaceHolderType)
                 *              _client.CallServiceMethod<PlaceHolderType>(_namespace, "MethodName", serializedParams);
                 *          else
                 *              return _client.CallServiceMethod<MethodReturnType>(_namespace, "MethodName", serializedParams);
                 *      }
                 * }
                 */
                il = methBuilder.GetILGenerator();
                {
                    var localStream = il.DeclareLocal(streamType);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, clientField);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, nameSpaceField);
                    il.Emit(OpCodes.Ldstr, method.Name);
                    il.Emit(OpCodes.Newobj, streamType.GetConstructor(Type.EmptyTypes));
                    il.Emit(OpCodes.Stloc, localStream);
                    il.Emit(OpCodes.Ldloc, localStream);
                    for (var i = 0; i < methodParams.Length; i++)
                    {
                        var currentParam = methodParams[i];
                        il.Emit(OpCodes.Ldloc, localStream);
                        switch (i)
                        {
                            case 0:
                                il.Emit(OpCodes.Ldarg_1);
                                break;
                            case 1:
                                il.Emit(OpCodes.Ldarg_2);
                                break;
                            case 2:
                                il.Emit(OpCodes.Ldarg_3);
                                break;
                            default:
                                il.Emit(OpCodes.Ldarg, i + 1);
                                break;
                        }
                        il.EmitCall(OpCodes.Call, serializeParamMethod.MakeGenericMethod(currentParam.ParameterType),
                            null);
                    }
                    il.EmitCall(OpCodes.Callvirt, callServiceMethod.MakeGenericMethod(methodReturnType), null);
                    if(methodReturnType == typeof(PlaceHolderType))
                        il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldloc, localStream);
                    il.EmitCall(OpCodes.Call, streamDisposeMethod, null);
                    il.Emit(OpCodes.Ret);
                }

            }

            var type = typeBuilder.CreateType();
            var factory = FastActivator.GenerateDelegate<Func<string, ServiceClient, object>>(type, typeof (string), typeof (ServiceClient));

            _factory[contract] = (ns) => factory(ns, this);
        }
    }
}
