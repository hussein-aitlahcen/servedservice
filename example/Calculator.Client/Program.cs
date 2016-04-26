using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calculator.Shared;
using ServedService;

namespace Calculator.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var proxy = new ServiceProxy("127.0.0.1", 4444)
                .GetService<ICalculator>("com.servedservice.calculator");
            var result = proxy.Compute(new Operation()
            {
                Type = OperationType.Or,
                A = 8,
                B = 1,
            });
        }
    }
}
