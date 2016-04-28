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

    [ProtoContract]
    public sealed class OperationRequest
    {
        [ProtoMember(1)]
        public OperationType Type { get; set; }
        [ProtoMember(2)]
        public int A { get; set; }
        [ProtoMember(3)]
        public int B { get; set; }

        public OperationRequest() { }

        public OperationRequest(OperationType type, int a, int b)
        {
            Type = type;
            A = a;
            B = b;
        }
    }

    [ProtoContract]
    public sealed class OperationResult
    {
        [ProtoMember(1)]
        public int Computed { get; set; }

        public OperationResult() { }
        public OperationResult(int output)
        {
            Computed = output;
        }
    }
}
