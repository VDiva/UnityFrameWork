using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace FrameWork
{
    /// <summary>
    /// Unity 全局事件管理器（支持异常隔离、Domain Reload 重置与快照派发）
    /// </summary>
    public static class EventMrg
    {
        private sealed class EventSlot
        {
            public Delegate Combined;
            public Delegate[] Snapshot;

            public EventSlot(Delegate initial)
            {
                Combined = initial;
                Snapshot = initial != null ? initial.GetInvocationList() : Array.Empty<Delegate>();
            }

            public void Update(Delegate next)
            {
                Combined = next;
                Snapshot = next != null ? next.GetInvocationList() : Array.Empty<Delegate>();
            }
        }

        private static readonly Dictionary<int, Dictionary<int, EventSlot>> eventDic = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => eventDic.Clear();

        private static Dictionary<int, EventSlot> GetGroup(int type)
        {
            if (!eventDic.TryGetValue(type, out var dic))
            {
                dic = new Dictionary<int, EventSlot>();
                eventDic.Add(type, dic);
            }

            return dic;
        }

        public static void Subscribe<TType, TEvent>(TType type, TEvent eventId, Action callback)
            where TType : struct, IConvertible
            where TEvent : struct, IConvertible
        {
            Subscribe(UnsafeUtility.EnumToInt(type), UnsafeUtility.EnumToInt(eventId), callback);
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
            if (dic.TryGetValue(eventId, out var slot))
            {
                if (slot.Combined is not Action)
                {
                    Debug.LogError($"事件类型不匹配：{eventId} 已注册为带参数事件");
                    return;
                }
                slot.Update(Delegate.Combine(slot.Combined, callback));
            }
            else
            {
                dic.Add(eventId, new EventSlot(callback));
            }
        }

        public static void Subscribe<TType, TEvent, TData>(TType type, TEvent eventId, Action<TData> callback)
            where TType : struct, IConvertible
            where TEvent : struct, IConvertible
        {
            Subscribe<TData>(UnsafeUtility.EnumToInt(type), UnsafeUtility.EnumToInt(eventId), callback);
        }

        public static void Subscribe<T>(object type, object eventId, Action<T> callback)
        {
            Subscribe<T>((int)type, (int)eventId, callback);
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        public static void Subscribe<T>(int type, int eventId, Action<T> callback)
        {
            if (callback == null)
                return;

            var dic = GetGroup(type);
            if (dic.TryGetValue(eventId, out var slot))
            {
                if (slot.Combined.GetType() != callback.GetType())
                {
                    Debug.LogError($"事件类型不匹配：{eventId} 已注册为其他参数类型");
                    return;
                }
                slot.Update(Delegate.Combine(slot.Combined, callback));
            }
            else
            {
                dic.Add(eventId, new EventSlot(callback));
            }
        }

        public static void Unsubscribe<TType, TEvent, TData>(TType type, TEvent eventId, Action<TData> callback)
            where TType : struct, IConvertible
            where TEvent : struct, IConvertible
        {
            Unsubscribe<TData>(UnsafeUtility.EnumToInt(type), UnsafeUtility.EnumToInt(eventId), callback);
        }

        public static void Unsubscribe<TType, TEvent>(TType type, TEvent eventId, Action callback)
            where TType : struct, IConvertible
            where TEvent : struct, IConvertible
        {
            Unsubscribe(UnsafeUtility.EnumToInt(type), UnsafeUtility.EnumToInt(eventId), callback);
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
            if (callback == null ||
                !eventDic.TryGetValue(type, out var dic) ||
                !dic.TryGetValue(eventId, out var slot) ||
                slot.Combined is not Action)
                return;

            Delegate newAction = Delegate.Remove(slot.Combined, callback);
            if (newAction == null)
                dic.Remove(eventId);
            else
                slot.Update(newAction);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public static void Unsubscribe<T>(int type, int eventId, Action<T> callback)
        {
            if (callback == null ||
                !eventDic.TryGetValue(type, out var dic) ||
                !dic.TryGetValue(eventId, out var slot) ||
                slot.Combined.GetType() != callback.GetType())
                return;

            var newAction = Delegate.Remove(slot.Combined, callback);
            if (newAction == null)
                dic.Remove(eventId);
            else
                slot.Update(newAction);
        }

        public static void Trigger<TType, TEvent, TData>(TType type, TEvent eventId, TData data)
            where TType : struct, IConvertible
            where TEvent : struct, IConvertible
        {
            Trigger<TData>(UnsafeUtility.EnumToInt(type), UnsafeUtility.EnumToInt(eventId), data);
        }

        public static void Trigger<TType, TEvent>(TType type, TEvent eventId)
            where TType : struct, IConvertible
            where TEvent : struct, IConvertible
        {
            Trigger(UnsafeUtility.EnumToInt(type), UnsafeUtility.EnumToInt(eventId));
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
                !dic.TryGetValue(eventId, out var slot))
                return;

            if (slot.Combined is not Action)
            {
                Debug.LogError($"事件类型不匹配：{eventId} 是带参数事件");
                return;
            }

            var snapshot = slot.Snapshot;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { ((Action)snapshot[i]).Invoke(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        public static void Trigger<T>(int type, int eventId, T data)
        {
            if (!eventDic.TryGetValue(type, out var dic) ||
                !dic.TryGetValue(eventId, out var slot))
                return;

            if (slot.Combined is not Action<T>)
            {
                Debug.LogError($"事件类型不匹配 {eventId} 参数类型错误");
                return;
            }

            var snapshot = slot.Snapshot;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { ((Action<T>)snapshot[i]).Invoke(data); }
                catch (Exception ex) { Debug.LogException(ex); }
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
