using System.Collections.Concurrent;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace FrameWork
{
    public class ObjectPoolAsGameObject
    {
       
        private int _num;
        private int _currentNum;
        private GameObject _go;
        private List<GameObject> _objectPool;
        private Func<GameObject> _func;
        public ObjectPoolAsGameObject(Func<GameObject> func,int num=-1)
        {
            _func = func;
            _objectPool = new List<GameObject>();
            _currentNum = 0;
            _num = num;
        }
        
        public void EnQueue(GameObject go)
        {
            if (!_objectPool.Contains(go))
            {
                _objectPool.Add(go);
                go.gameObject.SetActive(false);
            }
            else
            {
                MyLog.LogError("添加重复的对象进对象池!!!!");
            }
        }
        
        public GameObject DeQueue()
        {
            
            if (_objectPool.Count>0)
            {
                var item = _objectPool[0];
                _objectPool.RemoveAt(0);
                item.SetActive(true);
                return item;
            }
            else
            {
                if (_num==-1)
                {
                    GameObject obj = _func.Invoke();
                    obj.SetActive(true);
                    return obj;
                }
                else
                {
                    if (_currentNum>_num)
                    {
                        return null;
                    }
                    else
                    {
                        GameObject obj = _func.Invoke();
                        obj.SetActive(true);
                        return obj;
                    }
                }
            }
        }
    }
}