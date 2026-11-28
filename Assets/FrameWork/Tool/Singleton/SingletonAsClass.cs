using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    /// <summary>
    /// 非泛型静态重置注册表：Unity 不会对开放泛型类执行 [RuntimeInitializeOnLoadMethod]，
    /// 由各泛型单例首次初始化时注册清理委托，确保关闭 Domain Reload 时静态状态正确重置。
    /// </summary>
    internal static class SingletonResetRegistry
    {
        private static readonly List<Action> ResetActions = new List<Action>();

        internal static void Register(Action resetAction)
        {
            if (resetAction != null)
                ResetActions.Add(resetAction);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            for (int i = 0; i < ResetActions.Count; i++)
            {
                try { ResetActions[i]?.Invoke(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            ResetActions.Clear();
        }
    }

    public class SingletonAsClass<T> where T : class, new()
    {
        private static T _instance;
        private static bool _resetRegistered;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (!_resetRegistered)
                    {
                        _resetRegistered = true;
                        SingletonResetRegistry.Register(ResetStatic);
                    }
                    _instance = new T();
                }

                return _instance;
            }
        }

        private static void ResetStatic()
        {
            _instance = null;
            _resetRegistered = false;
        }
    }
}
