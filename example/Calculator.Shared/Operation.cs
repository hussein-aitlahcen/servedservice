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
    public sealed class Operation
    {
        public OperationType Type { get; set; }
        public int A { get; set; }
        public int B { get; set; }
    }
}
