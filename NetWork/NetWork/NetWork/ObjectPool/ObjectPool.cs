

using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace FrameWork.ObjectPool
{
    public class ObjectPool<T> where T: class, new()
    {
        private Type _type;
        private int _num;
        private int _currentNum;
        private ConcurrentQueue<T> _objectPool;
        private Action<T> action;
        public ObjectPool(Action<T> action = null, int num = -1)
        {
            _objectPool = new ConcurrentQueue<T>();
            _type = typeof(T);
            _currentNum = 0;
            _num = num;
            this.action = action;
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
                    FieldInfo[] data= _type.GetFields();
                    foreach(FieldInfo item in data)
                    {
                        FieldInfo field = _type.GetField(item.Name);
                        Type type = field.GetType();
                        ConstructorInfo[] constructor= type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic);
                        if (constructor.Length>0)
                        {
                            field.SetValue(t2, Activator.CreateInstance(field.GetType()));
                        }
                    }
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
                        T t2 = new T();
                        FieldInfo[] data = _type.GetFields();
                        foreach (FieldInfo item in data)
                        {
                            FieldInfo field = _type.GetField(item.Name);
                            Type type = field.GetType();
                            ConstructorInfo[] constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic);
                            if (constructor.Length > 0)
                            {
                                field.SetValue(t2, Activator.CreateInstance(field.GetType()));
                            }
                        }
                        action?.Invoke(t2);
                        return t2;
                    }
                }
            }
        }
    }
}