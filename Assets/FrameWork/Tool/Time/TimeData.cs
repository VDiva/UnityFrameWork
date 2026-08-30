using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FrameWork
{
    /// <summary>
    /// UniTask 定时器句柄，可交给 Timer.DestroyTimer 提前取消。
    /// </summary>
    public sealed class TimeData
    {
        private bool _isInterval;
        private float _delay;
        private float _duration;
        private int _remainingCalls;
        private Action _action;
        private CancellationTokenSource _cancellation;

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
            _cancellation = new CancellationTokenSource();
            IsRunning = true;
            RunAsync(_cancellation.Token).Forget();
        }

        internal void Cancel()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            _cancellation?.Cancel();
        }

        private async UniTaskVoid RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!_isInterval)
                {
                    await DelayAsync(_delay, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    _action?.Invoke();
                    return;
                }

                float elapsed = 0f;
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_remainingCalls == 0)
                        break;

                    // 防止 0 秒循环在同一帧持续执行，至少等待到下一次 Update。
                    if (_delay <= 0f)
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    else
                        await DelayAsync(_delay, cancellationToken);

                    elapsed += _delay <= 0f ? UnityEngine.Time.deltaTime : _delay;
                    if (_duration >= 0f && elapsed > _duration)
                        break;

                    cancellationToken.ThrowIfCancellationRequested();
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
                Timer.OnTimerFinished(this);
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }

        private static UniTask DelayAsync(float seconds, CancellationToken cancellationToken)
        {
            return UniTask.Delay(
                TimeSpan.FromSeconds(seconds),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                cancellationToken);
        }
    }
}
