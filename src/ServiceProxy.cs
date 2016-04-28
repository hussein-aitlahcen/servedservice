using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeProject.ObjectPool;

namespace ServedService
{
    public sealed class ServiceProxy
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

        public ServiceProxy(string host, int port)
        {
            _factory = new Dictionary<Type, Func<string, object>>();
            _host = host;
            _port = port;
        }

        public T GetService<T>(string nameSpace)
        {
            return Generate<T>(nameSpace);
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

        public TOut CallMethod<TOut>(string nameSpace, string method, MemoryStream serializedParams)
        {
            using (var client = new TcpClient())
            {
                client.Connect(_host, _port);
                using (var stream = client.GetStream())
                {
                    using (var burst = new MemoryStream())
                    {
                        using (var output = new BinaryWriter(burst))
                        {
                            output.Write(nameSpace);
                            output.Write(method);
                            output.Write(serializedParams.ToArray());
                            stream.Write(burst.ToArray(), 0, (int) burst.Position);
                            stream.Flush();
                        }
                        serializedParams.Dispose();
                    }
                    using (var pooledBuffer = CachedBuffers.GetObject())
                    {
                        var bytes = pooledBuffer.Buffer;
                        var length = stream.Read(bytes, 0, bytes.Length) - 1;
                        var success = bytes[0] == 1;
                        if (!success)
                            throw new Exception(Encoding.Default.GetString(bytes, 1, length));
                        if (typeof(TOut) == typeof(PlaceHolderType))
                            return default(TOut);
                        using (var input = new MemoryStream(bytes, 1, length))
                        {
                            return ProtoBuf.Serializer.Deserialize<TOut>(input);
                        }
                    }
                }
            }
        }

        private T Generate<T>(string nameSpace)
        {
            if (!Exists<T>())
                RegisterDefinition<T>(nameSpace);
            return (T)_factory[typeof(T)](nameSpace);
        }

        private bool Exists<T>()
        {
            return _factory.ContainsKey(typeof(T));
        }

        private void RegisterDefinition<T>(string nameSpace)
        {
            var contract = typeof(T);
            if (!contract.IsInterface)
                throw new InvalidOperationException("Type T must be an interface");

            var asmName = new AssemblyName("ServiceProxy_" + contract.Name);
            var asmBuilder = Thread.GetDomain().DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var modBuilder = asmBuilder.DefineDynamicModule(asmBuilder.GetName().Name, false);
            var typeBuilder = modBuilder.DefineType(contract.FullName,
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null, 
                new [] { contract });
            typeBuilder.AddInterfaceImplementation(contract);

            var nameSpaceField = typeBuilder.DefineField("_namespace", typeof(string), FieldAttributes.Private);
            var serviceField = typeBuilder.DefineField("_service", typeof(ServiceProxy), FieldAttributes.Private);

            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new [] { typeof(string), typeof(ServiceProxy) });
            var il = ctor.GetILGenerator();
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, nameSpaceField);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Stfld, serviceField);
                il.Emit(OpCodes.Ret);
            }

            var callMethod = GetType().GetMethod("CallMethod");

            var serializeParamMethod = GetType().GetMethod("SerializeParam");

            var streamType = typeof(MemoryStream);

            foreach (var method in contract.GetMethods())
            {
                var methodParams = method.GetParameters().ToArray();

                var methBuilder = typeBuilder.DefineMethod(
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, 
                    method.ReturnType,
                    method.GetParameters().Select(param => param.ParameterType).ToArray());
                typeBuilder.DefineMethodOverride(methBuilder, method);


                var outputType = method.ReturnType;
                if (outputType == typeof (void))
                    outputType = typeof (PlaceHolderType);

                il = methBuilder.GetILGenerator();
                {
                    var localStream = il.DeclareLocal(streamType);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, serviceField);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, nameSpaceField);
                    il.Emit(OpCodes.Ldstr, method.Name);
                    

                    il.Emit(OpCodes.Newobj, streamType.GetConstructor(Type.EmptyTypes));
                    il.Emit(OpCodes.Stloc, localStream);
                    il.Emit(OpCodes.Ldloc, localStream);

                    if (methodParams.Length > 0)
                    {
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
                            il.EmitCall(OpCodes.Call, serializeParamMethod.MakeGenericMethod(currentParam.ParameterType), null);
                        }
                        il.EmitCall(OpCodes.Callvirt, callMethod.MakeGenericMethod(outputType), null);
                    }
                    else
                    {
                        il.EmitCall(OpCodes.Callvirt, callMethod.MakeGenericMethod(outputType), null);
                        il.Emit(OpCodes.Pop);
                    }
                    il.Emit(OpCodes.Ret);
                }

            }
            var type = typeBuilder.CreateType();
            var factory = FastActivator.GenerateDelegate(type, typeof(string), typeof(ServiceProxy));
            _factory[contract] = (ns) => factory(new object[] {ns, this});
        }
    }
}
