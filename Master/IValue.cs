using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Master
{
    public interface IValue
    {
        Type GetValueType();
        object GetValue();
    }
}
