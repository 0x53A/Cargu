using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cargu
{
    // multi-map - one to many
    internal class LoookUp<TKey, TVal>
    {
        private Dictionary<TKey, List<TVal>> _data = new Dictionary<TKey, List<TVal>>();

        public void Add(TKey key, TVal val)
        {
            List<TVal> l;
            if (!_data.TryGetValue(key, out l))
            {
                l = new List<TVal>();
                _data.Add(key, l);
            }

            l.Add(val);
        }

        public bool ContainsKey(TKey key)
        {
            if (_data.TryGetValue(key, out var l))
                return l.Count > 0;

            return false;
        }

        public Dictionary<TKey, TVal[]> ToResult()
        {
            return _data.Where(x => x.Value.Any()).ToDictionary(x => x.Key, x => x.Value.ToArray());
        }
    }
}
