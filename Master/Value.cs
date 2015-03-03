using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Master
{
    public class Value<T> : IValue
    {
        T value;

        public Value(T value)
        {
            this.value = value;
        }

        public object GetValue()
        {
            return value;
        }

        public Type GetValueType()
        {
            return typeof(T);
        }
    }
}
