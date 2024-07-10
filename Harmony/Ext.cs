using System;
using System.Collections.Generic;

namespace Numeira
{
    internal static class Ext
    {
        public static TValue GetOrAdd<TDictionary, TKey, TValue>(this TDictionary dictionary, TKey key, Func<TKey, TValue> factory) where TDictionary : IDictionary<TKey, TValue>
        {
            if (!dictionary.TryGetValue(key, out TValue value))
            {
                value = factory(key);
                dictionary.Add(key, value);
            }
            return value;
        }
    }
}
