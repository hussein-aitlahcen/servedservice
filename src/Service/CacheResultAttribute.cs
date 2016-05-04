using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServedService.Service
{
    public enum CacheSideEnum
    {
        CLIENT,
        SERVER
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CacheResultAttribute : Attribute
    {
    }
}
