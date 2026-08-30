using System;
using System.Collections.Generic;

namespace FrameWork
{
    /// <summary>
    /// 基于 UniTask 的轻量定时器，不再创建协程或在 Update 中遍历定时器列表。
    /// 默认使用受 Time.timeScale 影响的游戏时间。
    /// </summary>
    public static class Timer
    {
        private static readonly HashSet<TimeData> TimeDatas = new HashSet<TimeData>();
        private static readonly Dictionary<string, TimeData> TimeDic = new Dictionary<string, TimeData>();

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
        public static TimeData IntervalCall(float time, Action call)
        {
            return CreateTimer(true, time, -1f, call);
        }

        /// <summary>
        /// 每隔指定秒数调用一次，累计调用指定次数后自动停止。
        /// count 小于或等于 0 时不会执行回调。
        /// </summary>
        public static TimeData IntervalCallAsCount(float intervalTime, int count, Action call)
        {
            return CreateTimer(true, intervalTime, -1f, call, Math.Max(0, count));
        }

        /// <summary>
        /// 每隔指定秒数调用一次，并在持续时间结束后自动停止。
        /// </summary>
        public static TimeData IntervalCallAsTime(float time, float intervalTime, Action call)
        {
            return CreateTimer(true, time, intervalTime, call);
        }

        /// <summary>
        /// 延迟指定秒数后调用一次。
        /// </summary>
        public static TimeData DelayCall(float time, Action call)
        {
            return CreateTimer(false, time, -1f, call);
        }

        /// <summary>
        /// 相同 key 再次添加时取消旧定时器，并从现在重新计时。
        /// </summary>
        public static void AddTimeAsReset(string key, float time, Action end)
        {
            CancelKeyTimer(key, false);
            TimeDic[key] = DelayCall(time, end);
        }

        /// <summary>
        /// 相同 key 再次添加时立即执行旧回调，然后重新计时。
        /// </summary>
        public static void AddTimeAsCall(string key, float time, Action end)
        {
            CancelKeyTimer(key, true);
            TimeDic[key] = DelayCall(time, end);
        }

        /// <summary>
        /// 重置同 key 定时器、执行 start，并在延迟结束后执行 end。
        /// </summary>
        public static void AddTimeAsAdd(string key, float time, Action start, Action end)
        {
            CancelKeyTimer(key, true);
            TimeDic[key] = DelayCall(time, end);
            start?.Invoke();
        }
        
        /// <summary>
        /// 取消旧定时器 每多少秒执行一次 一共执行多少次
        /// </summary>
        /// <param name="key"></param>
        /// <param name="time"></param>
        /// <param name="count"></param>
        /// <param name="call"></param>
        public static void AddTimeAsCount(string key, float time,int count,Action call)
        {
            CancelKeyTimer(key, false);
            TimeDic[key] = IntervalCallAsCount(time, count, call);
        }

        internal static void OnTimerFinished(TimeData timeData)
        {
            TimeDatas.Remove(timeData);

            string removeKey = null;
            foreach (KeyValuePair<string, TimeData> pair in TimeDic)
            {
                if (ReferenceEquals(pair.Value, timeData))
                {
                    removeKey = pair.Key;
                    break;
                }
            }

            if (removeKey != null)
                TimeDic.Remove(removeKey);
        }

        private static TimeData CreateTimer(
            bool isInterval, float delay, float duration, Action call, int callCount = -1)
        {
            var data = new TimeData();
            TimeDatas.Add(data);
            data.Init(isInterval, delay, duration, call, callCount);
            return data;
        }

        private static void CancelKeyTimer(string key, bool invokeOldCallback)
        {
            if (!TimeDic.TryGetValue(key, out TimeData oldTimer))
                return;

            TimeDic.Remove(key);
            if (invokeOldCallback && oldTimer.IsRunning)
                oldTimer.Call();

            DestroyTimer(oldTimer);
        }
    }
}
