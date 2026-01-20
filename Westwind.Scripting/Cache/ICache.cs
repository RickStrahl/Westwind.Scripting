using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Westwind.Scripting.Cache
{
    public interface ICache<T_KEY, T_VALUE>
    {

        void Set(T_KEY key, T_VALUE value);

        bool TryGet(T_KEY key, out T_VALUE? value);

        new IEnumerable<T_KEY> Keys();

        new IEnumerable<T_VALUE> Values();

        void Clear();

        bool Contains(T_KEY key);
    }
}
