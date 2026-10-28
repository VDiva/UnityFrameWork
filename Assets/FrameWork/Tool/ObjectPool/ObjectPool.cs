using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace FrameWork
{
    internal sealed class PoolReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly PoolReferenceComparer<T> Instance = new PoolReferenceComparer<T>();
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>主线程对象池；num 限制创建总数及空闲容量，-1 表示不限。</summary>
    public class ObjectPool<T> where T : class, new()
    {
        private readonly int _capacity;
        private int _created;
        private readonly Queue<T> _items = new Queue<T>();
        private readonly HashSet<T> _idle = new HashSet<T>(PoolReferenceComparer<T>.Instance);
        private readonly Func<UniTask<T>> _factory;

        public ObjectPool(Func<UniTask<T>> func, int num = -1) : this(num)
        {
            _factory = func ?? throw new ArgumentNullException(nameof(func));
        }

        public ObjectPool(int num = -1)
        {
            if (num < -1) throw new ArgumentOutOfRangeException(nameof(num));
            _capacity = num;
        }

        public int GetSize() => _items.Count;

        public bool EnQueue(T item)
        {
            if (item == null || (item is UnityEngine.Object unityObject && unityObject == null) || (_capacity >= 0 && _items.Count >= _capacity) || !_idle.Add(item))
                return false;
            _items.Enqueue(item);
            return true;
        }

        public void EnQueue(List<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var item in items) EnQueue(item);
        }

        public async UniTask<T> DeQueue()
        {
            while (_items.Count > 0)
            {
                var item = _items.Dequeue();
                _idle.Remove(item);
                if (item is UnityEngine.Object unityObject && unityObject == null)
                {
                    _created = Math.Max(0, _created - 1);
                    continue;
                }
                return item;
            }
            if (_capacity >= 0 && _created >= _capacity) return null;
            // await 前预留名额，失败或返回 null 时归还。
            _created++;
            try
            {
                var item = _factory != null ? await _factory() : new T();
                if (item == null) _created--;
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
