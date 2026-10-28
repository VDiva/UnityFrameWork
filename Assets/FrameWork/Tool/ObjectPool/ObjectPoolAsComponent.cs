using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class ObjectPoolAsComponent<T> where T : Component
    {
        private readonly int _capacity;
        private int _created;
        private readonly Func<T> _factory;
        private readonly Queue<T> _items = new Queue<T>();
        private readonly HashSet<T> _idle = new HashSet<T>(PoolReferenceComparer<T>.Instance);

        public ObjectPoolAsComponent(Func<T> func, int num = -1)
        {
            if (num < -1) throw new ArgumentOutOfRangeException(nameof(num));
            _factory = func ?? throw new ArgumentNullException(nameof(func));
            _capacity = num;
        }

        public ObjectPoolAsComponent(int num = -1) : this(() => new GameObject(typeof(T).Name).AddComponent<T>(), num) {}

        public void EnQueue(T item)
        {
            if (item == null) return;
            if (_idle.Contains(item))
            {
                MyLog.LogError("添加重复的对象进对象池!!!!");
                return;
            }
            if (_capacity >= 0 && _items.Count >= _capacity)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                return;
            }
            _idle.Add(item);
            _items.Enqueue(item);
            item.gameObject.SetActive(false);
        }

        public T DeQueue()
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
                item.gameObject.SetActive(true);
                return item;
            }
            if (_capacity >= 0 && _created >= _capacity) return null;
            _created++;
            try
            {
                var item = _factory();
                if (item == null) _created--;
                else item.gameObject.SetActive(true);
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
