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
        
        public static void AddListener(object evtType, object evt, Action<object[]> listener)
        {
            AddListener((int)evtType,(int)evt,listener);
        }

        // 添加事件注册
        public static void AddListener(int evtType,int evt,Action<object[]> listener)
        {
            if (_listeners.TryGetValue(evtType,out var actions))
            {
                if (!actions.ContainsKey(evt))
                {
                    actions.TryAdd(evt,listener);
                }
                else
                {
                    actions[evt] += listener;
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

        public static void DispatchEvent(object evtType, object evt, object[] data = null)
        {
            DispatchEvent((int)evtType,(int)evt,data);
        }

        // 事件的触发
        public static void DispatchEvent(int evtType,int evt,object[] data=null)
        {
            if (_listeners.TryGetValue(evtType,out var actions)&& actions.TryGetValue(evt,out var action))
            {
                action?.Invoke(data);
            }
        }

        public static void RemoveListener(object evtType, object evt, Action<object[]> listener)
        {
            RemoveListener((int)evtType,(int)evt,listener);
        }

        // 移除事件
        public static void RemoveListener(int evtType,int evt, Action<object[]> listener)
        {
            if (_listeners.TryGetValue(evtType,out var actions)&& actions.TryGetValue(evt,out var action))
            {
                action -= listener;
            }
        }
    }
}