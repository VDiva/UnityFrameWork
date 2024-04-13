using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FrameWork
{
    public static class EventManager
    {
        private static ConcurrentDictionary<int, ConcurrentDictionary<int,Action<object[]>>> _listeners = new ConcurrentDictionary<int, ConcurrentDictionary<int,Action<object[]>>>();
        
        public static void Init()
        {
            _listeners.Clear();
        }
        
        // public static void AddListener(Enum evtType, Enum evt, Action<object[]> listener)
        // {
        //     AddListener(evtType,evt,listener);
        // }

        // 添加事件注册
        public static void AddListener(int evtType,int evt,Action<object[]> listener)
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
                ConcurrentDictionary<int, Action<object[]>> dictionary = new ConcurrentDictionary<int, Action<object[]>>();
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
        public static void DispatchEvent(int evtType,int evt,object[] data=null)
        {
            if (_listeners.ContainsKey(evtType)&&  _listeners[evtType].ContainsKey(evt))
            {
                _listeners[evtType][evt]?.Invoke(data);
            }
        }

        // public static void RemoveListener(Enum evtType, Enum evt, Action<object[]> listener)
        // {
        //     RemoveListener(evtType,evt,listener);
        // }

        // 移除事件
        public static void RemoveListener(int evtType,int evt, Action<object[]> listener)
        {
            if (_listeners.ContainsKey(evtType)&& _listeners[evtType].ContainsKey(evt))
            {
                _listeners[evtType][evt] -= listener;
                //action -= listener;
            }
        }
    }
}