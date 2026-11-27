using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 通用 2D (XY) / 3D (XZ) 双域（地面 + 空中层）寻路网格。
    /// 支持地面单位与 2D/2.5D 飞行单位同图寻路（飞行单位可跨越水体、低矮障碍与地面动态阻挡，仅受高空/禁飞区阻挡）。
    /// </summary>
    public sealed class PathGrid : IDisposable
    {
        private const uint SerializationMagic = 0x32475046u; // "FPG2"
        private const int SerializationVersion = 2;

        private NativeArray<GridCell> _cells;
        private NativeArray<float> _heights;
        private int _nonZeroPenaltyCount;
        private bool _disposed;

        internal Action<PathGrid> BeforeMutate;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public float CellSize { get; private set; }
        public Vector3 Origin { get; private set; }
        public GridPlane Plane { get; private set; }
        public int Version { get; private set; }

        public int CellCount => Width * Height;
        public bool IsCreated => !_disposed && _cells.IsCreated;
        public bool HasPenalties => _nonZeroPenaltyCount > 0;
        public bool HasHeights => _heights.IsCreated && _heights.Length == CellCount;

        public PathGrid(
            int width,
            int height,
            float cellSize,
            Vector3 origin = default,
            GridPlane plane = GridPlane.XZ,
            bool defaultWalkable = true,
            bool allocateHeights = false)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0.0001f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;
            Plane = plane;

            AllocateBuffers(defaultWalkable, allocateHeights);
            Version = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PathGridData AsReadOnlyData()
        {
            return new PathGridData
            {
                Cells = _cells,
                Heights = _heights,
                Width = Width,
                Height = Height,
                CellSize = CellSize,
                InvCellSize = 1f / CellSize,
                Origin = Origin,
                Plane = Plane,
                HasPenalties = (byte)(_nonZeroPenaltyCount > 0 ? 1 : 0)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrepareWrite()
        {
            if (_disposed || !_cells.IsCreated)
                throw new ObjectDisposedException(nameof(PathGrid));
            BeforeMutate?.Invoke(this);
        }

        private void AllocateBuffers(bool defaultWalkable, bool allocateHeights)
        {
            int total = Width * Height;
            _cells = new NativeArray<GridCell>(total, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (allocateHeights)
            {
                _heights = new NativeArray<float>(total, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < total; i++) _heights[i] = Origin.y;
            }

            var cell = new GridCell
            {
                BaseWalkable = defaultWalkable,
                AirBaseWalkable = true
            };
            for (int i = 0; i < total; i++) _cells[i] = cell;
        }

        public void UpdateTransform(Vector3 origin, float cellSize, GridPlane plane)
        {
            if (cellSize <= 0.0001f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            PrepareWrite();
            Origin = origin;
            CellSize = cellSize;
            Plane = plane;
            Version++;
        }

        public void Resize(int width, int height, bool defaultWalkable = true, bool allocateHeights = false)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            PrepareWrite();

            if (_cells.IsCreated) _cells.Dispose();
            if (_heights.IsCreated) _heights.Dispose();
            Width = width;
            Height = height;
            _nonZeroPenaltyCount = 0;

            AllocateBuffers(defaultWalkable, allocateHeights);
            Version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int2 cell) => (uint)cell.x < (uint)Width && (uint)cell.y < (uint)Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 WorldToCell(Vector3 worldPos)
        {
            float inv = 1f / CellSize;
            int x = Mathf.FloorToInt((worldPos.x - Origin.x) * inv);
            int y = Plane == GridPlane.XZ
                ? Mathf.FloorToInt((worldPos.z - Origin.z) * inv)
                : Mathf.FloorToInt((worldPos.y - Origin.y) * inv);
            return new int2(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 CellToWorld(int x, int y, MovementDomain domain = MovementDomain.Ground, float altitudeOffset = 0f)
        {
            float cx = Origin.x + (x + 0.5f) * CellSize;
            if (Plane == GridPlane.XZ)
            {
                int idx = y * Width + x;
                float wy = domain == MovementDomain.Ground && _heights.IsCreated && (uint)idx < (uint)_heights.Length
                    ? _heights[idx]
                    : Origin.y;
                return new Vector3(cx, wy + altitudeOffset, Origin.z + (y + 0.5f) * CellSize);
            }
            return new Vector3(cx, Origin.y + (y + 0.5f) * CellSize, Origin.z + altitudeOffset);
        }

        public GridCell GetCell(int x, int y)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException($"({x}, {y})");
            return _cells[y * Width + x];
        }

        public bool IsWalkable(int x, int y, uint walkableTagMask = uint.MaxValue, MovementDomain domain = MovementDomain.Ground)
        {
            if (!InBounds(x, y)) return false;
            return _cells[y * Width + x].IsWalkable(walkableTagMask, domain);
        }

        public void SetWalkable(int x, int y, bool walkable, MovementDomain domain = MovementDomain.Ground)
        {
            if (!InBounds(x, y)) return;
            PrepareWrite();
            int idx = y * Width + x;
            GridCell cell = _cells[idx];
            if (domain == MovementDomain.Air)
            {
                if (cell.AirBaseWalkable == walkable) return;
                cell.AirBaseWalkable = walkable;
            }
            else
            {
                if (cell.BaseWalkable == walkable) return;
                cell.BaseWalkable = walkable;
            }
            _cells[idx] = cell;
            Version++;
        }

        public void SetAirWalkable(int x, int y, bool airWalkable) => SetWalkable(x, y, airWalkable, MovementDomain.Air);

        public void SetRectWalkable(int minX, int minY, int maxX, int maxY, bool walkable, MovementDomain domain = MovementDomain.Ground)
        {
            ClampRect(ref minX, ref minY, ref maxX, ref maxY);
            if (minX > maxX || minY > maxY) return;
            PrepareWrite();
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * Width;
                for (int x = minX; x <= maxX; x++)
                {
                    GridCell cell = _cells[row + x];
                    if (domain == MovementDomain.Air) cell.AirBaseWalkable = walkable;
                    else cell.BaseWalkable = walkable;
                    _cells[row + x] = cell;
                }
            }
            Version++;
        }

        public void SetPenalty(int x, int y, ushort penalty)
        {
            if (!InBounds(x, y)) return;
            PrepareWrite();
            int idx = y * Width + x;
            GridCell cell = _cells[idx];
            if (cell.Penalty == penalty) return;
            if (cell.Penalty == 0 && penalty > 0) _nonZeroPenaltyCount++;
            else if (cell.Penalty > 0 && penalty == 0) _nonZeroPenaltyCount--;
            cell.Penalty = penalty;
            _cells[idx] = cell;
            Version++;
        }

        public void SetTag(int x, int y, int tag)
        {
            if (!InBounds(x, y)) return;
            PrepareWrite();
            int idx = y * Width + x;
            GridCell cell = _cells[idx];
            cell.Tag = tag;
            _cells[idx] = cell;
            Version++;
        }

        public void AddDynamicObstacleRect(int minX, int minY, int maxX, int maxY, bool blocksGround = true, bool blocksAir = false)
        {
            if (!blocksGround && !blocksAir) return;
            ClampRect(ref minX, ref minY, ref maxX, ref maxY);
            if (minX > maxX || minY > maxY) return;
            PrepareWrite();
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * Width;
                for (int x = minX; x <= maxX; x++)
                {
                    GridCell cell = _cells[row + x];
                    if (blocksGround && cell.GroundDynamicCount < byte.MaxValue) cell.GroundDynamicCount++;
                    if (blocksAir && cell.AirDynamicCount < byte.MaxValue) cell.AirDynamicCount++;
                    _cells[row + x] = cell;
                }
            }
            Version++;
        }

        public void RemoveDynamicObstacleRect(int minX, int minY, int maxX, int maxY, bool blocksGround = true, bool blocksAir = false)
        {
            if (!blocksGround && !blocksAir) return;
            ClampRect(ref minX, ref minY, ref maxX, ref maxY);
            if (minX > maxX || minY > maxY) return;
            PrepareWrite();
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * Width;
                for (int x = minX; x <= maxX; x++)
                {
                    GridCell cell = _cells[row + x];
                    if (blocksGround && cell.GroundDynamicCount > 0) cell.GroundDynamicCount--;
                    if (blocksAir && cell.AirDynamicCount > 0) cell.AirDynamicCount--;
                    _cells[row + x] = cell;
                }
            }
            Version++;
        }

        public void WorldBoundsToCellRect(Bounds worldBounds, float extraRadius, out int minX, out int minY, out int maxX, out int maxY)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            float inv = 1f / CellSize;
            minX = Mathf.FloorToInt((min.x - extraRadius - Origin.x) * inv);
            maxX = Mathf.FloorToInt((max.x + extraRadius - Origin.x) * inv);
            if (Plane == GridPlane.XZ)
            {
                minY = Mathf.FloorToInt((min.z - extraRadius - Origin.z) * inv);
                maxY = Mathf.FloorToInt((max.z + extraRadius - Origin.z) * inv);
            }
            else
            {
                minY = Mathf.FloorToInt((min.y - extraRadius - Origin.y) * inv);
                maxY = Mathf.FloorToInt((max.y + extraRadius - Origin.y) * inv);
            }
        }

        /// <summary>
        /// 使用 Physics2D 同时烘焙地面层与空中禁飞层（airObstacleMask 为 0 时空中全畅通）。
        /// </summary>
        public void BakePhysics2D(LayerMask groundObstacleMask, float agentRadius = 0f, LayerMask airObstacleMask = default, float airAgentRadius = 0f)
        {
            PrepareWrite();
            var groundFilter = new ContactFilter2D { useLayerMask = true, layerMask = groundObstacleMask, useTriggers = false };
            var airFilter = new ContactFilter2D { useLayerMask = true, layerMask = airObstacleMask, useTriggers = false };
            bool bakeAir = airObstacleMask.value != 0;
            Collider2D[] hitBuffer = new Collider2D[1];

            float groundSize = Mathf.Max(0.01f, CellSize + agentRadius * 2f);
            float airSize = Mathf.Max(0.01f, CellSize + airAgentRadius * 2f);
            Vector2 groundBox = new Vector2(groundSize, groundSize);
            Vector2 airBox = new Vector2(airSize, airSize);

            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                float wy = Origin.y + (y + 0.5f) * CellSize;
                for (int x = 0; x < Width; x++)
                {
                    Vector2 center = new Vector2(Origin.x + (x + 0.5f) * CellSize, wy);
                    GridCell cell = _cells[row + x];
                    cell.BaseWalkable = groundObstacleMask.value == 0 || Physics2D.OverlapBox(center, groundBox, 0f, groundFilter, hitBuffer) == 0;
                    cell.AirBaseWalkable = !bakeAir || Physics2D.OverlapBox(center, airBox, 0f, airFilter, hitBuffer) == 0;
                    _cells[row + x] = cell;
                }
            }
            Version++;
        }

        /// <summary>
        /// 使用 Physics 烘焙 3D XZ 地面可行走性、地形高度及巡航高度层的空中可飞行性。
        /// </summary>
        public void BakePhysics3D(
            LayerMask obstacleMask,
            LayerMask groundMask,
            float agentRadius = 0.35f,
            float agentHeight = 1.8f,
            float raycastTopOffset = 50f,
            float raycastMaxDistance = 100f,
            float maxSlopeAngle = 45f,
            LayerMask airObstacleMask = default,
            float flightCruiseAltitude = 8f,
            float airAgentRadius = 0.5f)
        {
            PrepareWrite();
            bool checkGround = groundMask.value != 0;
            if (checkGround) EnsureHeightsAllocated();

            float radius = Mathf.Max(0.05f, agentRadius > 0f ? agentRadius : CellSize * 0.45f);
            float height = Mathf.Max(radius * 2f + 0.05f, agentHeight);
            float flyRadius = Mathf.Max(0.05f, airAgentRadius > 0f ? airAgentRadius : radius);
            bool checkAir = airObstacleMask.value != 0;

            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                float wz = Origin.z + (y + 0.5f) * CellSize;
                for (int x = 0; x < Width; x++)
                {
                    float wx = Origin.x + (x + 0.5f) * CellSize;
                    float groundY = Origin.y;
                    bool walkable = true;

                    if (checkGround)
                    {
                        Vector3 rayStart = new Vector3(wx, Origin.y + raycastTopOffset, wz);
                        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
                        {
                            groundY = hit.point.y;
                            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle) walkable = false;
                        }
                        else walkable = false;
                        _heights[row + x] = groundY;
                    }

                    if (walkable && obstacleMask.value != 0)
                    {
                        Vector3 p0 = new Vector3(wx, groundY + radius + 0.05f, wz);
                        Vector3 p1 = new Vector3(wx, groundY + height - radius, wz);
                        if (Physics.CheckCapsule(p0, p1, radius, obstacleMask, QueryTriggerInteraction.Ignore))
                            walkable = false;
                    }

                    bool airWalkable = true;
                    if (checkAir)
                    {
                        Vector3 airPos = new Vector3(wx, Origin.y + flightCruiseAltitude, wz);
                        airWalkable = !Physics.CheckSphere(airPos, flyRadius, airObstacleMask, QueryTriggerInteraction.Ignore);
                    }

                    GridCell cell = _cells[row + x];
                    cell.BaseWalkable = walkable;
                    cell.AirBaseWalkable = airWalkable;
                    _cells[row + x] = cell;
                }
            }
            Version++;
        }

        public byte[] ExportBytes()
        {
            if (!IsCreated) throw new ObjectDisposedException(nameof(PathGrid));
            bool exportHeights = HasHeights;
            using var ms = new MemoryStream(64 + CellCount * (exportHeights ? 7 : 3));
            using var bw = new BinaryWriter(ms);
            bw.Write(SerializationMagic);
            bw.Write(SerializationVersion);
            bw.Write(Width);
            bw.Write(Height);
            bw.Write(CellSize);
            bw.Write(Origin.x);
            bw.Write(Origin.y);
            bw.Write(Origin.z);
            bw.Write((byte)Plane);
            bw.Write(exportHeights);

            int total = CellCount;
            for (int i = 0; i < total; i++)
            {
                GridCell c = _cells[i];
                bw.Write(c.Flags);
                bw.Write(c.Penalty);
            }
            if (exportHeights)
            {
                for (int i = 0; i < total; i++) bw.Write(_heights[i]);
            }
            return ms.ToArray();
        }

        public static PathGrid ImportBytes(byte[] data)
        {
            if (data == null || data.Length < 32) throw new ArgumentException("无效的网格二进制数据。", nameof(data));
            using var ms = new MemoryStream(data, false);
            using var br = new BinaryReader(ms);
            if (br.ReadUInt32() != SerializationMagic || br.ReadInt32() != SerializationVersion)
                throw new InvalidDataException("网格数据版本不匹配。");

            int width = br.ReadInt32();
            int height = br.ReadInt32();
            float cellSize = br.ReadSingle();
            Vector3 origin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            GridPlane plane = (GridPlane)br.ReadByte();
            bool hasHeights = br.ReadBoolean();

            var grid = new PathGrid(width, height, cellSize, origin, plane, defaultWalkable: false, allocateHeights: hasHeights);
            int total = width * height;
            int penalties = 0;
            for (int i = 0; i < total; i++)
            {
                byte flags = br.ReadByte();
                ushort penalty = br.ReadUInt16();
                if (penalty > 0) penalties++;
                grid._cells[i] = new GridCell { Flags = flags, Penalty = penalty };
            }
            grid._nonZeroPenaltyCount = penalties;
            if (hasHeights)
            {
                for (int i = 0; i < total; i++) grid._heights[i] = br.ReadSingle();
            }
            return grid;
        }

        private void EnsureHeightsAllocated()
        {
            int total = CellCount;
            if (_heights.IsCreated && _heights.Length == total) return;
            if (_heights.IsCreated) _heights.Dispose();
            _heights = new NativeArray<float>(total, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < total; i++) _heights[i] = Origin.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClampRect(ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            if (minX > maxX) (minX, maxX) = (maxX, minX);
            if (minY > maxY) (minY, maxY) = (maxY, minY);
            minX = Mathf.Max(0, minX);
            minY = Mathf.Max(0, minY);
            maxX = Mathf.Min(Width - 1, maxX);
            maxY = Mathf.Min(Height - 1, maxY);
        }

        public void Dispose()
        {
            if (_disposed) return;
            BeforeMutate?.Invoke(this);
            _disposed = true;
            if (_cells.IsCreated) _cells.Dispose();
            if (_heights.IsCreated) _heights.Dispose();
        }
    }
}
