using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calculator.Shared;
using ServedService;
using ServedService.Client;

namespace Calculator.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var service = new ServiceClient("127.0.0.1", 4444)
                .GetService<ICalculator>("com.servedservice.calculator");

            const int size = 100000;
            var watch = Stopwatch.StartNew();
            var before = watch.ElapsedMilliseconds;
            for (var i = 0; i < size; i++)
            {
                var result = service.Compute
                    (
                        new OperationRequest
                            (
                                OperationType.Mul,
                                i * 2,
                                i * i
                            )
                    );

                var resultB = service.Parameterless();
                var resultC = service.YoBoss(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            }
            var delta = watch.ElapsedMilliseconds - before;
            Console.WriteLine(delta + "ms");
            Console.Read();
        }
    }
}
