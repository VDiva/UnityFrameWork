namespace WebSocketDemo;

/// <summary>仅用于无业务写入的查询，单连接串行、有界，不阻塞实时消息接收。</summary>
public sealed class SerialQueryQueue
{
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;
    private int _count, _bytes;
    private readonly Action<Exception> _failed;
    public SerialQueryQueue(Action<Exception> failed) => _failed = failed;

    public bool TryEnqueue(int bytes, Func<Task> action)
    {
        lock (_gate)
        {
            if (_count >= 8 || bytes > 2 * 1024 * 1024 - _bytes) return false;
            ++_count;
            _bytes += bytes;
            Task previous = _tail;
            _tail = Task.Run(async () =>
            {
                try { await previous; await action(); }
                catch (Exception exception) { _failed(exception); }
                finally { lock (_gate) { --_count; _bytes -= bytes; } }
            });
            return true;
        }
    }
    public Task DrainAsync() { lock (_gate) return _tail; }
}
