using System.Net.WebSockets;

namespace WebSocketDemo;

/// <summary>排队期间保持不可变；ByteCount 必须等于编码长度，MergeFrom 不得修改旧载荷。</summary>
public interface ISessionPacketPayload
{
    int ByteCount { get; }
    byte[] GetBytes();
    ISessionPacketPayload MergeFrom(ISessionPacketPayload previous);
}

/// <summary>单连接有界发送队列。可靠包保持 FIFO，实时包仅在相邻可靠包之间合并。</summary>
public sealed class SessionSendQueue
{
    private sealed record BytePayload(byte[] Data, Func<byte[], byte[], byte[]>? Merge) : ISessionPacketPayload
    {
        public int ByteCount => Data.Length;
        public byte[] GetBytes() => Data;
        public ISessionPacketPayload MergeFrom(ISessionPacketPayload previous) =>
            Merge == null ? this : new BytePayload(Merge(previous.GetBytes(), Data), Merge);
    }
    private sealed record Packet(ISessionPacketPayload Payload, WebSocketMessageType Type, ulong? Key,
        long QueuedAt, int MaxAgeMs);
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
    private int _reliablePackets;
    private readonly int _realtimeIntervalMs;
    private long _nextRealtimeFlush;
    private long _coalescedPackets;
    private long _sentPackets;
    private long _lastQueueWaitMs;
    private long _lastWriteMs;
    private long _expiredRealtimePackets;
    private bool _closed;
    private long _lastSlowLog;
    private readonly string _label;
    public long CoalescedPackets => Interlocked.Read(ref _coalescedPackets);
    public long SentPackets => Interlocked.Read(ref _sentPackets);
    public Task Completion { get; }
    public int PendingCount { get { lock (_gate) return _packets.Count; } }
    public int PendingBytes { get { lock (_gate) return _bytes; } }
    public long LastQueueWaitMs => Interlocked.Read(ref _lastQueueWaitMs);
    public long LastWriteMs => Interlocked.Read(ref _lastWriteMs);
    public long ExpiredRealtimePackets => Interlocked.Read(ref _expiredRealtimePackets);

    public SessionSendQueue(Func<byte[], WebSocketMessageType, CancellationToken, Task> send,
        Action<Exception> failed, int maxPackets = 1024, int maxBytes = 4 * 1024 * 1024,
        TimeSpan? timeout = null, string label = "session", TimeSpan? realtimeInterval = null)
    {
        if (maxPackets <= 0 || maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxPackets));
        var interval = realtimeInterval ?? TimeSpan.Zero;
        if (interval < TimeSpan.Zero || interval.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(realtimeInterval));
        _realtimeIntervalMs = (int)Math.Ceiling(interval.TotalMilliseconds);
        _nextRealtimeFlush = Environment.TickCount64 + _realtimeIntervalMs;
        _send = send;
        _failed = failed;
        _maxPackets = maxPackets;
        _maxBytes = maxBytes;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _label = label;
        Completion = Task.Run(RunAsync);
    }

    public bool Enqueue(byte[] data, WebSocketMessageType type, ulong? key = null,
        Func<byte[], byte[], byte[]>? merge = null, int maxAgeMs = 0)
    {
        return Enqueue(new BytePayload(data ?? throw new ArgumentNullException(nameof(data)), merge), type, key, maxAgeMs);
    }

    public bool Enqueue(ISessionPacketPayload payload, WebSocketMessageType type, ulong? key = null, int maxAgeMs = 0)
    {
        ArgumentNullException.ThrowIfNull(payload);
        bool overflow;
        lock (_gate)
        {
            if (_closed) return false;
            _latest.TryGetValue(key ?? 0, out var previous);
            if (!key.HasValue) previous = null;
            // 同一个实时 key 的批量状态需要按对象合并；直接覆盖整包会漏掉未在新包出现的对象。
            if (previous != null) payload = payload.MergeFrom(previous.Value.Payload);
            if (payload.ByteCount < 0) throw new ArgumentOutOfRangeException(nameof(payload));
            long nextBytes = (long)_bytes + payload.ByteCount - (previous?.Value.Payload.ByteCount ?? 0);
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
                        _bytes -= node.Value.Payload.ByteCount;
                        _packets.Remove(node);
                        nextBytes = (long)_bytes + payload.ByteCount - (previous?.Value.Payload.ByteCount ?? 0);
                    }
                    node = next;
                }
            }
            overflow = nextBytes > _maxBytes || (previous == null && _packets.Count >= _maxPackets);
            // 单个实时包放不下或队列全部是可靠包时，跳过这帧即可，不中止连接。
            if (overflow && key.HasValue) return false;
            if (!overflow)
            {
                var packet = new Packet(payload, type, key, Environment.TickCount64, maxAgeMs);
                if (previous != null)
                {
                    previous.Value = packet;
                    Interlocked.Increment(ref _coalescedPackets);
                }
                else
                {
                    // 不跨越生成、销毁、场景、RPC 等可靠消息替换状态，避免时间倒序。
                    if (!key.HasValue) { _latest.Clear(); _reliablePackets++; }
                    var node = _packets.AddLast(packet);
                    if (key.HasValue) _latest[key.Value] = node;
                }
                _bytes = (int)nextBytes;
                // 空队列首次入队才唤醒；实时覆盖不打断发送窗口，避免每次更新创建一次等待。
                // 可靠消息始终可以唤醒窗口等待，保持控制消息低延迟。
                if ((!key.HasValue || previous == null && _packets.Count == 1) && _signal.CurrentCount == 0)
                    _signal.Release();
                return true;
            }
            // 超出预算时不静默丢可靠包；隔离慢连接，由客户端重连重建状态。
            _closed = true;
            _packets.Clear();
            _latest.Clear();
            _bytes = 0;
            _reliablePackets = 0;
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
            _reliablePackets = 0;
        }
        _stop.Cancel();
    }

    private async Task RunAsync()
    {
        try
        {
            // 一个连接只允许一个发送者，超时令牌可以复用；避免每个高频位置包
            // 都分配 linked CTS 和定时器。超时一旦触发，连接会被隔离，不再复用。
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            while (true)
            {
                await _signal.WaitAsync(_stop.Token);
                bool sentRealtime = false;
                while (true)
                {
                    Packet? packet = null;
                    int waitMs = 0;
                    lock (_gate)
                    {
                        if (_closed) return;
                        var first = _packets.First;
                        if (first == null)
                        {
                            if (sentRealtime) _nextRealtimeFlush = Environment.TickCount64 + _realtimeIntervalMs;
                            break;
                        }
                        // 按发送窗口合并状态；可靠消息到达会唤醒等待并立即放行前序状态。
                        // 不逐个对象限速，同一窗口中不同 key 的状态都能发送。
                        long remaining = _nextRealtimeFlush - Environment.TickCount64;
                        if (!sentRealtime && first.Value.Key.HasValue && _reliablePackets == 0 && remaining > 0)
                            waitMs = (int)Math.Min(remaining, int.MaxValue);
                        else
                        {
                            packet = first.Value;
                            _packets.RemoveFirst();
                            _bytes -= packet.Payload.ByteCount;
                            if (!packet.Key.HasValue) _reliablePackets--;
                            if (packet.Key.HasValue && _latest.TryGetValue(packet.Key.Value, out var current) &&
                                ReferenceEquals(first, current)) _latest.Remove(packet.Key.Value);
                        }
                    }
                    if (waitMs > 0)
                    {
                        await _signal.WaitAsync(waitMs, _stop.Token);
                        continue;
                    }
                    if (packet == null) continue;
                    // Transform 是可丢帧状态；过期后发送只会挡住更新的位置。
                    if (packet.MaxAgeMs > 0 &&
                        Environment.TickCount64 - packet.QueuedAt > packet.MaxAgeMs)
                    {
                        Interlocked.Increment(ref _expiredRealtimePackets);
                        continue;
                    }
                    // 编码在锁外、真正发送前才执行，被覆盖或过期的快照无需编码。
                    byte[] data = packet.Payload.GetBytes();
                    if (data.Length != packet.Payload.ByteCount) throw new InvalidOperationException("packet byte count changed");
                    sentRealtime |= packet.Key.HasValue;
                    deadline.CancelAfter(_timeout);
                    long sendStarted = Environment.TickCount64;
                    // 发送必须支持取消；发生超时后直接中止连接，不再继续发后续可靠包。
                    await _send(data, packet.Type, deadline.Token);
                    Interlocked.Increment(ref _sentPackets);
                    deadline.Token.ThrowIfCancellationRequested();
                    deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                    long finished = Environment.TickCount64;
                    Interlocked.Exchange(ref _lastQueueWaitMs, sendStarted - packet.QueuedAt);
                    Interlocked.Exchange(ref _lastWriteMs, finished - sendStarted);
                    if ((sendStarted - packet.QueuedAt > 500 || finished - sendStarted > 250) &&
                        finished - _lastSlowLog > 5000)
                    {
                        _lastSlowLog = finished;
                        Console.WriteLine($"[SendQueue] {_label}: queuedMs={sendStarted - packet.QueuedAt}, writeMs={finished - sendStarted}, bytes={data.Length}");
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
