using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace Calculator.Shared
{
    public enum OperationType
    {
        Add,
        Mul,
        Sub,
        Xor,
        Or,
        And
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class OperationRequest
    {
        public OperationType Type { get; set; }
        public int A { get; set; }
        public int B { get; set; }

        public OperationRequest() { }

        public OperationRequest(OperationType type, int a, int b)
        {
            Type = type;
            A = a;
            B = b;
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public sealed class OperationResult
    {
        public int Computed { get; set; }

        public OperationResult() { }
        public OperationResult(int output)
        {
            Computed = output;
        }
    }
}
