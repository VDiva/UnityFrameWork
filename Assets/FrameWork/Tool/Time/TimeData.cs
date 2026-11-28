using System;
using UnityEngine;

namespace FrameWork
{
    /// <summary>
    /// 集中式定时器句柄，由 TimerDriver 统一驱动，不再为每个定时器维护独立 UniTask 每帧 Yield 状态机。
    /// 支持受 Time.timeScale 影响的游戏时间以及忽略 timeScale 的真实时间。
    /// </summary>
    public sealed class TimeData
    {
        private bool _isInterval;
        private float _delay;
        private float _duration;
        private float _elapsed;
        private float _nextTriggerTime;
        private int _remainingCalls;
        private bool _ignoreTimeScale;
        private Action _action;
        private bool _cancelled;
        internal bool Poolable;
        internal int ActiveIndex = -1;

        internal string Key { get; set; }
        internal Action Callback => _action;

        public bool IsRunning { get; private set; }

        /// <summary>
        /// 立即执行一次当前定时器的回调，不会改变原定时计划。
        /// </summary>
        public void Call()
        {
            _action?.Invoke();
        }

        internal void Init(
            bool isInterval, float delay, float duration, Action action, int callCount = -1, bool ignoreTimeScale = false)
        {
            _isInterval = isInterval;
            _delay = Math.Max(0f, delay);
            _duration = duration;
            _elapsed = 0f;
            _remainingCalls = callCount;
            _ignoreTimeScale = ignoreTimeScale;
            _action = action;
            _cancelled = false;
            IsRunning = true;

            if (_isInterval && _remainingCalls == 0)
            {
                Finish();
                return;
            }

            float now = GetNow();
            _nextTriggerTime = now + _delay;
            Timer.RegisterActive(this);
        }

        internal void Cancel()
        {
            if (!IsRunning)
                return;

            _cancelled = true;
            Finish();
        }

        internal void Tick(float scaledNow, float unscaledNow, float scaledDelta, float unscaledDelta)
        {
            if (!IsRunning || _cancelled)
                return;

            float now = _ignoreTimeScale ? unscaledNow : scaledNow;
            if (now < _nextTriggerTime)
                return;

            if (!_isInterval)
            {
                var callback = _action;
                Finish();
                InvokeSafe(callback);
                return;
            }

            float step = _delay > 0f ? _delay : (_ignoreTimeScale ? unscaledDelta : scaledDelta);
            _elapsed += step;
            if (_duration >= 0f && _elapsed > _duration)
            {
                Finish();
                return;
            }

            var intervalCallback = _action;
            if (_remainingCalls > 0)
                _remainingCalls--;

            bool done = _remainingCalls == 0 || (_duration >= 0f && _elapsed >= _duration);
            if (done)
            {
                Finish();
            }
            else
            {
                // 防止 0 秒循环在同一帧持续执行，至少推迟到下一帧。
                _nextTriggerTime = _delay > 0f ? now + _delay : now;
            }

            InvokeSafe(intervalCallback);
        }

        private float GetNow() => _ignoreTimeScale ? Time.unscaledTime : Time.time;

        private static void InvokeSafe(Action callback)
        {
            if (callback == null) return;
            try { callback.Invoke(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void Finish()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            _action = null;
            Timer.UnregisterActive(this);
            Timer.OnTimerFinished(this);
            if (Poolable)
            {
                Key = null;
                Poolable = false;
                Timer.ReturnPooled(this);
            }
        }
    }
}
