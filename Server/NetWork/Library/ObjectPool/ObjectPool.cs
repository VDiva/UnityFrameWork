

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Library.ObjectPool
{
    public class ObjectPool<T> where T: class, new()
    {
        private System.Type _type;
        private int _num;
        private int _currentNum;
        private List<T> _objectPool;
        private Func<T> _func;
        private bool IsFun;
        public int GetSize()
        {
            return _objectPool.Count;
        }
        
        public ObjectPool(Func<T> func,int num=-1)
        {
            IsFun = true;
            _objectPool = new List<T>();
            _func = func;
            _type = typeof(T);
            _currentNum = 0;
            _num = num;
        }
        
        public ObjectPool(int num=-1)
        {
            IsFun = false;
            _objectPool = new List<T>();
            _type = typeof(T);
            _currentNum = 0;
            _num = num;
        }
        
        public void EnQueue(T t)
        {
            if (!_objectPool.Contains(t))
            {
                _objectPool.Add(t);
            }
            else
            {
                // MyLog.LogError("添加重复的对象进对象池!!!!");
            }
        }


        public T DeQueue(Action<T> action=null)
        {
            
            if (_objectPool.Count>0)
            {
                var item = _objectPool[0];
                _objectPool.RemoveAt(0);
                return item;
            }
            else
            {
                if (_num==-1)
                {
                    T t2 = IsFun ? _func() : new T();
                    action?.Invoke(t2);
                    return t2;
                }
                else
                {
                    if (_currentNum>_num)
                    {
                        return null;
                    }
                    else
                    {
                        _currentNum += 1;
                        T t2 = IsFun ? _func() : new T();
                        action?.Invoke(t2);
                        return t2;
                    }
                }
            }
        }
    }
}