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
        public int Compute(Operation operation)
        {
            switch (operation.Type)
            {
                case OperationType.Add:
                    return operation.A + operation.B;
                case OperationType.Sub:
                    return operation.A - operation.B;
                case OperationType.Mul:
                    return operation.A*operation.B;
                case OperationType.And:
                    return operation.A & operation.B;
                case OperationType.Or:
                    return operation.A | operation.B;
                case OperationType.Xor:
                    return operation.A ^ operation.B;
            }
            throw new InvalidOperationException("Unknow operation type " + operation.Type);
        }
    }
}
