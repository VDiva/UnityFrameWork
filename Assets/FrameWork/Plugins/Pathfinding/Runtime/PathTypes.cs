using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FrameWork.Pathfinding
{
    public enum GridPlane : byte
    {
        XZ = 0,
        XY = 1
    }

    /// <summary>
    /// 移动空间域：地面单位受地面障碍阻挡，飞行单位忽略地面障碍、仅受高空/禁飞障碍阻挡。
    /// </summary>
    public enum MovementDomain : byte
    {
        Ground = 0,
        Air = 1
    }

    public enum PathAlgorithm : byte
    {
        Auto = 0,
        JPS = 1,
        AStar = 2
    }

    public enum PathExecutionMode : byte
    {
        Auto = 0,
        ForceJobs = 1,
        ForceMainThread = 2
    }

    public enum PathOutputMode : byte
    {
        SmoothedWaypoints = 0,
        CornerWaypoints = 1,
        FullGridCells = 2
    }

    public enum PathStatus : byte
    {
        Success = 0,
        PartialPath = 1,
        StartOutOfBounds = 2,
        EndOutOfBounds = 3,
        Unreachable = 4,
        InvalidGrid = 5,
        Canceled = 6
    }

    /// <summary>
    /// 紧凑 6 字节双域（地面 + 空中）网格单元。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GridCell
    {
        private const byte GroundWalkableBit = 0x01;
        private const byte AirWalkableBit = 0x40;
        private const int TagShift = 1;
        private const byte TagMaskBits = 0x3E; // bits 1..5 -> 0..31

        public byte Flags;
        public byte GroundDynamicCount;
        public byte AirDynamicCount;
        public byte Reserved;
        public ushort Penalty;

        public bool BaseWalkable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & GroundWalkableBit) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Flags = value ? (byte)(Flags | GroundWalkableBit) : (byte)(Flags & ~GroundWalkableBit);
        }

        public bool AirBaseWalkable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & AirWalkableBit) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Flags = value ? (byte)(Flags | AirWalkableBit) : (byte)(Flags & ~AirWalkableBit);
        }

        public int Tag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & TagMaskBits) >> TagShift;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Flags = (byte)((Flags & ~TagMaskBits) | ((value & 31) << TagShift));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWalkable(uint walkableTagMask = uint.MaxValue, MovementDomain domain = MovementDomain.Ground)
        {
            if ((walkableTagMask & (1u << ((Flags & TagMaskBits) >> TagShift))) == 0)
                return false;
            return domain == MovementDomain.Air
                ? (Flags & AirWalkableBit) != 0 && AirDynamicCount == 0
                : (Flags & GroundWalkableBit) != 0 && GroundDynamicCount == 0;
        }
    }

    /// <summary>
    /// 3D 立体飞行空域体素单元（4 字节对齐，支持静态禁飞区、动态空中障碍物引用计数及气流代价权重）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelCell
    {
        private const byte FlyableBit = 0x01;
        private const int TagShift = 1;
        private const byte TagMaskBits = 0x3E;

        public byte Flags;
        public byte DynamicBlockCount;
        public ushort Penalty;

        public bool BaseFlyable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & FlyableBit) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Flags = value ? (byte)(Flags | FlyableBit) : (byte)(Flags & ~FlyableBit);
        }

        public int Tag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & TagMaskBits) >> TagShift;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Flags = (byte)((Flags & ~TagMaskBits) | ((value & 31) << TagShift));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFlyable(uint tagMask = uint.MaxValue)
        {
            return (Flags & FlyableBit) != 0 &&
                   DynamicBlockCount == 0 &&
                   (tagMask & (1u << ((Flags & TagMaskBits) >> TagShift))) != 0;
        }
    }

    [Serializable]
    public struct PathOptions
    {
        public MovementDomain Domain;
        public PathAlgorithm Algorithm;
        public PathOutputMode OutputMode;
        public bool AllowDiagonal;
        public bool CutCorners;
        public bool SnapUnwalkableStart;
        public bool SnapUnwalkableEnd;
        public bool AllowPartialPath;
        public int MaxSnapRadius;
        public int MaxExpandedNodes;
        public uint WalkableTagMask;
        /// <summary>平面网格处于 Air 模式时附加的巡航高度偏移（XZ 下作用于 Y，XY 下作用于 Z）。</summary>
        public float FlightAltitudeOffset;

        public static PathOptions Default => new PathOptions
        {
            Domain = MovementDomain.Ground,
            Algorithm = PathAlgorithm.Auto,
            OutputMode = PathOutputMode.SmoothedWaypoints,
            AllowDiagonal = true,
            CutCorners = false,
            SnapUnwalkableStart = true,
            SnapUnwalkableEnd = true,
            AllowPartialPath = true,
            MaxSnapRadius = 4,
            MaxExpandedNodes = 0,
            WalkableTagMask = uint.MaxValue,
            FlightAltitudeOffset = 0f
        };

        public static PathOptions DefaultFlying(float flightAltitudeOffset = 0f)
        {
            var opt = Default;
            opt.Domain = MovementDomain.Air;
            opt.FlightAltitudeOffset = flightAltitudeOffset;
            return opt;
        }
    }

    public struct PathQuery
    {
        public Vector3 Start;
        public Vector3 End;
        public PathOptions Options;

        public PathQuery(Vector3 start, Vector3 end)
        {
            Start = start;
            End = end;
            Options = PathOptions.Default;
        }

        public PathQuery(Vector3 start, Vector3 end, PathOptions options)
        {
            Start = start;
            End = end;
            Options = options;
        }
    }

    public readonly struct PathResult
    {
        public readonly PathStatus Status;
        public readonly Vector3[] Waypoints;
        public readonly float TotalCost;
        public readonly int VisitedNodes;
        public readonly int3 StartCoord;
        public readonly int3 EndCoord;

        public bool IsSuccess => Status == PathStatus.Success || Status == PathStatus.PartialPath;
        public int WaypointCount => Waypoints != null ? Waypoints.Length : 0;
        public int2 StartCell => new int2(StartCoord.x, StartCoord.y);
        public int2 EndCell => new int2(EndCoord.x, EndCoord.y);

        public PathResult(
            PathStatus status,
            Vector3[] waypoints,
            float totalCost,
            int visitedNodes,
            int3 startCoord,
            int3 endCoord)
        {
            Status = status;
            Waypoints = waypoints ?? Array.Empty<Vector3>();
            TotalCost = totalCost;
            VisitedNodes = visitedNodes;
            StartCoord = startCoord;
            EndCoord = endCoord;
        }
    }

    public readonly struct PathRequestHandle : IEquatable<PathRequestHandle>
    {
        public readonly int Id;
        public PathRequestHandle(int id) => Id = id;
        public bool IsValid => Id > 0;
        public bool Equals(PathRequestHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is PathRequestHandle other && Id == other.Id;
        public override int GetHashCode() => Id;
    }

    public struct PathGridData
    {
        [ReadOnly] public NativeArray<GridCell> Cells;
        [ReadOnly] public NativeArray<float> Heights;
        public int Width;
        public int Height;
        public float CellSize;
        public float InvCellSize;
        public float3 Origin;
        public GridPlane Plane;
        public byte HasPenalties;

        public int CellCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Width * Height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToIndex(int x, int y) => y * Width + x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 FromIndex(int index)
        {
            int y = index / Width;
            return new int2(index - y * Width, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWalkable(int x, int y, uint tagMask, MovementDomain domain)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return false;
            return Cells[y * Width + x].IsWalkable(tagMask, domain);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 WorldToCell(float3 worldPos)
        {
            float lx = (worldPos.x - Origin.x) * InvCellSize;
            float ly = Plane == GridPlane.XZ
                ? (worldPos.z - Origin.z) * InvCellSize
                : (worldPos.y - Origin.y) * InvCellSize;
            return new int2((int)math.floor(lx), (int)math.floor(ly));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 CellToWorld(int x, int y, MovementDomain domain, float altitudeOffset)
        {
            float cx = Origin.x + (x + 0.5f) * CellSize;
            if (Plane == GridPlane.XZ)
            {
                int idx = y * Width + x;
                float baseY = domain == MovementDomain.Ground && Heights.IsCreated && (uint)idx < (uint)Heights.Length
                    ? Heights[idx]
                    : Origin.y;
                return new float3(cx, baseY + altitudeOffset, Origin.z + (y + 0.5f) * CellSize);
            }
            return new float3(cx, Origin.y + (y + 0.5f) * CellSize, Origin.z + altitudeOffset);
        }
    }

    public struct PathVolume3DData
    {
        [ReadOnly] public NativeArray<VoxelCell> Voxels;
        public int SizeX;
        public int SizeY;
        public int SizeZ;
        public int SliceStride; // SizeX * SizeZ
        public float VoxelSize;
        public float InvVoxelSize;
        public float3 Origin;
        public byte HasPenalties;

        public int VoxelCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SizeX * SizeY * SizeZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InBounds(int x, int y, int z)
        {
            return (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToIndex(int x, int y, int z) => y * SliceStride + z * SizeX + x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 FromIndex(int index)
        {
            int y = index / SliceStride;
            int rem = index - y * SliceStride;
            int z = rem / SizeX;
            int x = rem - z * SizeX;
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFlyable(int x, int y, int z, uint tagMask)
        {
            if ((uint)x >= (uint)SizeX || (uint)y >= (uint)SizeY || (uint)z >= (uint)SizeZ)
                return false;
            return Voxels[y * SliceStride + z * SizeX + x].IsFlyable(tagMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 WorldToVoxel(float3 worldPos)
        {
            float3 local = (worldPos - Origin) * InvVoxelSize;
            return (int3)math.floor(local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 VoxelToWorld(int x, int y, int z)
        {
            return Origin + new float3(x + 0.5f, y + 0.5f, z + 0.5f) * VoxelSize;
        }
    }
}
