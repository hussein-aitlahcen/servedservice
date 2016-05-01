using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ServedService.Service
{
    public sealed class ClassProxy
    {
        private readonly Dictionary<string, MethodProxy> _methods;

        public ClassProxy(object instance)
        {
            _methods = new Dictionary<string, MethodProxy>();
            foreach (
                var methodInfo in
                    instance.GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                _methods.Add(methodInfo.Name, new MethodProxy(instance, methodInfo));
            }
        }

        public bool HasMethod(string name)
        {
            return _methods.ContainsKey(name);
        }

        public void CallMethod(string name, Stream input, Stream output)
        {
            if (!_methods.ContainsKey(name))
                throw new Exception("Unknow method name : " + name);
            _methods[name].Execute(input, output);
        }
    }
}
