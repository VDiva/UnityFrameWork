using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    // public static class EventManager
    // {
    //     private static ConcurrentDictionary<int, ConcurrentDictionary<int,Action<List<object>>>> _listeners = new ConcurrentDictionary<int, ConcurrentDictionary<int,Action<List<object>>>>();
    //     private static ObjectPool<List<object>> _objectPool = new ObjectPool<List<object>>();
    //     public static void Init()
    //     {
    //         _listeners.Clear();
    //     }
    //
    //     static EventManager()
    //     {
    //        
    //     }
    //     
    //     public static void AddListener(object evtType, object evt, Action<List<object>> listener)
    //     {
    //         AddListener((int)evtType,(int)evt,listener);
    //     }
    //
    //
    //     // 添加事件注册
    //     public static void AddListener(int evtType,int evt,Action<List<object>> listener)
    //     {
    //         if (_listeners.ContainsKey(evtType))
    //         {
    //             if (!_listeners[evtType].ContainsKey(evt))
    //             {
    //                 _listeners[evtType].TryAdd(evt,listener);
    //             }
    //             else
    //             {
    //                 _listeners[evtType][evt] += listener;
    //             }
    //         }
    //         else
    //         {
    //             ConcurrentDictionary<int, Action<List<object>>> dictionary = new ConcurrentDictionary<int, Action<List<object>>>();
    //             dictionary.TryAdd(evt, listener);
    //             //dictionary.Add(evt,listener);
    //             _listeners.TryAdd(evtType,dictionary);
    //         }
    //     }
    //
    //     public static void DispatchEvent(object evtType, object evt, List<object> data=null)
    //     {
    //         DispatchEvent((int)evtType,(int)evt,data);
    //     }
    //
    //     // 事件的触发
    //     public static void DispatchEvent(int evtType,int evt,List<object> data=null)
    //     {
    //         if (_listeners.ContainsKey(evtType)&&  _listeners[evtType].ContainsKey(evt))
    //         {
    //             _listeners[evtType][evt]?.Invoke(data);
    //             if (data!=null)
    //             {
    //                 _objectPool.EnQueue(data);
    //             }
    //         }
    //     }
    //
    //     public static void RemoveListener(object evtType, object evt, Action<List<object>> listener)
    //     {
    //         RemoveListener((int)evtType,(int)evt,listener);
    //     }
    //
    //     // 移除事件
    //     public static void RemoveListener(int evtType,int evt, Action<List<object>> listener)
    //     {
    //         if (_listeners.ContainsKey(evtType)&& _listeners[evtType].ContainsKey(evt))
    //         {
    //             _listeners[evtType][evt] -= listener;
    //             //action -= listener;
    //         }
    //     }
    //     
    //     public static List<object> GetEventMsg()
    //     {
    //         var msg = _objectPool.DeQueue();
    //         msg.Clear();
    //         return msg;
    //     }
    // }


    /// <summary>
    /// Unity 全局事件管理器
    /// </summary>
    public static class EventMrg
    {
        private static readonly Dictionary<
            int,
            Dictionary<int, Delegate>
        > eventDic = new();


        /// <summary>
        /// 初始化事件
        /// </summary>
        private static Dictionary<int, Delegate> GetGroup(int type)
        {
            if (!eventDic.TryGetValue(type, out var dic))
            {
                dic = new Dictionary<int, Delegate>();
                eventDic.Add(type, dic);
            }

            return dic;
        }

        public static void Subscribe(object type, object eventId, Action callback)
        {
            Subscribe((int)type, (int)eventId, callback);
        }

        public static void Subscribe(int type, int eventId, Action callback)
        {
            if (callback == null)
                return;

            var dic = GetGroup(type);
            if (dic.TryGetValue(eventId, out var action))
            {
                if (action is not Action)
                {
                    Debug.LogError($"事件类型不匹配：{eventId} 已注册为带参数事件");
                    return;
                }
                dic[eventId] = Delegate.Combine(action, callback);
            }
            else
            {
                dic.Add(eventId, callback);
            }
        }

        public static void Subscribe<T>(object type, object eventId, Action<T> callback)
        {
            Subscribe<T>((int)type, (int)eventId, callback);
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        public static void Subscribe<T>(
            int type,
            int eventId,
            Action<T> callback)
        {
            if (callback == null)
                return;

            var dic = GetGroup(type);


            if (dic.TryGetValue(eventId, out var action))
            {
                if (action is not Action<T>)
                {
                    Debug.LogError($"事件类型不匹配：{eventId} 已注册为其他参数类型");
                    return;
                }
                dic[eventId] = Delegate.Combine(action, callback);
            }
            else
            {
                dic.Add(eventId, callback);
            }
        }


        public static void Unsubscribe<T>(object type, object eventId, Action<T> callback)
        {
            Unsubscribe<T>((int)type, (int)eventId, callback);
        }

        public static void Unsubscribe(object type, object eventId, Action callback)
        {
            Unsubscribe((int)type, (int)eventId, callback);
        }

        public static void Unsubscribe(int type, int eventId, Action callback)
        {
            if (!eventDic.TryGetValue(type, out var dic) ||
                !dic.TryGetValue(eventId, out var action) || action is not Action)
                return;

            Delegate newAction = Delegate.Remove(action, callback);
            if (newAction == null)
                dic.Remove(eventId);
            else
                dic[eventId] = newAction;
        }
        /// <summary>
        /// 取消订阅
        /// </summary>
        public static void Unsubscribe<T>(
            int type,
            int eventId,
            Action<T> callback)
        {
            if (!eventDic.TryGetValue(type, out var dic))
                return;


            if (!dic.TryGetValue(eventId, out var action))
                return;


            var newAction = Delegate.Remove(action, callback);


            if (newAction == null)
            {
                dic.Remove(eventId);
            }
            else
            {
                dic[eventId] = newAction;
            }
        }


        public static void Trigger<T>(object type, object eventId, T data)
        {
            Trigger<T>((int)type, (int)eventId, data);
        }

        public static void Trigger(object type, object eventId)
        {
            Trigger((int)type, (int)eventId);
        }

        public static void Trigger(int type, int eventId)
        {
            if (!eventDic.TryGetValue(type, out var dic) ||
                !dic.TryGetValue(eventId, out var action))
                return;

            if (action is Action callback)
                callback.Invoke();
            else
                Debug.LogError($"事件类型不匹配：{eventId} 是带参数事件");
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        public static void Trigger<T>(
            int type,
            int eventId,
            T data)
        {
            if (!eventDic.TryGetValue(type, out var dic))
                return;


            if (!dic.TryGetValue(eventId, out var action))
                return;


            if (action is Action<T> callback)
            {
                callback.Invoke(data);
            }
            else
            {
                Debug.LogError(
                    $"事件类型不匹配 {eventId} 参数类型错误"
                );
            }
        }



        /// <summary>
        /// 清除所有事件
        /// </summary>
        public static void Clear()
        {
            eventDic.Clear();
        }
    }
}
