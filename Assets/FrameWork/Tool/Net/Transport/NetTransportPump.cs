#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.Net.Transport
{
    internal interface IMainThreadPumpable
    {
        void PumpMainThreadEvents();
        void AbortOnQuit();
    }

    /// <summary>
    /// 将 TCP / KCP 后台线程的网络事件安全派发到 Unity 主线程，并在退出时中止后台套接字。
    /// </summary>
    internal sealed class NetTransportPump : MonoBehaviour
    {
        private static NetTransportPump _instance;
        private static bool _isQuitting;
        private static readonly object Gate = new object();
        private static readonly List<IMainThreadPumpable> Active = new List<IMainThreadPumpable>();
        private static readonly List<IMainThreadPumpable> Snapshot = new List<IMainThreadPumpable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isQuitting = false;
            lock (Gate)
            {
                Active.Clear();
                Snapshot.Clear();
            }
        }

        public static void Register(IMainThreadPumpable pumpable)
        {
            if (pumpable == null || _isQuitting)
                return;

            EnsureInstance();
            lock (Gate)
            {
                if (!Active.Contains(pumpable))
                    Active.Add(pumpable);
            }
        }

        public static void Unregister(IMainThreadPumpable pumpable)
        {
            if (pumpable == null)
                return;
            lock (Gate)
            {
                Active.Remove(pumpable);
            }
        }

        private static void EnsureInstance()
        {
            if (_instance != null || _isQuitting)
                return;

            var go = new GameObject("[NetTransportPump]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetTransportPump>();
        }

        private void Update()
        {
            Snapshot.Clear();
            lock (Gate)
            {
                for (int i = 0; i < Active.Count; i++)
                    Snapshot.Add(Active[i]);
            }

            for (int i = 0; i < Snapshot.Count; i++)
            {
                try
                {
                    Snapshot[i].PumpMainThreadEvents();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            Snapshot.Clear();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            AbortAll();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            AbortAll();
        }

        private static void AbortAll()
        {
            List<IMainThreadPumpable> toAbort;
            lock (Gate)
            {
                toAbort = new List<IMainThreadPumpable>(Active);
                Active.Clear();
            }

            for (int i = 0; i < toAbort.Count; i++)
            {
                try { toAbort[i].AbortOnQuit(); }
                catch (Exception ex) { Debug.LogWarning($"[NetTransportPump] Abort 异常: {ex.Message}"); }
            }
        }
    }
}
#endif
