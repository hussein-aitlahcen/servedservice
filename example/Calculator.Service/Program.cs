using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calculator.Shared;
using ServedService;

namespace Calculator.Service
{
    class Program
    {
        static void Main(string[] args)
        {
            new Servant("127.0.0.1", 4444)
                .Serve<ICalculator>("com.servedservice.calculator", new CalculatorImpl())
                .Start();
            Console.ReadLine();
        }
    }
}
