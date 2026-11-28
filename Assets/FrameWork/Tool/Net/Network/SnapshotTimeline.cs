using System;
using System.Collections.Generic;

namespace FrameWork.Net.Network
{
    /// <summary>按发送端时间播放固定容量快照；只处理时间轴，不依赖 Unity 或传输协议。</summary>
    internal sealed class SnapshotTimeline<T>
    {
        readonly struct Sample
        {
            public readonly double Time;
            public readonly T Value;
            public Sample(double time, T value) { Time = time; Value = value; }
        }

        readonly List<Sample> samples;
        readonly int capacity;
        double playbackTime;
        double playbackTimeScale = 1.0;
        double lastArrivalElapsed;
        double lastSentTime;
        double estimatedJitter;
        bool hasArrivalSample;

        public int Count => samples.Count;
        /// <summary>EWMA 估计的网络到达抖动（秒），供上层动态调整缓冲窗口。</summary>
        public double EstimatedJitter => estimatedJitter;

        public SnapshotTimeline(int capacity = 8)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            samples = new List<Sample>(capacity);
        }

        public void Clear()
        {
            samples.Clear();
            playbackTime = 0;
            playbackTimeScale = 1.0;
            lastArrivalElapsed = 0;
            lastSentTime = 0;
            hasArrivalSample = false;
        }

        public bool TryGetLatest(out T value)
        {
            if (samples.Count == 0) { value = default!; return false; }
            value = samples[samples.Count - 1].Value;
            return true;
        }

        public void Add(double sentTime, T value, double bufferTime)
        {
            int count = samples.Count;
            if (count > 0)
            {
                double lastTime = samples[count - 1].Time;
                if (sentTime - lastTime > 1.0)
                {
                    Clear();
                    count = 0;
                }
                else if (sentTime <= lastTime)
                    sentTime = lastTime + 0.001;
            }
            bufferTime = Math.Max(0.02, bufferTime);
            if (count == 0)
            {
                playbackTime = sentTime - bufferTime;
                playbackTimeScale = 1.0;
                lastSentTime = sentTime;
                lastArrivalElapsed = 0;
                hasArrivalSample = true;
            }
            else
            {
                if (hasArrivalSample)
                {
                    double sentDelta = Math.Max(0.001, sentTime - lastSentTime);
                    double transitJitter = Math.Abs(lastArrivalElapsed - sentDelta);
                    if (transitJitter < 0.5)
                        estimatedJitter += (transitJitter - estimatedJitter) * 0.15;
                }
                lastSentTime = sentTime;
                lastArrivalElapsed = 0;
                hasArrivalSample = true;

                double desired = sentTime - bufferTime;
                double error = desired - playbackTime;
                if (Math.Abs(error) > bufferTime * 2)
                {
                    playbackTime = desired;
                    playbackTimeScale = 1.0;
                }
                else
                {
                    // 用播放速率微调（0.92x ~ 1.08x）平滑追帧，避免直接跳变 playbackTime 造成微顿挫。
                    double scaleAdjust = Math.Max(-0.08, Math.Min(0.08, error * 0.6));
                    playbackTimeScale = 1.0 + scaleAdjust;
                }
            }
            samples.Add(new Sample(sentTime, value));
            if (samples.Count > capacity) samples.RemoveAt(0);
        }

        public bool TrySample(double deltaTime, double maxExtrapolation,
            out T from, out T to, out float alpha, out float extrapolation)
        {
            from = default!; to = default!; alpha = 0; extrapolation = 0;
            if (samples.Count == 0) return false;
            double clampedDt = Math.Max(0, Math.Min(0.1, deltaTime));
            lastArrivalElapsed += clampedDt;
            playbackTime += clampedDt * playbackTimeScale;
            // 无新包时缓慢回归 1.0x，防止长期偏速
            playbackTimeScale += (1.0 - playbackTimeScale) * Math.Min(1.0, clampedDt * 2.0);
            while (samples.Count > 1 && samples[1].Time <= playbackTime)
                samples.RemoveAt(0);
            Sample first = samples[0];
            from = first.Value;
            to = first.Value;
            if (samples.Count > 1 && playbackTime > first.Time)
            {
                Sample second = samples[1];
                to = second.Value;
                alpha = (float)((playbackTime - first.Time) /
                    Math.Max(0.001, second.Time - first.Time));
            }
            else
                extrapolation = (float)Math.Max(0,
                    Math.Min(Math.Max(0, maxExtrapolation), playbackTime - first.Time));
            return true;
        }
    }
}
