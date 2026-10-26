using System.Threading.Channels;

namespace WebSocketDemo;

/// <summary>
/// 每个连接只有一个业务指令执行者。接收循环只负责入队，数据库等待不会挡住实时包。
/// 房间和对象生命周期指令形成屏障，执行完毕之前不接受新位置状态。
/// </summary>
public sealed class SessionCommandQueue
{
    private sealed record Command(int Bytes, bool WorldMutation, Func<Task> Action);
    private readonly object _gate = new();
    private readonly Channel<Command> _channel;
    private readonly Action<Exception> _failed;
    private readonly int _maxCount;
    private readonly int _maxBytes;
    private int _count;
    private int _bytes;
    private int _worldMutations;
    private bool _completed;

    public bool HasPendingWorldMutation => Volatile.Read(ref _worldMutations) != 0;
    public Task Completion { get; }

    public SessionCommandQueue(Action<Exception> failed, int maxCount = 256,
        int maxBytes = 4 * 1024 * 1024)
    {
        _failed = failed;
        _maxCount = maxCount;
        _maxBytes = maxBytes;
        _channel = Channel.CreateBounded<Command>(new BoundedChannelOptions(maxCount)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        Completion = Task.Run(RunAsync);
    }

    public bool TryEnqueue(int bytes, bool worldMutation, Func<Task> action)
    {
        lock (_gate)
        {
            if (_completed || bytes < 0 || _count >= _maxCount ||
                bytes > _maxBytes - _bytes) return false;
            if (!_channel.Writer.TryWrite(new Command(bytes, worldMutation, action))) return false;
            _count++;
            _bytes += bytes;
            if (worldMutation) Interlocked.Increment(ref _worldMutations);
            return true;
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            if (_completed) return;
            _completed = true;
            _channel.Writer.TryComplete();
        }
    }

    private async Task RunAsync()
    {
        await foreach (Command command in _channel.Reader.ReadAllAsync())
        {
            try { await command.Action(); }
            catch (Exception exception) { _failed(exception); }
            finally
            {
                lock (_gate)
                {
                    _count--;
                    _bytes -= command.Bytes;
                    if (command.WorldMutation)
                        Interlocked.Decrement(ref _worldMutations);
                }
            }
        }
    }
}
