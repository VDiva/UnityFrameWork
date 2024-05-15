using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FrameWork
{
    public static class EventManager
    {
        private static ConcurrentDictionary<int, ConcurrentDictionary<int,Action<List<object>>>> _listeners = new ConcurrentDictionary<int, ConcurrentDictionary<int,Action<List<object>>>>();
        private static ObjectPool<List<object>> _objectPool = new ObjectPool<List<object>>();
        public static void Init()
        {
            _listeners.Clear();
        }

        static EventManager()
        {
            for (int i = 0; i < 50; i++)
            {
                _objectPool.EnQueue(new List<object>());
            }   
        }
        
        // public static void AddListener(Enum evtType, Enum evt, Action<object[]> listener)
        // {
        //     AddListener(evtType,evt,listener);
        // }

        // 添加事件注册
        public static void AddListener(int evtType,int evt,Action<List<object>> listener)
        {
            if (_listeners.ContainsKey(evtType))
            {
                if (!_listeners[evtType].ContainsKey(evt))
                {
                    _listeners[evtType].TryAdd(evt,listener);
                }
                else
                {
                    _listeners[evtType][evt] += listener;
                }
            }
            else
            {
                ConcurrentDictionary<int, Action<List<object>>> dictionary = new ConcurrentDictionary<int, Action<List<object>>>();
                dictionary.TryAdd(evt, listener);
                //dictionary.Add(evt,listener);
                _listeners.TryAdd(evtType,dictionary);
            }
        }

        // public static void DispatchEvent(Enum evtType, Enum evt, object[] data = null)
        // {
        //     DispatchEvent(evtType,evt,data);
        // }

        // 事件的触发
        public static void DispatchEvent(int evtType,int evt,List<object> data=null)
        {
            if (_listeners.ContainsKey(evtType)&&  _listeners[evtType].ContainsKey(evt))
            {
                _listeners[evtType][evt]?.Invoke(data);
                if (data!=null)
                {
                    _objectPool.EnQueue(data);
                }
            }
        }

        // public static void RemoveListener(Enum evtType, Enum evt, Action<object[]> listener)
        // {
        //     RemoveListener(evtType,evt,listener);
        // }

        // 移除事件
        public static void RemoveListener(int evtType,int evt, Action<List<object>> listener)
        {
            if (_listeners.ContainsKey(evtType)&& _listeners[evtType].ContainsKey(evt))
            {
                _listeners[evtType][evt] -= listener;
                //action -= listener;
            }
        }

        public static List<object> GetEventMsg()
        {
            var msg = _objectPool.DeQueue();
            msg.Clear();
            return msg;
        }
    }
}