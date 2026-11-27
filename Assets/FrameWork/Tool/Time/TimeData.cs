using System;
using Cysharp.Threading.Tasks;

namespace FrameWork
{
    /// <summary>
    /// UniTask 定时器句柄，可交给 Timer.DestroyTimer 提前取消。
    /// 不使用 CancellationTokenSource.Dispose，避免微信小游戏 IL2CPP 签名异常与每定时器 CTS 分配。
    /// </summary>
    public sealed class TimeData
    {
        private bool _isInterval;
        private float _delay;
        private float _duration;
        private int _remainingCalls;
        private Action _action;
        private bool _cancelled;
        internal bool Poolable;

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
            bool isInterval, float delay, float duration, Action action, int callCount = -1)
        {
            _isInterval = isInterval;
            _delay = Math.Max(0f, delay);
            _duration = duration;
            _remainingCalls = callCount;
            _action = action;
            _cancelled = false;
            IsRunning = true;
            RunAsync().Forget();
        }

        internal void Cancel()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            _cancelled = true;
            _action = null;
        }

        private async UniTaskVoid RunAsync()
        {
            try
            {
                if (!_isInterval)
                {
                    if (!await WaitScaledAsync(_delay))
                        return;
                    if (_cancelled)
                        return;
                    _action?.Invoke();
                    return;
                }

                float elapsed = 0f;
                while (!_cancelled)
                {
                    if (_remainingCalls == 0)
                        break;

                    // 防止 0 秒循环在同一帧持续执行，至少等待到下一次 Update。
                    if (_delay <= 0f)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        if (_cancelled)
                            break;
                        elapsed += UnityEngine.Time.deltaTime;
                    }
                    else
                    {
                        if (!await WaitScaledAsync(_delay))
                            break;
                        elapsed += _delay;
                    }

                    if (_duration >= 0f && elapsed > _duration)
                        break;

                    if (_cancelled)
                        break;

                    _action?.Invoke();

                    if (_remainingCalls > 0)
                        _remainingCalls--;

                    if (_remainingCalls == 0 ||
                        (_duration >= 0f && elapsed >= _duration))
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // 主动销毁定时器属于正常流程，不需要输出异常。
            }
            finally
            {
                IsRunning = false;
                _action = null;
                Timer.OnTimerFinished(this);
                if (Poolable)
                {
                    Key = null;
                    Poolable = false;
                    Timer.ReturnPooled(this);
                }
            }
        }

        private async UniTask<bool> WaitScaledAsync(float seconds)
        {
            if (seconds <= 0f)
                return !_cancelled;

            float remaining = seconds;
            while (remaining > 0f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (_cancelled)
                    return false;
                remaining -= UnityEngine.Time.deltaTime;
            }

            return !_cancelled;
        }
    }
}
