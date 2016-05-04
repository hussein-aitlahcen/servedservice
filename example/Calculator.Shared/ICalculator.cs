using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Calculator.Shared
{
    public interface ICalculator
    {
        OperationResult Compute(OperationRequest request);
        string Test(int a, int b, int c, OperationRequest x);
        string Parameterless();
        int YoBoss(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j);
        void Push(List<int> queue);
    }
}
