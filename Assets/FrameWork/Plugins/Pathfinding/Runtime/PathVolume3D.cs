using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 真 3D 立体空域体素网格（X-Y-Z 三维自由飞行寻路，支持飞越屋顶、穿过桥洞、三维爬升俯冲与动态空中障碍物）。
    /// </summary>
    public sealed class PathVolume3D : IDisposable
    {
        private NativeArray<VoxelCell> _voxels;
        private int _nonZeroPenaltyCount;
        private bool _disposed;

        internal Action<PathVolume3D> BeforeMutate;

        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public int SizeZ { get; private set; }
        public float VoxelSize { get; private set; }
        public Vector3 Origin { get; private set; }
        public int Version { get; private set; }

        public int VoxelCount => SizeX * SizeY * SizeZ;
        public bool IsCreated => !_disposed && _voxels.IsCreated;
        public bool HasPenalties => _nonZeroPenaltyCount > 0;

        public PathVolume3D(
            int sizeX,
            int sizeY,
            int sizeZ,
            float voxelSize,
            Vector3 origin = default,
            bool defaultFlyable = true)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                throw new ArgumentOutOfRangeException("体素尺寸必须大于 0。");
            if (voxelSize <= 0.0001f)
                throw new ArgumentOutOfRangeException(nameof(voxelSize));

            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            VoxelSize = voxelSize;
            Origin = origin;

            Allocate(defaultFlyable);
            Version = 1;
        }

        private void Allocate(bool defaultFlyable)
        {
            int total = SizeX * SizeY * SizeZ;
            _voxels = new NativeArray<VoxelCell>(total, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (defaultFlyable)
            {
                var cell = new VoxelCell { BaseFlyable = true };
                for (int i = 0; i < total; i++) _voxels[i] = cell;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PathVolume3DData AsReadOnlyData()
        {
            return new PathVolume3DData
            {
                Voxels = _voxels,
                SizeX = SizeX,
                SizeY = SizeY,
                SizeZ = SizeZ,
                SliceStride = SizeX * SizeZ,
                VoxelSize = VoxelSize,
                InvVoxelSize = 1f / VoxelSize,
                Origin = Origin,
                HasPenalties = (byte)(_nonZeroPenaltyCount > 0 ? 1 : 0)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrepareWrite()
        {
            if (_disposed || !_voxels.IsCreated)
                throw new ObjectDisposedException(nameof(PathVolume3D));
            BeforeMutate?.Invoke(this);
        }

        public void Resize(int sizeX, int sizeY, int sizeZ, float voxelSize, Vector3 origin, bool defaultFlyable = true)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                throw new ArgumentOutOfRangeException("体素尺寸必须大于 0。");
            PrepareWrite();
            if (_voxels.IsCreated) _voxels.Dispose();
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            VoxelSize = voxelSize;
            Origin = origin;
            _nonZeroPenaltyCount = 0;
            Allocate(defaultFlyable);
            Version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int x, int y, int z)
        {
            return (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToIndex(int x, int y, int z) => y * (SizeX * SizeZ) + z * SizeX + x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 WorldToVoxel(Vector3 worldPos)
        {
            float inv = 1f / VoxelSize;
            return new int3(
                Mathf.FloorToInt((worldPos.x - Origin.x) * inv),
                Mathf.FloorToInt((worldPos.y - Origin.y) * inv),
                Mathf.FloorToInt((worldPos.z - Origin.z) * inv));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 VoxelToWorld(int x, int y, int z)
        {
            return new Vector3(
                Origin.x + (x + 0.5f) * VoxelSize,
                Origin.y + (y + 0.5f) * VoxelSize,
                Origin.z + (z + 0.5f) * VoxelSize);
        }

        public bool IsFlyable(int x, int y, int z, uint tagMask = uint.MaxValue)
        {
            if (!InBounds(x, y, z)) return false;
            return _voxels[ToIndex(x, y, z)].IsFlyable(tagMask);
        }

        public VoxelCell GetVoxel(int x, int y, int z)
        {
            if (!InBounds(x, y, z)) throw new ArgumentOutOfRangeException($"({x},{y},{z})");
            return _voxels[ToIndex(x, y, z)];
        }

        public void SetFlyable(int x, int y, int z, bool flyable)
        {
            if (!InBounds(x, y, z)) return;
            PrepareWrite();
            int idx = ToIndex(x, y, z);
            VoxelCell cell = _voxels[idx];
            if (cell.BaseFlyable == flyable) return;
            cell.BaseFlyable = flyable;
            _voxels[idx] = cell;
            Version++;
        }

        public void SetPenalty(int x, int y, int z, ushort penalty)
        {
            if (!InBounds(x, y, z)) return;
            PrepareWrite();
            int idx = ToIndex(x, y, z);
            VoxelCell cell = _voxels[idx];
            if (cell.Penalty == penalty) return;
            if (cell.Penalty == 0 && penalty > 0) _nonZeroPenaltyCount++;
            else if (cell.Penalty > 0 && penalty == 0) _nonZeroPenaltyCount--;
            cell.Penalty = penalty;
            _voxels[idx] = cell;
            Version++;
        }

        public void AddDynamicObstacleBox(int3 min, int3 max)
        {
            ClampBox(ref min, ref max);
            if (min.x > max.x || min.y > max.y || min.z > max.z) return;
            PrepareWrite();
            int slice = SizeX * SizeZ;
            for (int y = min.y; y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    int row = y * slice + z * SizeX;
                    for (int x = min.x; x <= max.x; x++)
                    {
                        VoxelCell c = _voxels[row + x];
                        if (c.DynamicBlockCount < byte.MaxValue) c.DynamicBlockCount++;
                        _voxels[row + x] = c;
                    }
                }
            }
            Version++;
        }

        public void RemoveDynamicObstacleBox(int3 min, int3 max)
        {
            ClampBox(ref min, ref max);
            if (min.x > max.x || min.y > max.y || min.z > max.z) return;
            PrepareWrite();
            int slice = SizeX * SizeZ;
            for (int y = min.y; y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    int row = y * slice + z * SizeX;
                    for (int x = min.x; x <= max.x; x++)
                    {
                        VoxelCell c = _voxels[row + x];
                        if (c.DynamicBlockCount > 0) c.DynamicBlockCount--;
                        _voxels[row + x] = c;
                    }
                }
            }
            Version++;
        }

        public void WorldBoundsToVoxelBox(Bounds bounds, float extraRadius, out int3 min, out int3 max)
        {
            float inv = 1f / VoxelSize;
            Vector3 bMin = bounds.min - Vector3.one * extraRadius;
            Vector3 bMax = bounds.max + Vector3.one * extraRadius;
            min = new int3(
                Mathf.FloorToInt((bMin.x - Origin.x) * inv),
                Mathf.FloorToInt((bMin.y - Origin.y) * inv),
                Mathf.FloorToInt((bMin.z - Origin.z) * inv));
            max = new int3(
                Mathf.FloorToInt((bMax.x - Origin.x) * inv),
                Mathf.FloorToInt((bMax.y - Origin.y) * inv),
                Mathf.FloorToInt((bMax.z - Origin.z) * inv));
        }

        /// <summary>
        /// 使用 3D 物理碰撞体烘焙整个三维立体飞行空域。
        /// </summary>
        public void BakePhysics3D(LayerMask obstacleMask, float agentRadius = 0.4f)
        {
            PrepareWrite();
            float half = VoxelSize * 0.5f + Mathf.Max(0f, agentRadius);
            Vector3 halfExtents = new Vector3(half, half, half);
            int slice = SizeX * SizeZ;

            for (int y = 0; y < SizeY; y++)
            {
                float wy = Origin.y + (y + 0.5f) * VoxelSize;
                for (int z = 0; z < SizeZ; z++)
                {
                    float wz = Origin.z + (z + 0.5f) * VoxelSize;
                    int row = y * slice + z * SizeX;
                    for (int x = 0; x < SizeX; x++)
                    {
                        Vector3 center = new Vector3(Origin.x + (x + 0.5f) * VoxelSize, wy, wz);
                        bool blocked = obstacleMask.value != 0 &&
                                       Physics.CheckBox(center, halfExtents, Quaternion.identity, obstacleMask, QueryTriggerInteraction.Ignore);
                        VoxelCell c = _voxels[row + x];
                        c.BaseFlyable = !blocked;
                        _voxels[row + x] = c;
                    }
                }
            }
            Version++;
        }

        private void ClampBox(ref int3 min, ref int3 max)
        {
            int3 lo = math.min(min, max);
            int3 hi = math.max(min, max);
            min = math.max(lo, int3.zero);
            max = math.min(hi, new int3(SizeX - 1, SizeY - 1, SizeZ - 1));
        }

        public void Dispose()
        {
            if (_disposed) return;
            BeforeMutate?.Invoke(this);
            _disposed = true;
            if (_voxels.IsCreated) _voxels.Dispose();
        }
    }
}
