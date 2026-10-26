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
