using System.Net.WebSockets;

namespace WebSocketDemo;

/// <summary>单连接有界发送队列。可靠包保持 FIFO，实时包仅在相邻可靠包之间合并。</summary>
public sealed class SessionSendQueue
{
    private sealed record Packet(byte[] Data, WebSocketMessageType Type, ulong? Key, long QueuedAt);
    private readonly object _gate = new();
    private readonly LinkedList<Packet> _packets = new();
    private readonly Dictionary<ulong, LinkedListNode<Packet>> _latest = new();
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<byte[], WebSocketMessageType, CancellationToken, Task> _send;
    private readonly Action<Exception> _failed;
    private readonly int _maxPackets;
    private readonly int _maxBytes;
    private readonly TimeSpan _timeout;
    private int _bytes;
    private bool _closed;
    private long _lastSlowLog;
    private readonly string _label;
    public Task Completion { get; }

    public SessionSendQueue(Func<byte[], WebSocketMessageType, CancellationToken, Task> send,
        Action<Exception> failed, int maxPackets = 1024, int maxBytes = 4 * 1024 * 1024,
        TimeSpan? timeout = null, string label = "session")
    {
        _send = send;
        _failed = failed;
        _maxPackets = maxPackets;
        _maxBytes = maxBytes;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _label = label;
        Completion = Task.Run(RunAsync);
    }

    public bool Enqueue(byte[] data, WebSocketMessageType type, ulong? key = null)
    {
        bool overflow;
        lock (_gate)
        {
            if (_closed) return false;
            _latest.TryGetValue(key ?? 0, out var previous);
            if (!key.HasValue) previous = null;
            int nextBytes = _bytes + data.Length - (previous?.Value.Data.Length ?? 0);
            // 弱网先牺牲可丢帧状态，不让它挤掉房间/RPC 等可靠消息。
            // 可以丢弃旧状态，但不能移动可靠包或把新状态合并到可靠包之前。
            if (nextBytes > _maxBytes || (previous == null && _packets.Count >= _maxPackets))
            {
                var node = _packets.First;
                while (node != null &&
                    (nextBytes > _maxBytes || (previous == null && _packets.Count >= _maxPackets)))
                {
                    var next = node.Next;
                    if (node.Value.Key.HasValue && !ReferenceEquals(node, previous))
                    {
                        if (_latest.TryGetValue(node.Value.Key.Value, out var indexed) &&
                            ReferenceEquals(indexed, node)) _latest.Remove(node.Value.Key.Value);
                        _bytes -= node.Value.Data.Length;
                        _packets.Remove(node);
                        nextBytes = _bytes + data.Length - (previous?.Value.Data.Length ?? 0);
                    }
                    node = next;
                }
            }
            overflow = nextBytes > _maxBytes || (previous == null && _packets.Count >= _maxPackets);
            // 单个实时包放不下或队列全部是可靠包时，跳过这帧即可，不中止连接。
            if (overflow && key.HasValue) return false;
            if (!overflow)
            {
                var packet = new Packet(data, type, key, Environment.TickCount64);
                if (previous != null) previous.Value = packet;
                else
                {
                    // 不跨越生成、销毁、场景、RPC 等可靠消息替换状态，避免时间倒序。
                    if (!key.HasValue) _latest.Clear();
                    var node = _packets.AddLast(packet);
                    if (key.HasValue) _latest[key.Value] = node;
                }
                _bytes = nextBytes;
                // 所有生产者均持有 _gate；消费者只取信号，无需用异常判断已唤醒。
                if (_signal.CurrentCount == 0) _signal.Release();
                return true;
            }
            // 超出预算时不静默丢可靠包；隔离慢连接，由客户端重连重建状态。
            _closed = true;
            _packets.Clear();
            _latest.Clear();
            _bytes = 0;
        }
        _stop.Cancel();
        _failed(new IOException("send queue exceeded packet/byte limit"));
        return false;
    }

    public void Stop()
    {
        lock (_gate)
        {
            _closed = true;
            _packets.Clear();
            _latest.Clear();
            _bytes = 0;
        }
        _stop.Cancel();
    }

    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                await _signal.WaitAsync(_stop.Token);
                while (true)
                {
                    Packet packet;
                    lock (_gate)
                    {
                        if (_closed) return;
                        var first = _packets.First;
                        if (first == null) break;
                        packet = first.Value;
                        _packets.RemoveFirst();
                        _bytes -= packet.Data.Length;
                        if (packet.Key.HasValue && _latest.TryGetValue(packet.Key.Value, out var current) &&
                            ReferenceEquals(first, current)) _latest.Remove(packet.Key.Value);
                    }
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    deadline.CancelAfter(_timeout);
                    long sendStarted = Environment.TickCount64;
                    // 发送必须支持取消；发生超时后直接中止连接，不再继续发后续可靠包。
                    await _send(packet.Data, packet.Type, deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    long finished = Environment.TickCount64;
                    if ((sendStarted - packet.QueuedAt > 500 || finished - sendStarted > 250) &&
                        finished - _lastSlowLog > 5000)
                    {
                        _lastSlowLog = finished;
                        Console.WriteLine($"[SendQueue] {_label}: queuedMs={sendStarted - packet.QueuedAt}, writeMs={finished - sendStarted}, bytes={packet.Data.Length}");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Stop();
            _failed(exception);
        }
    }
}
