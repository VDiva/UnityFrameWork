using System;
using System.Collections.Generic;

namespace FrameWork.Script.WebNet
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

        public int Count => samples.Count;

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
                playbackTime = sentTime - bufferTime;
            else
            {
                double desired = sentTime - bufferTime;
                double error = desired - playbackTime;
                if (Math.Abs(error) > bufferTime * 2)
                    playbackTime = desired;
                else
                    playbackTime += Math.Max(-0.005, Math.Min(0.005, error * 0.1));
            }
            samples.Add(new Sample(sentTime, value));
            if (samples.Count > capacity) samples.RemoveAt(0);
        }

        public bool TrySample(double deltaTime, double maxExtrapolation,
            out T from, out T to, out float alpha, out float extrapolation)
        {
            from = default!; to = default!; alpha = 0; extrapolation = 0;
            if (samples.Count == 0) return false;
            playbackTime += Math.Max(0, Math.Min(0.1, deltaTime));
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
