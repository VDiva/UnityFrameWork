using System.Collections.Concurrent;
using UnityEngine;

namespace FrameWork.ObjectPool
{
    public class ObjectPoolAsGameObject
    {
       
        private int _num;
        private int _currentNum;
        private GameObject _go;
        private ConcurrentQueue<GameObject> _objectPool;
        
        public ObjectPoolAsGameObject(GameObject go,int num=-1)
        {
            _objectPool = new ConcurrentQueue<GameObject>();
            _go = go;
            _currentNum = 0;
            _num = num;
        }



        public void EnQueue(GameObject go)
        {
            go.SetActive(false);
            _objectPool.Enqueue(go);
        }


        public GameObject DeQueue()
        {
            
            if (_objectPool.TryDequeue(out GameObject go))
            {
                go.SetActive(true);
                return go;
            }
            else
            {
                if (_num==-1)
                {
                    GameObject obj = GameObject.Instantiate(_go);
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
                        GameObject obj = GameObject.Instantiate(_go);
                        obj.SetActive(true);
                        return go;
                    }
                }
            }
        }
    }
}