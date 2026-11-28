using System;
using UnityEngine;
using FrameWork.Net;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// 全局网络质量监控与 NTP 风格时钟同步：
    /// - 维护滑动窗口内最小 RTT 样本以推算稳定的服务器时钟偏移，消除单包抖动偏差；
    /// - 根据 RTT、服务端发送队列等待与本地待发队列动态输出 AI 发送降频倍率与插值缓冲余量。
    /// </summary>
    public static class NetworkQualityMonitor
    {
        private const int ClockWindowSize = 8;

        private struct ClockSample
        {
            public float RttMs;
            public double OffsetSeconds;
        }

        private static readonly ClockSample[] ClockWindow = new ClockSample[ClockWindowSize];
        private static int _clockWindowCount;
        private static int _clockWindowCursor;

        public static bool HasServerClock { get; private set; }
        public static double ServerToLocalOffsetSeconds { get; private set; }
        public static float SmoothedRttMs { get; private set; }
        public static float LatestRttMs { get; private set; } = -1f;
        public static int LastServerQueuePackets { get; private set; }
        public static int LastServerQueueWaitMs { get; private set; }
        public static double LastProbeTime { get; private set; }

        /// <summary>当前估算的服务端统一时间（秒，Unix Epoch）。无样本时退回本地 unscaledTime。</summary>
        public static double ServerNowSeconds => HasServerClock
            ? Time.unscaledTimeAsDouble + ServerToLocalOffsetSeconds
            : Time.unscaledTimeAsDouble;

        /// <summary>弱网拥塞时自动拉长 AI 发送间隔（1.0x ~ 2.2x），优先保障玩家自身流畅度。</summary>
        public static float AiSendIntervalScale { get; private set; } = 1f;

        /// <summary>高 RTT 或服务端发送队列积压时，建议叠加给远端快照时间轴的额外缓冲（秒）。</summary>
        public static float RecommendedExtraBufferSeconds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            _clockWindowCount = 0;
            _clockWindowCursor = 0;
            HasServerClock = false;
            ServerToLocalOffsetSeconds = 0;
            SmoothedRttMs = 0f;
            LatestRttMs = -1f;
            LastServerQueuePackets = 0;
            LastServerQueueWaitMs = 0;
            LastProbeTime = 0;
            AiSendIntervalScale = 1f;
            RecommendedExtraBufferSeconds = 0f;
        }

        public static void RecordLatencySample(GameNet.ServerLatencySample sample, double localReceiveUnscaledTime)
        {
            LastProbeTime = Time.realtimeSinceStartupAsDouble;
            LatestRttMs = sample.RttMs;
            LastServerQueuePackets = sample.ServerQueuePackets;
            LastServerQueueWaitMs = sample.ServerQueueWaitMs;

            if (SmoothedRttMs <= 0f)
                SmoothedRttMs = sample.RttMs;
            else
                SmoothedRttMs += (sample.RttMs - SmoothedRttMs) * 0.25f;

            if (sample.ServerTimeMs > 0)
            {
                double serverSec = sample.ServerTimeMs * 0.001;
                double halfRttSec = Math.Max(0.0, sample.RttMs * 0.0005);
                // 服务端生成时间戳约在 localReceiveUnscaledTime - halfRttSec 时刻
                double offset = serverSec - (localReceiveUnscaledTime - halfRttSec);

                ClockWindow[_clockWindowCursor] = new ClockSample
                {
                    RttMs = sample.RttMs,
                    OffsetSeconds = offset
                };
                _clockWindowCursor = (_clockWindowCursor + 1) % ClockWindowSize;
                if (_clockWindowCount < ClockWindowSize)
                    _clockWindowCount++;

                // 选取窗口内 RTT 最小的样本，最大程度过滤非对称排队抖动。
                int bestIdx = 0;
                float bestRtt = ClockWindow[0].RttMs;
                for (int i = 1; i < _clockWindowCount; i++)
                {
                    if (ClockWindow[i].RttMs < bestRtt)
                    {
                        bestRtt = ClockWindow[i].RttMs;
                        bestIdx = i;
                    }
                }

                double targetOffset = ClockWindow[bestIdx].OffsetSeconds;
                if (!HasServerClock || Math.Abs(targetOffset - ServerToLocalOffsetSeconds) > 1.0)
                    ServerToLocalOffsetSeconds = targetOffset;
                else
                    ServerToLocalOffsetSeconds += (targetOffset - ServerToLocalOffsetSeconds) * 0.2;
                HasServerClock = true;
            }

            UpdateAdaptivePolicy();
        }

        private static void UpdateAdaptivePolicy()
        {
            int localPending = GameNet.PendingSendCount;
            bool severe = SmoothedRttMs >= 260f || LastServerQueueWaitMs >= 120 ||
                          LastServerQueuePackets >= 24 || localPending >= 24;
            bool moderate = !severe && (SmoothedRttMs >= 140f || LastServerQueueWaitMs >= 50 ||
                                        LastServerQueuePackets >= 10 || localPending >= 10);

            AiSendIntervalScale = severe ? 2.2f : (moderate ? 1.5f : 1f);

            float extra = 0f;
            if (LastServerQueueWaitMs > 20)
                extra += Mathf.Min(0.05f, (LastServerQueueWaitMs - 20) * 0.0005f);
            if (SmoothedRttMs > 120f)
                extra += Mathf.Min(0.04f, (SmoothedRttMs - 120f) * 0.0002f);
            RecommendedExtraBufferSeconds = Mathf.Clamp(extra, 0f, 0.08f);
        }
    }
}
