using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FrameWork
{
    public class XlsxData<T,TV>
    {
       
        public Dictionary<T, List<TV>> _dicData;
        public XlsxData(string key,IList obj)
        {
            _dicData = new Dictionary<T, List<TV>>();
            var list = obj;
            var count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var t = list[i].GetType();
                var key1 = (T)t.GetField(key).GetValue(list[i]);
                if (_dicData.ContainsKey(key1))
                {
                    _dicData[key1].Add((TV)list[i]);
                }
                else
                {
                    _dicData.Add(key1,new List<TV>(){(TV)list[i]});
                }
                
            }
        }

        public TV ByKeyGetValue(T key)
        {
            if (_dicData.TryGetValue(key,out var data))
            {
                return data[0];
            }

            return default(TV);
        }
        
        public List<TV> ByKeyGetValues(T key)
        {
            if (_dicData.TryGetValue(key,out var data))
            {
                return data;
            }

            return default;
        }
    }
    
    public class XlsxData<T,TK,TV>
    {
        public Dictionary<T,Dictionary<TK,TV>> _dicData;
        public XlsxData(string key,string key2,IList obj)
        {
            _dicData = new Dictionary<T, Dictionary<TK,TV>>();
            var list = obj;
            var count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var t = list[i].GetType();
                var dicKey1 = (T)t.GetField(key).GetValue(list[i]);
                var dicKey2 = (TK)t.GetField(key2).GetValue(list[i]);
                if (_dicData.ContainsKey(dicKey1))
                {
                    _dicData[dicKey1].Add(dicKey2,(TV)list[i]);
                }
                else
                {
                    _dicData.Add(dicKey1,new Dictionary<TK, TV>(){{dicKey2,(TV)list[i]}});
                }
            }
        }

        public TV ByKeyGetValue(T key,TK key2)
        {
            if (_dicData.TryGetValue(key,out var data)&& data.TryGetValue(key2,out var data2))
            {
                return data2;
            }

            return default(TV);
        }
    }
}