using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var service = new ServiceProxy("127.0.0.1", 4444)
                .GetService<ICalculator>("com.servedservice.calculator");

            var result = service.Compute
            (
                new OperationRequest
                (
                    OperationType.Mul,
                    10, 
                    5
                )
            );
            
            Console.WriteLine(result.Computed);
            Console.Read();
        }
    }
}
