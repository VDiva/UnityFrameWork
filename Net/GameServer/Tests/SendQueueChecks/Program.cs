using System.Collections.Concurrent;
using System.Net.WebSockets;
using WebSocketDemo;

static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
static void Check(bool ok, string name)
{
    if (!ok) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}
var started = Signal();
var release = Signal();
var done = Signal();
var values = new ConcurrentQueue<byte>();
int active = 0, maxActive = 0;
var queue = new SessionSendQueue(async (data, type, token) =>
{
    maxActive = Math.Max(maxActive, Interlocked.Increment(ref active));
    if (data[0] == 0) { started.TrySetResult(true); await release.Task.WaitAsync(token); }
    values.Enqueue(data[0]);
    Interlocked.Decrement(ref active);
    if (values.Count == 4) done.TrySetResult(true);
}, error => done.TrySetException(error));
queue.Enqueue(new byte[] {0}, WebSocketMessageType.Binary);
await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
queue.Enqueue(new byte[] {1}, WebSocketMessageType.Binary, 1);
queue.Enqueue(new byte[] {2}, WebSocketMessageType.Binary, 1);
queue.Enqueue(new byte[] {9}, WebSocketMessageType.Binary);
queue.Enqueue(new byte[] {3}, WebSocketMessageType.Binary, 1);
release.TrySetResult(true);
await done.Task.WaitAsync(TimeSpan.FromSeconds(2));
Check(values.SequenceEqual(new byte[] {0, 2, 9, 3}), "FIFO, coalescing, reliable barrier");
Check(maxActive == 1, "single socket writer");
queue.Stop(); await queue.Completion;

foreach (bool bytesLimit in new[] {false, true})
{
    var entered = Signal(); var failed = Signal();
    var bounded = new SessionSendQueue(async (data, type, token) =>
    { entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, token); },
        error => failed.TrySetResult(true), maxPackets: bytesLimit ? 100 : 2, maxBytes: bytesLimit ? 2 : 100);
    bounded.Enqueue(new byte[] {0}, WebSocketMessageType.Binary);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    bounded.Enqueue(new byte[] {1}, WebSocketMessageType.Binary);
    bounded.Enqueue(new byte[] {2}, WebSocketMessageType.Binary);
    Check(!bounded.Enqueue(new byte[] {3}, WebSocketMessageType.Binary), "reject overflow: " + bytesLimit);
    await failed.Task.WaitAsync(TimeSpan.FromSeconds(2)); await bounded.Completion;
}
var timedOut = Signal();
// 模拟带宽暂时下降：待发实时包应被可靠控制消息挤出，而不是踢掉玩家。
var congestionStarted = Signal(); var congestionRelease = Signal(); var congestionDone = Signal();
var congestionValues = new ConcurrentQueue<byte>();
bool congestionFailed = false;
var congestion = new SessionSendQueue(async (data, type, token) =>
{
    if (data[0] == 0) { congestionStarted.TrySetResult(true); await congestionRelease.Task.WaitAsync(token); }
    congestionValues.Enqueue(data[0]);
    if (data[0] == 9) congestionDone.TrySetResult(true);
}, error => congestionFailed = true, maxPackets: 2, maxBytes: 2);
congestion.Enqueue(new byte[] {0}, WebSocketMessageType.Binary);
await congestionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
congestion.Enqueue(new byte[] {1}, WebSocketMessageType.Binary, 1);
congestion.Enqueue(new byte[] {2}, WebSocketMessageType.Binary, 2);
Check(congestion.Enqueue(new byte[] {8}, WebSocketMessageType.Binary), "control evicts stale realtime");
Check(congestion.Enqueue(new byte[] {9}, WebSocketMessageType.Binary), "reliable FIFO survives congestion");
Check(!congestion.Enqueue(new byte[] {3}, WebSocketMessageType.Binary, 3), "realtime drops when reliable queue is full");
congestionRelease.TrySetResult(true);
await congestionDone.Task.WaitAsync(TimeSpan.FromSeconds(2));
Check(!congestionFailed && congestionValues.SequenceEqual(new byte[] {0, 8, 9}), "congestion does not disconnect healthy session");
congestion.Stop(); await congestion.Completion;

var stalled = new SessionSendQueue(async (data, type, token) => await Task.Delay(Timeout.Infinite, token),
    error => timedOut.TrySetResult(true), timeout: TimeSpan.FromMilliseconds(50));
stalled.Enqueue(new byte[] {0}, WebSocketMessageType.Binary);
await timedOut.Task.WaitAsync(TimeSpan.FromSeconds(2)); await stalled.Completion;
Check(true, "stalled send isolated on timeout");

var queues = new List<SessionSendQueue>();
var completions = new List<Task>();
for (int i = 0; i < 50; i++)
{
    bool slow = i == 0; var received = Signal(); int count = 0;
    var q = new SessionSendQueue(async (data, type, token) =>
    {
        if (slow) await Task.Delay(Timeout.Infinite, token);
        if (++count == 10) received.TrySetResult(true);
    }, error => received.TrySetException(error));
    queues.Add(q);
    if (!slow) completions.Add(received.Task);
    for (int n = 0; n < 10; n++) q.Enqueue(new byte[] {(byte)n}, WebSocketMessageType.Binary);
}
await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(2));
Check(true, "50 queues: stalled client does not delay 49 healthy clients");
foreach (var q in queues) q.Stop();
await Task.WhenAll(queues.Select(q => q.Completion));
