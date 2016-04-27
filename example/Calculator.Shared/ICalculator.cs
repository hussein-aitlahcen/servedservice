using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calculator.Shared
{
    public interface ICalculator
    {
        OperationResult Compute(OperationRequest request);
    }
}
