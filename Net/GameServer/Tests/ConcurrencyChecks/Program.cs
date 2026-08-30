using WebSocketDemo;
static void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}
var gates = new AccountLoginGate();
var first = await gates.AcquireAsync("a");
var same = gates.AcquireAsync("a");
using (await gates.AcquireAsync("b").WaitAsync(TimeSpan.FromSeconds(2)))
    Check(!same.IsCompleted, "different accounts independent; same account waits");
using var cancel = new CancellationTokenSource();
var canceled = gates.AcquireAsync("a", cancel.Token);
cancel.Cancel();
try { await canceled; throw new Exception("expected cancellation"); }
catch (OperationCanceledException) { Check(true, "canceled waiter safely removed"); }
first.Dispose(); first.Dispose();
using (await same.WaitAsync(TimeSpan.FromSeconds(2))) { }
using (await gates.AcquireAsync("a").WaitAsync(TimeSpan.FromSeconds(2)))
    Check(true, "gate reuse and duplicate disposal safe");

var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var seen = new List<int>();
int errors = 0;
var queries = new SerialQueryQueue(_ => Interlocked.Increment(ref errors));
for (int i = 0; i < 8; i++)
{
    int index = i;
    Check(queries.TryEnqueue(1, async () => { await release.Task; seen.Add(index); }), "query admitted");
}
Check(!queries.TryEnqueue(1, () => Task.CompletedTask), "query backlog bounded");
Task drain = queries.DrainAsync();
Check(!drain.IsCompleted, "identity switch barrier waits for outstanding reads");
var independent = new SerialQueryQueue(_ => { });
Check(independent.TryEnqueue(1, () => Task.CompletedTask), "other session progresses");
await independent.DrainAsync().WaitAsync(TimeSpan.FromSeconds(2));
release.SetResult();
await drain.WaitAsync(TimeSpan.FromSeconds(2));
Check(seen.SequenceEqual(Enumerable.Range(0, 8)), "query FIFO order");
Check(!queries.TryEnqueue(3 * 1024 * 1024, () => Task.CompletedTask), "query byte budget");
queries.TryEnqueue(1, () => throw new Exception("injected"));
queries.TryEnqueue(1, () => Task.CompletedTask);
await queries.DrainAsync().WaitAsync(TimeSpan.FromSeconds(2));
Check(errors == 1, "failed read does not wedge queue");
