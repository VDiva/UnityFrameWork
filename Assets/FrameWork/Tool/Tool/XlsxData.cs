using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FrameWork
{
    public class XlsxData<T,TV>
    {
       
        private Dictionary<T, TV> _dictionary;
        public XlsxData(string key,IList obj)
        {
            _dictionary = new Dictionary<T, TV>();
            var list = obj;
            var count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var t = list[i].GetType();
                _dictionary.Add((T)t.GetField(key).GetValue(list[i]),(TV)list[i]);
            }
        }

        public TV ByKeyGetValue(T key)
        {
            if (_dictionary.TryGetValue(key,out var data))
            {
                return data;
            }

            return default(TV);
        }
    }
    
    public class XlsxData<T,TK,TV>
    {
        private Dictionary<T,Dictionary<TK,TV>> _dictionary;
        public XlsxData(string key,string key2,IList obj)
        {
            _dictionary = new Dictionary<T, Dictionary<TK,TV>>();
            var list = obj;
            var count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var t = list[i].GetType();
                
                if (_dictionary.ContainsKey((T)t.GetField(key).GetValue(list[i])))
                {
                    _dictionary[(T)t.GetField(key).GetValue(list[i])].Add((TK)t.GetField(key2).GetValue(list[i]),(TV)list[i]);
                }
                else
                {
                    _dictionary.Add((T)t.GetField(key).GetValue(list[i]),new Dictionary<TK, TV>(){{(TK)t.GetField(key2).GetValue(list[i]),(TV)list[i]}});
                            
                }
            }
        }

        public TV ByKeyGetValue(T key,TK key2)
        {
            if (_dictionary.TryGetValue(key,out var data)&& data.TryGetValue(key2,out var data2))
            {
                return data2;
            }

            return default(TV);
        }
    }
}