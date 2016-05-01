using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServedService.Service
{
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<string, ClassProxy> _classByNamespace;
        private readonly NetworkHost _host;

        public ServiceRegistry(string host, int port)
        {
            _classByNamespace = new Dictionary<string, ClassProxy>();
            _host = new NetworkHost(host, port);
            _host.OnBytesReceived += ServeRequest;
        }

        private void ServeRequest(Socket socket, Stream input)
        {
            using (var reader = new BinaryReader(input))
            {
                reader.BaseStream.Position = 0;
                var nameSpace = reader.ReadString();
                var method = reader.ReadString();
                using (var output = new MemoryStream())
                {
                    // success byte
                    output.Write(new byte[1] { 1 }, 0, 1);
                    try
                    {
                        CallMethod(nameSpace, method, input, output);
                    }
                    catch (Exception e)
                    {
                        // sucess byte turned into failed one
                        output.GetBuffer()[0] = 0;
                        // transfert the error message
                        output.Write(Encoding.Default.GetBytes(e.Message), 0, e.Message.Length);
                    }
                    _host.Send(socket, output.ToArray());
                }
            }
        }

        private void CallMethod(string nameSpace, string method, Stream input, Stream output)
        {
            if (!_classByNamespace.ContainsKey(nameSpace))
                throw new Exception("Unknow namespace " + nameSpace);
            _classByNamespace[nameSpace].CallMethod(method, input, output);
        }

        private void AddClass(string nameSpace, object instance)
        {
            if (_classByNamespace.ContainsKey(nameSpace))
                throw new InvalidOperationException(string.Format("Namespace already used {0}, consider using an empty namespace", nameSpace));
            _classByNamespace.Add(nameSpace, new ClassProxy(instance));
        }

        public ServiceRegistry Register<T>(string nameSpace, T instance)
        {
            AddClass(nameSpace, instance);
            return this;
        }

        public ServiceRegistry Start()
        {
            _host.Start();
            return this;
        }

        public ServiceRegistry Stop()
        {
            _host.Stop();
            return this;
        }
    }
}
