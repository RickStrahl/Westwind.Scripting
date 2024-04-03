using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Westwind.Scripting.Cache
{
    internal class MemoryAssemblyCache : ICache<int, Assembly>
    {
        private readonly ConcurrentDictionary<int, Assembly> Cache = new ConcurrentDictionary<int, Assembly>();

        public void Set(int key, Assembly value) {
            if (Cache.ContainsKey(key))
            {
                if (Cache.TryRemove(key, out _))
                {
                    Cache.TryAdd(key, value);
                }
            }
            else
            {
                Cache.TryAdd(key, value);
            }
        }

        public bool TryGet(int key, out Assembly? value) {
            if (Cache.ContainsKey(key))
            {
                return Cache.TryGetValue(key, out value);
            }
            value = default;
            return false;
        }

        public void Clear()
        {
            Cache.Clear();
        }

        public IEnumerable<int> Keys()
        {
            return Cache.Keys;
        }

        public IEnumerable<Assembly> Values()
        {
            return Cache.Values;
        }

        public bool Contains(int key)
        {
            return Cache.ContainsKey(key);
        }
    }
}
