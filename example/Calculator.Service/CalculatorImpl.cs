using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calculator.Shared;

namespace Calculator.Service
{
    public sealed class CalculatorImpl : ICalculator
    {
        public OperationResult Compute(OperationRequest request)
        {
            switch (request.Type)
            {
                case OperationType.ADD:
                    return new OperationResult(request.A + request.B);
                case OperationType.SUB:
                    return new OperationResult(request.A - request.B);
                case OperationType.MUL:
                    return new OperationResult(request.A*request.B);
                case OperationType.AND:
                    return new OperationResult(request.A & request.B);
                case OperationType.OR:
                    return new OperationResult(request.A | request.B);
                case OperationType.XOR:
                    return new OperationResult(request.A ^ request.B);
            }
            throw new InvalidOperationException("Unknow operation type " + request.Type);
        }

        public string Test(int a, int b, int c, OperationRequest x)
        { 
            return a + "-" + b  +"-" + c;
        }

        public string Parameterless()
        {
            return "Bitch, i'm fabulous";
        }

        public int YoBoss(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
        {
            return a + b + c + d + e + f + g + h + i + j;
        }

        public void Push(List<int> queue)
        {
        }
    }
}
