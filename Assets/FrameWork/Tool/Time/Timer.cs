using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    /// <summary>
    /// 集中驱动的轻量定时器：单一 TimerDriver 驱动全部活跃定时器，无活跃定时器时自动停用，零空闲开销。
    /// 默认使用受 Time.timeScale 影响的游戏时间，也支持 ignoreTimeScale=true 用于暂停/UI 倒计时场景。
    /// </summary>
    public static class Timer
    {
        private const int MaxPooledTimers = 128;
        private static readonly Dictionary<string, TimeData> TimeDic = new Dictionary<string, TimeData>();
        private static readonly List<TimeData> ActiveTimers = new List<TimeData>(32);
        private static readonly Stack<TimeData> Pool = new Stack<TimeData>(32);
        private static TimerDriver _driver;
        private static bool _isQuitting;

        [AddComponentMenu("")]
        private sealed class TimerDriver : MonoBehaviour
        {
            private void Update()
            {
                Timer.TickAll();
            }

            private void OnApplicationQuit() => _isQuitting = true;

            private void OnDestroy()
            {
                if (_driver == this) _driver = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CancelAllKeyedTimers();
            ActiveTimers.Clear();
            Pool.Clear();
            _driver = null;
            _isQuitting = false;
        }

        private static void EnsureDriver()
        {
            if (_driver != null || _isQuitting || !Application.isPlaying) return;
            var go = new GameObject("[TimerDriver]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<TimerDriver>();
        }

        internal static void RegisterActive(TimeData data)
        {
            if (data == null || data.ActiveIndex >= 0) return;
            data.ActiveIndex = ActiveTimers.Count;
            ActiveTimers.Add(data);
            EnsureDriver();
            if (_driver != null && !_driver.enabled) _driver.enabled = true;
        }

        internal static void UnregisterActive(TimeData data)
        {
            if (data == null) return;
            int index = data.ActiveIndex;
            int last = ActiveTimers.Count - 1;
            if (index >= 0 && index <= last)
            {
                if (index != last)
                {
                    var moved = ActiveTimers[last];
                    ActiveTimers[index] = moved;
                    if (moved != null) moved.ActiveIndex = index;
                }
                ActiveTimers.RemoveAt(last);
            }
            data.ActiveIndex = -1;
            if (_driver != null && ActiveTimers.Count == 0)
                _driver.enabled = false;
        }

        private static void TickAll()
        {
            if (ActiveTimers.Count == 0)
            {
                if (_driver != null) _driver.enabled = false;
                return;
            }

            float scaledNow = Time.time;
            float unscaledNow = Time.unscaledTime;
            float scaledDelta = Time.deltaTime;
            float unscaledDelta = Time.unscaledDeltaTime;

            for (int i = ActiveTimers.Count - 1; i >= 0; i--)
            {
                if (i >= ActiveTimers.Count) continue;
                ActiveTimers[i]?.Tick(scaledNow, unscaledNow, scaledDelta, unscaledDelta);
            }
        }

        internal static void ReturnPooled(TimeData data)
        {
            if (data != null && Pool.Count < MaxPooledTimers)
                Pool.Push(data);
        }

        /// <summary>取消所有带 key 的定时器，不触发结束回调；不影响未命名的定时器。</summary>
        public static void CancelAllKeyedTimers()
        {
            if (TimeDic.Count == 0) return;
            // Cancel 的任务清理可能同步回调 OnTimerFinished，先移除索引再取消。
            var timers = new List<TimeData>(TimeDic.Values);
            TimeDic.Clear();
            foreach (var timer in timers) timer.Cancel();
        }

        /// <summary>取消指定 key 的定时器（若存在）。</summary>
        public static bool CancelKey(string key, bool invokeOldCallback = false)
        {
            if (string.IsNullOrEmpty(key) || !TimeDic.ContainsKey(key)) return false;
            CancelKeyTimer(key, invokeOldCallback);
            return true;
        }

        /// <summary>
        /// 提前取消并删除一个定时器。
        /// </summary>
        public static void DestroyTimer(TimeData timeData)
        {
            if (timeData == null)
                return;

            timeData.Cancel();
            OnTimerFinished(timeData);
        }

        /// <summary>
        /// 每隔指定秒数循环调用，返回值可用于主动取消。
        /// </summary>
        public static TimeData IntervalCall(float time, Action call, bool ignoreTimeScale = false)
        {
            return CreateTimer(true, time, -1f, call, -1, ignoreTimeScale);
        }

        /// <summary>
        /// 每隔指定秒数调用一次，累计调用指定次数后自动停止。
        /// count 小于或等于 0 时不会执行回调。
        /// </summary>
        public static TimeData IntervalCallAsCount(float intervalTime, int count, Action call, bool ignoreTimeScale = false)
        {
            return CreateTimer(true, intervalTime, -1f, call, Math.Max(0, count), ignoreTimeScale);
        }

        /// <summary>
        /// 每隔指定秒数调用一次，并在持续时间结束后自动停止。
        /// </summary>
        public static TimeData IntervalCallAsTime(float time, float intervalTime, Action call, bool ignoreTimeScale = false)
        {
            return CreateTimer(true, time, intervalTime, call, -1, ignoreTimeScale);
        }

        /// <summary>
        /// 延迟指定秒数后调用一次。
        /// </summary>
        public static TimeData DelayCall(float time, Action call, bool ignoreTimeScale = false)
        {
            return CreateTimer(false, time, -1f, call, -1, ignoreTimeScale);
        }

        /// <summary>
        /// 相同 key 再次添加时取消旧定时器，并从现在重新计时。
        /// </summary>
        public static void AddTimeAsReset(string key, float time, Action end, bool ignoreTimeScale = false)
        {
            CancelKeyTimer(key, false);
            CreateKeyTimer(key, false, time, end, -1, ignoreTimeScale);
        }

        /// <summary>
        /// 相同 key 再次添加时立即执行旧回调，然后重新计时。
        /// </summary>
        public static void AddTimeAsCall(string key, float time, Action end, bool ignoreTimeScale = false)
        {
            CancelKeyTimer(key, true);
            CreateKeyTimer(key, false, time, end, -1, ignoreTimeScale);
        }

        /// <summary>
        /// 重置同 key 定时器、执行 start，并在延迟结束后执行 end。
        /// </summary>
        public static void AddTimeAsAdd(string key, float time, Action start, Action end, bool ignoreTimeScale = false)
        {
            CancelKeyTimer(key, true);
            CreateKeyTimer(key, false, time, end, -1, ignoreTimeScale);
            start?.Invoke();
        }

        /// <summary>
        /// 取消旧定时器 每多少秒执行一次 一共执行多少次
        /// </summary>
        public static void AddTimeAsCount(string key, float time, int count, Action call, bool ignoreTimeScale = false)
        {
            CancelKeyTimer(key, false);
            CreateKeyTimer(key, true, time, call, Math.Max(0, count), ignoreTimeScale);
        }

        internal static void OnTimerFinished(TimeData timeData)
        {
            var key = timeData.Key;
            if (key != null && TimeDic.TryGetValue(key, out var current) && ReferenceEquals(current, timeData))
                TimeDic.Remove(key);
        }

        private static void CreateKeyTimer(string key, bool interval, float time, Action call, int count = -1, bool ignoreTimeScale = false)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            // 旧回调可能重入并创建同 key 任务；本次替换也必须取消它。
            CancelKeyTimer(key, false);
            var data = Pool.Count > 0 ? Pool.Pop() : new TimeData();
            data.Key = key;
            data.Poolable = true;
            TimeDic[key] = data;
            data.Init(interval, time, -1f, call, count, ignoreTimeScale);
        }

        private static TimeData CreateTimer(
            bool isInterval, float delay, float duration, Action call, int callCount = -1, bool ignoreTimeScale = false)
        {
            var data = new TimeData();
            data.Init(isInterval, delay, duration, call, callCount, ignoreTimeScale);
            return data;
        }

        private static void CancelKeyTimer(string key, bool invokeOldCallback)
        {
            if (!TimeDic.TryGetValue(key, out TimeData oldTimer))
                return;

            TimeDic.Remove(key);
            var callback = invokeOldCallback && oldTimer.IsRunning ? oldTimer.Callback : null;
            DestroyTimer(oldTimer);
            try { callback?.Invoke(); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
