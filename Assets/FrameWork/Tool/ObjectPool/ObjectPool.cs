

using System;
using System.Collections.Concurrent;

namespace FrameWork
{
    public class ObjectPool<T> where T: class, new()
    {
        private System.Type _type;
        private int _num;
        private int _currentNum;
        private ConcurrentQueue<T> _objectPool;


        public int GetSize()
        {
            return _objectPool.Count;
        }
        
        public ObjectPool(int num=-1)
        {
            _objectPool = new ConcurrentQueue<T>();
            _type = typeof(T);
            _currentNum = 0;
            _num = num;
        }
        
        public void EnQueue(T t)
        {
            _objectPool.Enqueue(t);
        }


        public T DeQueue()
        {
            
            if (_objectPool.TryDequeue(out T t))
            {
                return t;
            }
            else
            {
                if (_num==-1)
                {
                    T t2 = new T();
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
                        T t2 = new T();
                        return t2;
                    }
                }
            }
        }
    }
}