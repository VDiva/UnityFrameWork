using System.Collections.Concurrent;

namespace WebSocketDemo;

/// <summary>按 Scope 分片的空间粗筛索引；消除跨房间/跨服全局锁竞争与 27 邻格字符串哈希开销。</summary>
public sealed class SpatialInterestIndex<T> where T : notnull
{
    private readonly record struct GridCoord(int X, int Y, int Z);

    private sealed class ScopeGrid
    {
        private const int MaxPooledSets = 64;
        public readonly object Gate = new();
        public readonly Dictionary<GridCoord, HashSet<T>> Cells = new();
        private readonly Stack<HashSet<T>> _setPool = new();

        public HashSet<T> RentSet() => _setPool.Count > 0 ? _setPool.Pop() : new HashSet<T>();

        public void ReturnSet(HashSet<T> set)
        {
            set.Clear();
            if (_setPool.Count < MaxPooledSets)
                _setPool.Push(set);
        }
    }

    private readonly object _lifecycleGate = new();
    private readonly ConcurrentDictionary<string, ScopeGrid> _scopes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<T, (ScopeGrid Grid, GridCoord Coord)> _locations = new();
    private readonly double _cellSize;

    public SpatialInterestIndex(float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        _cellSize = cellSize;
    }

    public int Count => _locations.Count;

    public int CellCount
    {
        get
        {
            int total = 0;
            foreach (var pair in _scopes)
            {
                lock (pair.Value.Gate)
                    total += pair.Value.Cells.Count;
            }
            return total;
        }
    }

    private GridCoord Locate(float x, float y, float z) =>
        new(Coordinate(x), Coordinate(y), Coordinate(z));

    private int Coordinate(float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        // 留出邻居坐标空间，防止极大有限坐标导致整数溢出。
        return (int)Math.Clamp(Math.Floor(value / _cellSize), int.MinValue + 1d, int.MaxValue - 1d);
    }

    public void Update(T item, string scope, float x, float y, float z)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var coord = Locate(x, y, z);
        var targetGrid = _scopes.GetOrAdd(scope, static _ => new ScopeGrid());

        // 稳态快路径：对象已在同一 Scope 内，仅锁定当前 Scope 分片，无全局锁竞争。
        if (_locations.TryGetValue(item, out var existing) && ReferenceEquals(existing.Grid, targetGrid))
        {
            if (existing.Coord == coord)
                return;

            lock (targetGrid.Gate)
            {
                if (_locations.TryGetValue(item, out var current) && ReferenceEquals(current.Grid, targetGrid))
                {
                    if (current.Coord == coord)
                        return;
                    RemoveFromGridCore(targetGrid, item, current.Coord);
                    AddToGridCore(targetGrid, item, coord);
                    _locations[item] = (targetGrid, coord);
                    return;
                }
            }
        }

        // 首次进入或跨 Scope 迁移慢路径：按固定锁层级 (_lifecycleGate -> ScopeGrid.Gate) 串行化，杜绝死锁。
        lock (_lifecycleGate)
        {
            targetGrid = _scopes.GetOrAdd(scope, static _ => new ScopeGrid());
            if (_locations.TryGetValue(item, out var prev))
            {
                if (ReferenceEquals(prev.Grid, targetGrid) && prev.Coord == coord)
                    return;

                lock (prev.Grid.Gate)
                {
                    RemoveFromGridCore(prev.Grid, item, prev.Coord);
                }
            }

            lock (targetGrid.Gate)
            {
                AddToGridCore(targetGrid, item, coord);
                _locations[item] = (targetGrid, coord);
            }
        }
    }

    public void Remove(T item)
    {
        if (!_locations.ContainsKey(item))
            return;

        lock (_lifecycleGate)
        {
            if (_locations.TryRemove(item, out var prev))
            {
                lock (prev.Grid.Gate)
                {
                    RemoveFromGridCore(prev.Grid, item, prev.Coord);
                }
            }
        }
    }

    private static void AddToGridCore(ScopeGrid grid, T item, GridCoord coord)
    {
        if (!grid.Cells.TryGetValue(coord, out var entries))
        {
            entries = grid.RentSet();
            grid.Cells.Add(coord, entries);
        }
        entries.Add(item);
    }

    private static void RemoveFromGridCore(ScopeGrid grid, T item, GridCoord coord)
    {
        if (!grid.Cells.TryGetValue(coord, out var entries))
            return;
        entries.Remove(item);
        if (entries.Count == 0)
        {
            grid.Cells.Remove(coord);
            grid.ReturnSet(entries);
        }
    }

    /// <summary>收集一格半径内候选；适用于精确可见半径等于 cellSize 的场景。</summary>
    public void CollectNearby(string scope, float x, float y, float z, ISet<T> destination)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(destination);
        var center = Locate(x, y, z);
        if (!_scopes.TryGetValue(scope, out var grid))
            return;

        lock (grid.Gate)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = center.X + dx;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = center.Y + dy;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (grid.Cells.TryGetValue(new GridCoord(nx, ny, center.Z + dz), out var entries))
                        {
                            foreach (var item in entries)
                                destination.Add(item);
                        }
                    }
                }
            }
        }
    }

    public void Clear()
    {
        lock (_lifecycleGate)
        {
            foreach (var pair in _scopes)
            {
                lock (pair.Value.Gate)
                {
                    pair.Value.Cells.Clear();
                }
            }
            _scopes.Clear();
            _locations.Clear();
        }
    }
}
