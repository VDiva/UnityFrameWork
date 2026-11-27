using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 通用高性能寻路系统门面：
    /// - 非 Web 平台（Windows/Mac/Linux/Android/iOS 等）支持 Job 时默认启用 Burst 多线程 Worker 寻路；
    /// - WebGL 平台自动退化为主线程时间片预算分帧调度，避免卡顿且保持零 GC；
    /// - 统一支持 2D(XY/XZ) 地面、2D 双域飞行、以及 3D 立体体素空域自由飞行。
    /// </summary>
    public static unsafe partial class PathfindingSystem
    {
        private const int DefaultMaxWaypoints = 2048;

        public static PathExecutionMode ExecutionMode { get; set; } = PathExecutionMode.Auto;
        public static float WebGLFrameBudgetMs { get; set; } = 2.5f;

        public static PathGrid DefaultGrid { get; set; }
        public static PathVolume3D DefaultVolume3D { get; set; }

        /// <summary>
        /// 当前运行平台是否原生支持多线程 Job 寻路（WebGL 始终返回 false）。
        /// </summary>
        public static bool SupportsMultithreadedJobs
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return JobsUtility.JobWorkerCount > 0 && SystemInfo.processorCount > 1;
#endif
            }
        }

        /// <summary>
        /// 当前实际生效的调度模式是否为多线程 Burst Job。
        /// </summary>
        public static bool IsUsingMultithreadedJobs
        {
            get
            {
                if (ExecutionMode == PathExecutionMode.ForceMainThread) return false;
                if (ExecutionMode == PathExecutionMode.ForceJobs) return SupportsMultithreadedJobs;
                return SupportsMultithreadedJobs;
            }
        }

        public static void SetDefaultGrid(PathGrid grid)
        {
            DefaultGrid = grid;
            RegisterGridGuard(grid);
        }

        public static void SetDefaultVolume3D(PathVolume3D volume)
        {
            DefaultVolume3D = volume;
            RegisterVolumeGuard(volume);
        }

        #region 2D（地面 / 平面飞行）同步寻路 API

        public static PathResult FindPathSync(Vector3 start, Vector3 end)
            => FindPathSync(DefaultGrid, new PathQuery(start, end));

        public static PathResult FindPathSync(Vector3 start, Vector3 end, PathOptions options)
            => FindPathSync(DefaultGrid, new PathQuery(start, end, options));

        public static PathResult FindFlightPathSync(Vector3 start, Vector3 end, float altitudeOffset = 0f)
            => FindPathSync(DefaultGrid, new PathQuery(start, end, PathOptions.DefaultFlying(altitudeOffset)));

        public static PathResult FindPathSync(PathGrid grid, in PathQuery query)
        {
            if (grid == null || !grid.IsCreated)
                return new PathResult(PathStatus.InvalidGrid, Array.Empty<Vector3>(), 0f, 0, default, default);

            CompleteAllInFlightJobs();
            EnsureRuntimeResources(grid.CellCount);

            float3* wpBuf = (float3*)UnsafeUtility.Malloc(
                (long)DefaultMaxWaypoints * sizeof(float3), UnsafeUtility.AlignOf<float3>(), Allocator.Temp);
            try
            {
                PathSolveOutput res = SolveSingle2DInternal(grid, in query, wpBuf, DefaultMaxWaypoints);
                var arr = res.WaypointCount > 0 ? new Vector3[res.WaypointCount] : Array.Empty<Vector3>();
                for (int i = 0; i < res.WaypointCount; i++)
                {
                    float3 p = wpBuf[i];
                    arr[i] = new Vector3(p.x, p.y, p.z);
                }
                return new PathResult(res.Status, arr, res.TotalCost, res.VisitedNodes, res.StartCoord, res.EndCoord);
            }
            finally
            {
                UnsafeUtility.Free(wpBuf, Allocator.Temp);
            }
        }

        /// <summary>
        /// 零 GC 同步寻路（复用调用方 List 接收路点）。
        /// </summary>
        public static PathStatus FindPathSync(
            PathGrid grid, in PathQuery query, List<Vector3> outWaypoints, out float totalCost, out int visitedNodes)
        {
            totalCost = 0f;
            visitedNodes = 0;
            outWaypoints?.Clear();
            if (grid == null || !grid.IsCreated) return PathStatus.InvalidGrid;

            CompleteAllInFlightJobs();
            EnsureRuntimeResources(grid.CellCount);

            float3* wpBuf = (float3*)UnsafeUtility.Malloc(
                (long)DefaultMaxWaypoints * sizeof(float3), UnsafeUtility.AlignOf<float3>(), Allocator.Temp);
            try
            {
                PathSolveOutput res = SolveSingle2DInternal(grid, in query, wpBuf, DefaultMaxWaypoints);
                totalCost = res.TotalCost;
                visitedNodes = res.VisitedNodes;
                if (outWaypoints != null && res.WaypointCount > 0)
                {
                    if (outWaypoints.Capacity < res.WaypointCount)
                        outWaypoints.Capacity = res.WaypointCount;
                    for (int i = 0; i < res.WaypointCount; i++)
                    {
                        float3 p = wpBuf[i];
                        outWaypoints.Add(new Vector3(p.x, p.y, p.z));
                    }
                }
                return res.Status;
            }
            finally
            {
                UnsafeUtility.Free(wpBuf, Allocator.Temp);
            }
        }

        #endregion

        #region 3D 立体空域飞行同步寻路 API

        public static PathResult FindFlightPath3DSync(Vector3 start, Vector3 end)
            => FindFlightPath3DSync(DefaultVolume3D, new PathQuery(start, end, PathOptions.DefaultFlying()));

        public static PathResult FindFlightPath3DSync(PathVolume3D volume, in PathQuery query)
        {
            if (volume == null || !volume.IsCreated)
                return new PathResult(PathStatus.InvalidGrid, Array.Empty<Vector3>(), 0f, 0, default, default);

            CompleteAllInFlightJobs();
            EnsureRuntimeResources(volume.VoxelCount);

            float3* wpBuf = (float3*)UnsafeUtility.Malloc(
                (long)DefaultMaxWaypoints * sizeof(float3), UnsafeUtility.AlignOf<float3>(), Allocator.Temp);
            try
            {
                PathSolveOutput res = SolveSingle3DInternal(volume, in query, wpBuf, DefaultMaxWaypoints);
                var arr = res.WaypointCount > 0 ? new Vector3[res.WaypointCount] : Array.Empty<Vector3>();
                for (int i = 0; i < res.WaypointCount; i++)
                {
                    float3 p = wpBuf[i];
                    arr[i] = new Vector3(p.x, p.y, p.z);
                }
                return new PathResult(res.Status, arr, res.TotalCost, res.VisitedNodes, res.StartCoord, res.EndCoord);
            }
            finally
            {
                UnsafeUtility.Free(wpBuf, Allocator.Temp);
            }
        }

        public static PathStatus FindFlightPath3DSync(
            PathVolume3D volume, in PathQuery query, List<Vector3> outWaypoints, out float totalCost, out int visitedNodes)
        {
            totalCost = 0f;
            visitedNodes = 0;
            outWaypoints?.Clear();
            if (volume == null || !volume.IsCreated) return PathStatus.InvalidGrid;

            CompleteAllInFlightJobs();
            EnsureRuntimeResources(volume.VoxelCount);

            float3* wpBuf = (float3*)UnsafeUtility.Malloc(
                (long)DefaultMaxWaypoints * sizeof(float3), UnsafeUtility.AlignOf<float3>(), Allocator.Temp);
            try
            {
                PathSolveOutput res = SolveSingle3DInternal(volume, in query, wpBuf, DefaultMaxWaypoints);
                totalCost = res.TotalCost;
                visitedNodes = res.VisitedNodes;
                if (outWaypoints != null && res.WaypointCount > 0)
                {
                    if (outWaypoints.Capacity < res.WaypointCount)
                        outWaypoints.Capacity = res.WaypointCount;
                    for (int i = 0; i < res.WaypointCount; i++)
                    {
                        float3 p = wpBuf[i];
                        outWaypoints.Add(new Vector3(p.x, p.y, p.z));
                    }
                }
                return res.Status;
            }
            finally
            {
                UnsafeUtility.Free(wpBuf, Allocator.Temp);
            }
        }

        #endregion

        #region 异步（Callback / UniTask / 批量）寻路 API

        public static PathRequestHandle RequestPath(Vector3 start, Vector3 end, Action<PathResult> onComplete)
            => RequestPath(DefaultGrid, new PathQuery(start, end), onComplete);

        public static PathRequestHandle RequestPath(PathGrid grid, PathQuery query, Action<PathResult> onComplete)
        {
            if (grid == null || !grid.IsCreated)
            {
                onComplete?.Invoke(new PathResult(PathStatus.InvalidGrid, Array.Empty<Vector3>(), 0f, 0, default, default));
                return default;
            }
            EnsureDriver();
            RegisterGridGuard(grid);
            int id = ++_nextRequestId;
            _pending2D.Add(new Pending2DRequest { Id = id, Grid = grid, Query = query, Callback = onComplete });
            return new PathRequestHandle(id);
        }

        public static PathRequestHandle RequestFlightPath3D(PathVolume3D volume, PathQuery query, Action<PathResult> onComplete)
        {
            if (volume == null || !volume.IsCreated)
            {
                onComplete?.Invoke(new PathResult(PathStatus.InvalidGrid, Array.Empty<Vector3>(), 0f, 0, default, default));
                return default;
            }
            EnsureDriver();
            RegisterVolumeGuard(volume);
            int id = ++_nextRequestId;
            _pending3D.Add(new Pending3DRequest { Id = id, Volume = volume, Query = query, Callback = onComplete });
            return new PathRequestHandle(id);
        }

        public static void CancelRequest(PathRequestHandle handle)
        {
            if (!handle.IsValid) return;
            _canceledIds.Add(handle.Id);
        }

        public static UniTask<PathResult> FindPathAsync(Vector3 start, Vector3 end)
            => FindPathAsync(DefaultGrid, new PathQuery(start, end));

        public static UniTask<PathResult> FindFlightPathAsync(Vector3 start, Vector3 end, float altitudeOffset = 0f)
            => FindPathAsync(DefaultGrid, new PathQuery(start, end, PathOptions.DefaultFlying(altitudeOffset)));

        public static UniTask<PathResult> FindPathAsync(PathGrid grid, PathQuery query)
        {
            var utcs = new UniTaskCompletionSource<PathResult>();
            RequestPath(grid, query, res => utcs.TrySetResult(res));
            return utcs.Task;
        }

        public static UniTask<PathResult> FindFlightPath3DAsync(Vector3 start, Vector3 end)
            => FindFlightPath3DAsync(DefaultVolume3D, new PathQuery(start, end, PathOptions.DefaultFlying()));

        public static UniTask<PathResult> FindFlightPath3DAsync(PathVolume3D volume, PathQuery query)
        {
            var utcs = new UniTaskCompletionSource<PathResult>();
            RequestFlightPath3D(volume, query, res => utcs.TrySetResult(res));
            return utcs.Task;
        }

        public static UniTask<PathResult[]> FindPathsBatchAsync(PathGrid grid, IReadOnlyList<PathQuery> queries)
        {
            if (queries == null || queries.Count == 0)
                return UniTask.FromResult(Array.Empty<PathResult>());

            var results = new PathResult[queries.Count];
            int remaining = queries.Count;
            var utcs = new UniTaskCompletionSource<PathResult[]>();

            for (int i = 0; i < queries.Count; i++)
            {
                int index = i;
                RequestPath(grid, queries[i], res =>
                {
                    results[index] = res;
                    if (--remaining == 0)
                        utcs.TrySetResult(results);
                });
            }
            return utcs.Task;
        }

        public static UniTask<PathResult[]> FindFlightPaths3DBatchAsync(PathVolume3D volume, IReadOnlyList<PathQuery> queries)
        {
            if (queries == null || queries.Count == 0)
                return UniTask.FromResult(Array.Empty<PathResult>());

            var results = new PathResult[queries.Count];
            int remaining = queries.Count;
            var utcs = new UniTaskCompletionSource<PathResult[]>();

            for (int i = 0; i < queries.Count; i++)
            {
                int index = i;
                RequestFlightPath3D(volume, queries[i], res =>
                {
                    results[index] = res;
                    if (--remaining == 0)
                        utcs.TrySetResult(results);
                });
            }
            return utcs.Task;
        }

        #endregion
    }
}
