using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class ObjectPoolAsGameObject 
    {
        private readonly int _capacity;
        private int _created;
        private readonly Func<GameObject> _factory;
        private readonly Queue<GameObject> _items = new Queue<GameObject>();
        private readonly HashSet<GameObject> _idle = new HashSet<GameObject>(PoolReferenceComparer<GameObject>.Instance);

        public ObjectPoolAsGameObject(Func<GameObject> func, int num = -1)
        {
            if (num < -1) throw new ArgumentOutOfRangeException(nameof(num));
            _factory = func ?? throw new ArgumentNullException(nameof(func));
            _capacity = num;
        }

        

        public void EnQueue(GameObject item)
        {
            if (item == null) return;
            if (_idle.Contains(item))
            {
                MyLog.LogError("添加重复的对象进对象池!!!!");
                return;
            }
            if (_capacity >= 0 && _items.Count >= _capacity)
            {
                UnityEngine.Object.Destroy(item);
                return;
            }
            _idle.Add(item);
            _items.Enqueue(item);
            item.SetActive(false);
        }

        public GameObject DeQueue()
        {
            while (_items.Count > 0)
            {
                var item = _items.Dequeue();
                _idle.Remove(item);
                if (item == null)
                {
                    _created = Math.Max(0, _created - 1);
                    continue;
                }
                item.SetActive(true);
                return item;
            }
            if (_capacity >= 0 && _created >= _capacity) return null;
            _created++;
            try
            {
                var item = _factory();
                if (item == null) _created--;
                else item.SetActive(true);
                return item;
            }
            catch
            {
                _created--;
                throw;
            }
        }
    }
}
