using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class ObjectPoolAsComponent<T> where T: Component
    {
        private System.Type _type;
        private int _num;
        private int _currentNum;
        private List<T> _objectPool;
        private Func<T> _func;
        private bool IsFun;
        public ObjectPoolAsComponent(Func<T> func,int num=-1)
        {
            _func = func;
            _objectPool = new List<T>();
            _type = typeof(T);
            _currentNum = 0;
            _num = num;
        }

        
        public ObjectPoolAsComponent(int num=-1)
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
                t.gameObject.SetActive(false);
            }
            else
            {
                MyLog.LogError("添加重复的对象进对象池!!!!");
            }
            
        }


        public T DeQueue()
        {
            
            if (_objectPool.Count>0)
            {
                var item = _objectPool[0];
                _objectPool.RemoveAt(0);
                item.gameObject.SetActive(true);
                return item;
            }
            else
            {
                if (_num==-1)
                {
                    
                    if (IsFun)
                    {
                        return _func?.Invoke();
                    }
                    else
                    {
                       GameObject obj = new GameObject(_type.Name);
                        obj.SetActive(true);
                        T t2=obj.AddComponent(_type) as T;
                        return t2;
                    }
                     
                    
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
                        if (IsFun)
                        {
                            return _func?.Invoke();
                        }
                        else
                        {
                            GameObject obj = new GameObject(_type.Name);
                            obj.SetActive(true);
                            T t2=obj.AddComponent(_type) as T;
                            return t2;
                        }
                    }
                }
            }
        }
    }
}