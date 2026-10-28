namespace WebSocketDemo;

/// <summary>空间粗筛；调用方仍需执行精确距离、在线状态及房间权限判断。</summary>
public sealed class SpatialInterestIndex<T> where T : notnull
{
    private readonly record struct Cell(string Scope, int X, int Y, int Z);
    private readonly object _gate = new();
    private readonly Dictionary<Cell, HashSet<T>> _cells = new();
    private readonly Dictionary<T, Cell> _locations = new();
    private readonly double _cellSize;
    public SpatialInterestIndex(float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
        _cellSize = cellSize;
    }
    public int Count { get { lock (_gate) return _locations.Count; } }
    public int CellCount { get { lock (_gate) return _cells.Count; } }

    private Cell Locate(string scope, float x, float y, float z) =>
        new(scope, Coordinate(x), Coordinate(y), Coordinate(z));
    private int Coordinate(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        // 留出邻居坐标空间，防止极大有限坐标导致整数溢出。
        return (int)Math.Clamp(Math.Floor(value / _cellSize), int.MinValue + 1d, int.MaxValue - 1d);
    }
    public void Update(T item, string scope, float x, float y, float z)
    {
        var cell = Locate(scope, x, y, z);
        lock (_gate)
        {
            if (_locations.TryGetValue(item, out var previous))
            {
                if (previous == cell) return;
                RemoveFromCell(item, previous);
            }
            if (!_cells.TryGetValue(cell, out var entries)) _cells.Add(cell, entries = new HashSet<T>());
            entries.Add(item);
            _locations[item] = cell;
        }
    }
    public void Remove(T item)
    {
        lock (_gate)
            if (_locations.Remove(item, out var cell)) RemoveFromCell(item, cell);
    }
    private void RemoveFromCell(T item, Cell cell)
    {
        if (!_cells.TryGetValue(cell, out var entries)) return;
        entries.Remove(item);
        if (entries.Count == 0) _cells.Remove(cell);
    }
    /// <summary>收集一格半径内候选；适用于精确可见半径等于 cellSize 的场景。</summary>
    public void CollectNearby(string scope, float x, float y, float z, ISet<T> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var center = Locate(scope, x, y, z);
        lock (_gate)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (_cells.TryGetValue(new Cell(scope, center.X + dx, center.Y + dy, center.Z + dz), out var entries))
                            foreach (var item in entries) destination.Add(item);
    }
    public void Clear() { lock (_gate) { _cells.Clear(); _locations.Clear(); } }
}
