

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace FrameWork
{
    public class ObjectPool<T> where T: class, new()
    {
        private System.Type _type;
        private int _num;
        private int _currentNum;
        private List<T> _objectPool;
        private Func<UniTask<T>> _func;
        private bool IsFun;
        public int GetSize()
        {
            return _objectPool.Count;
        }
        
        public ObjectPool(Func<UniTask<T>> func,int num=-1)
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
        
        public bool EnQueue(T t)
        {
            if (t == null || _objectPool.Contains(t))
                return false;

            if (_num >= 0 && _objectPool.Count >= _num)
                return false;

            _objectPool.Add(t);
            return true;
        }

        public void EnQueue(List<T> t)
        {
            _objectPool.AddRange(t);
        }


        public async UniTask<T> DeQueue()
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
                    T t2 = IsFun ? await _func() : new T();
                    return t2;
                }
                else
                {
                    if (_currentNum>=_num)
                    {
                        return null;
                    }
                    else
                    {
                        _currentNum += 1;
                        T t2 = IsFun ? await _func() : new T();
                        return t2;
                    }
                }
            }
        }
    }
}