namespace WebSocketDemo;

/// <summary>真正按账号互斥，最后一个持有者/等待者退出后移除锁。</summary>
public sealed class AccountLoginGate
{
    private sealed class Entry
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int References;
    }
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken token = default)
    {
        Entry entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out entry!)) _entries[key] = entry = new Entry();
            entry.References++;
        }
        try { await entry.Semaphore.WaitAsync(token); }
        catch { RemoveReference(key, entry); throw; }
        return new Lease(this, key, entry);
    }

    private void RemoveReference(string key, Entry entry)
    {
        lock (_gate)
        {
            if (--entry.References == 0)
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Lease(AccountLoginGate owner, string key, Entry entry) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            entry.Semaphore.Release();
            owner.RemoveReference(key, entry);
        }
    }
}
