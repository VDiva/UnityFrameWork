using System;
using System.Collections.Generic;

namespace FrameWork
{
    public static class EventManager
    {
        private static Dictionary<int, Dictionary<int,Action<object[]>>> _listeners = new Dictionary<int, Dictionary<int,Action<object[]>>>();
        
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
                    actions.Add(evt,listener);
                }
                else
                {
                    actions[evt] += listener;
                }
            }
            else
            {
                Dictionary<int, Action<object[]>> dictionary = new Dictionary<int, Action<object[]>>(){{evt,listener}};
                //dictionary.Add(evt,listener);
                _listeners.Add(evtType,dictionary);
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