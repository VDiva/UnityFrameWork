using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Library.ObjectPool;

namespace Library.EventSystem
{
    public static class EventSystem
    {
        private static ConcurrentDictionary<ushort, ConcurrentDictionary<ushort,Action<List<object>>>> _listeners = new ConcurrentDictionary<ushort, ConcurrentDictionary<ushort,Action<List<object>>>>();
        private static ObjectPool<List<object>> _objectPool = new ObjectPool<List<object>>();
        public static void Init()
        {
            _listeners.Clear();
        }

        static EventSystem()
        {
           
        }
        
        public static void AddListener(Enum evtType, Enum evt, Action<List<object>> listener)
        {
            AddListener((ushort)(object)evtType,(ushort)(object)evt,listener);
        }


        // 添加事件注册
        public static void AddListener(ushort evtType,ushort evt,Action<List<object>> listener)
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
                ConcurrentDictionary<ushort, Action<List<object>>> dictionary = new ConcurrentDictionary<ushort, Action<List<object>>>();
                dictionary.TryAdd(evt, listener);
                //dictionary.Add(evt,listener);
                _listeners.TryAdd(evtType,dictionary);
            }
        }

        public static void DispatchEvent(Enum evtType, Enum evt, List<object> data=null)
        {
            DispatchEvent((ushort)(object)evtType,(ushort)(object)evt,data);
        }

        // 事件的触发
        public static void DispatchEvent(ushort evtType,ushort evt,List<object> data=null)
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

        public static void RemoveListener(Enum evtType, Enum evt, Action<List<object>> listener)
        {
            RemoveListener((ushort)(object)evtType,(ushort)(object)evt,listener);
        }

        // 移除事件
        public static void RemoveListener(ushort evtType,ushort evt, Action<List<object>> listener)
        {
            if (_listeners.ContainsKey(evtType)&& _listeners[evtType].ContainsKey(evt))
            {
                _listeners[evtType][evt] -= listener;
                //action -= listener;
            }
        }
        
        
        // public static void AddListener(Enum eventType,Enum id,Action<List<object>> evt)
        // {
        //    AddListener((int)(object)eventType,(int)(object)id,evt);
        // }
        //
        // public static void RemoveListener(Enum eventType,Enum id,Action<List<object>> evt)
        // {
        //     RemoveListener((int)(object)eventType,(int)(object)id,evt);
        // }
        //
        // public static void DispatchEvent(Enum evtType, Enum evt, List<object> data = null)
        // {
        //     DispatchEvent((int)(object)evtType,(int)(object)evt,data);
        // }
        

        public static List<object> GetEventMsg()
        {
            var msg = _objectPool.DeQueue();
            msg.Clear();
            return msg;
        }
    }
}