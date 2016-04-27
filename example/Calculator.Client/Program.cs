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

            const int size = 10000;
            var watch = Stopwatch.StartNew();
            var before = watch.ElapsedMilliseconds;
            for (var i = 0; i < size; i++)
            {

                service.Compute
                    (
                        new OperationRequest
                            (
                                OperationType.Mul,
                                i*2,
                                i*i
                            )
                    );
            }
            var delta = watch.ElapsedMilliseconds - before;
            Console.WriteLine(delta + "ms");
            Console.Read();
        }
    }
}
