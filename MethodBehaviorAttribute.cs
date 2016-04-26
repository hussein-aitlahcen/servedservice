using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServedService
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MethodBehaviorAttribute : Attribute
    {
    }
}
