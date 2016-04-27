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

            const int size = 1000;
            var tasks = new Task<int>[size];
            var watch = Stopwatch.StartNew();
            var before = watch.ElapsedMilliseconds;
            for (var i = 0; i < size; i++)
            {
                var current = i;
                tasks[i] = Task.Factory.StartNew(() => 
                    service.Compute
                        (
                            new OperationRequest
                                (
                                    OperationType.Mul,
                                    current * 2,
                                    current * current
                                )
                        ).Computed
                );
            }
            Task.WhenAll(tasks).ContinueWith((task =>
            {
                var delta = watch.ElapsedMilliseconds - before;

                Console.WriteLine(delta + "ms");
            }));
            Console.Read();
        }
    }
}
