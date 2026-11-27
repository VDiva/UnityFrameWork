using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace FrameWork.Pathfinding
{
    public static unsafe partial class PathfindingSystem
    {
        private struct Pending2DRequest
        {
            public int Id;
            public PathGrid Grid;
            public PathQuery Query;
            public Action<PathResult> Callback;
        }

        private struct Pending3DRequest
        {
            public int Id;
            public PathVolume3D Volume;
            public PathQuery Query;
            public Action<PathResult> Callback;
        }

        private sealed class ActiveBatch2D : IDisposable
        {
            public JobHandle Handle;
            public List<Pending2DRequest> Requests;
            public NativeArray<PathQuery> Queries;
            public NativeArray<PathSolveOutput> Outputs;
            public float3* Arena;
            public int* Counters; // [0] = nextQuery, [1] = waypointCursor

            public void Dispose()
            {
                if (Queries.IsCreated) Queries.Dispose();
                if (Outputs.IsCreated) Outputs.Dispose();
                if (Arena != null) { UnsafeUtility.Free(Arena, Allocator.Persistent); Arena = null; }
                if (Counters != null) { UnsafeUtility.Free(Counters, Allocator.Persistent); Counters = null; }
            }
        }

        private sealed class ActiveBatch3D : IDisposable
        {
            public JobHandle Handle;
            public List<Pending3DRequest> Requests;
            public NativeArray<PathQuery> Queries;
            public NativeArray<PathSolveOutput> Outputs;
            public float3* Arena;
            public int* Counters;

            public void Dispose()
            {
                if (Queries.IsCreated) Queries.Dispose();
                if (Outputs.IsCreated) Outputs.Dispose();
                if (Arena != null) { UnsafeUtility.Free(Arena, Allocator.Persistent); Arena = null; }
                if (Counters != null) { UnsafeUtility.Free(Counters, Allocator.Persistent); Counters = null; }
            }
        }

        private static SolverScratchPool _pool;
        private static readonly List<Pending2DRequest> _pending2D = new List<Pending2DRequest>(64);
        private static readonly List<Pending3DRequest> _pending3D = new List<Pending3DRequest>(32);
        private static readonly HashSet<int> _canceledIds = new HashSet<int>();
        private static ActiveBatch2D _activeBatch2D;
        private static ActiveBatch3D _activeBatch3D;
        private static int _nextRequestId;
        private static PathfindingDriver _driver;
        private static readonly Action<PathGrid> _completeGridGuardAction = _ => CompleteAllInFlightJobs();
        private static readonly Action<PathVolume3D> _completeVolumeGuardAction = _ => CompleteAllInFlightJobs();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Shutdown();
            ExecutionMode = PathExecutionMode.Auto;
            WebGLFrameBudgetMs = 2.5f;
            DefaultGrid = null;
            DefaultVolume3D = null;
            _nextRequestId = 0;
        }

        public static void Shutdown()
        {
            CompleteAllInFlightJobs();
            _pending2D.Clear();
            _pending3D.Clear();
            _canceledIds.Clear();
            _pool?.Dispose();
            _pool = null;
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
                _driver = null;
            }
        }

        private static void RegisterGridGuard(PathGrid grid)
        {
            if (grid != null)
                grid.BeforeMutate = _completeGridGuardAction;
        }

        private static void RegisterVolumeGuard(PathVolume3D volume)
        {
            if (volume != null)
                volume.BeforeMutate = _completeVolumeGuardAction;
        }

        private static void EnsureRuntimeResources(int requiredNodes)
        {
            if (_pool == null)
            {
                int workers = SupportsMultithreadedJobs ? math.max(1, JobsUtility.JobWorkerCount) : 1;
                _pool = new SolverScratchPool(workers, math.max(4096, requiredNodes));
            }
            else
            {
                _pool.EnsureCapacity(requiredNodes);
            }
        }

        private static void EnsureDriver()
        {
            if (_driver != null || !Application.isPlaying) return;
            var go = new GameObject("[PathfindingSystemDriver]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<PathfindingDriver>();
        }

        private static PathSolveOutput SolveSingle2DInternal(
            PathGrid grid, in PathQuery query, float3* wpBuf, int maxWaypoints)
        {
            PathGridData data = grid.AsReadOnlyData();
            if (IsUsingMultithreadedJobs)
            {
                var outArr = new NativeArray<PathSolveOutput>(1, Allocator.TempJob);
                try
                {
                    var job = new PathSingle2DJob
                    {
                        Grid = data,
                        Query = query,
                        Workspaces = _pool.Workspaces,
                        OutWaypoints = wpBuf,
                        MaxWaypoints = maxWaypoints,
                        Output = outArr
                    };
                    job.Run();
                    return outArr[0];
                }
                finally
                {
                    outArr.Dispose();
                }
            }

            var workspaces = _pool.Workspaces;
            SolverWorkspace ws = workspaces[0];
            PathSolveOutput res = PathSolver2D.Solve(in data, in query, ref ws, wpBuf, maxWaypoints);
            workspaces[0] = ws;
            return res;
        }

        private static PathSolveOutput SolveSingle3DInternal(
            PathVolume3D volume, in PathQuery query, float3* wpBuf, int maxWaypoints)
        {
            PathVolume3DData data = volume.AsReadOnlyData();
            if (IsUsingMultithreadedJobs)
            {
                var outArr = new NativeArray<PathSolveOutput>(1, Allocator.TempJob);
                try
                {
                    var job = new PathSingle3DJob
                    {
                        Volume = data,
                        Query = query,
                        Workspaces = _pool.Workspaces,
                        OutWaypoints = wpBuf,
                        MaxWaypoints = maxWaypoints,
                        Output = outArr
                    };
                    job.Run();
                    return outArr[0];
                }
                finally
                {
                    outArr.Dispose();
                }
            }

            var workspaces = _pool.Workspaces;
            SolverWorkspace ws = workspaces[0];
            PathSolveOutput res = PathSolver3D.Solve(in data, in query, ref ws, wpBuf, maxWaypoints);
            workspaces[0] = ws;
            return res;
        }

        /// <summary>
        /// 强制完成所有进行中的寻路 Job（在网格/动态障碍修改前自动触发，保证线程安全）。
        /// </summary>
        public static void CompleteAllInFlightJobs()
        {
            if (_activeBatch2D != null)
            {
                _activeBatch2D.Handle.Complete();
                DeliverBatch2D(_activeBatch2D);
                _activeBatch2D.Dispose();
                _activeBatch2D = null;
            }
            if (_activeBatch3D != null)
            {
                _activeBatch3D.Handle.Complete();
                DeliverBatch3D(_activeBatch3D);
                _activeBatch3D.Dispose();
                _activeBatch3D = null;
            }
        }

        internal static void TickFrame()
        {
            // 1. 先收割上一帧完成的异步 Job
            if (_activeBatch2D != null && _activeBatch2D.Handle.IsCompleted)
            {
                _activeBatch2D.Handle.Complete();
                DeliverBatch2D(_activeBatch2D);
                _activeBatch2D.Dispose();
                _activeBatch2D = null;
            }
            if (_activeBatch3D != null && _activeBatch3D.Handle.IsCompleted)
            {
                _activeBatch3D.Handle.Complete();
                DeliverBatch3D(_activeBatch3D);
                _activeBatch3D.Dispose();
                _activeBatch3D = null;
            }

            if (_pending2D.Count == 0 && _pending3D.Count == 0)
                return;

            // 2. 多线程平台：发射 Burst IJobParallelFor 批次；WebGL/主线程模式：按帧预算分帧求解
            if (IsUsingMultithreadedJobs)
            {
                TryDispatchBatch2D();
                TryDispatchBatch3D();
            }
            else
            {
                PumpMainThreadBudgeted();
            }
        }

        private static void TryDispatchBatch2D()
        {
            if (_activeBatch2D != null || _pending2D.Count == 0) return;

            PathGrid targetGrid = null;
            var batchReqs = new List<Pending2DRequest>(_pending2D.Count);
            int writeIdx = 0;

            for (int i = 0; i < _pending2D.Count; i++)
            {
                var req = _pending2D[i];
                if (_canceledIds.Remove(req.Id))
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.Canceled, Array.Empty<Vector3>(), 0f, 0, default, default));
                    continue;
                }
                if (req.Grid == null || !req.Grid.IsCreated)
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.InvalidGrid, Array.Empty<Vector3>(), 0f, 0, default, default));
                    continue;
                }
                if (targetGrid == null) targetGrid = req.Grid;
                if (req.Grid == targetGrid)
                    batchReqs.Add(req);
                else
                    _pending2D[writeIdx++] = req;
            }
            _pending2D.RemoveRange(writeIdx, _pending2D.Count - writeIdx);

            if (batchReqs.Count == 0 || targetGrid == null) return;

            EnsureRuntimeResources(targetGrid.CellCount);
            int count = batchReqs.Count;
            int arenaCap = math.max(count * 64, 1024);
            var queries = new NativeArray<PathQuery>(count, Allocator.Persistent);
            var outputs = new NativeArray<PathSolveOutput>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++) queries[i] = batchReqs[i].Query;

            var arena = (float3*)UnsafeUtility.Malloc((long)arenaCap * sizeof(float3), UnsafeUtility.AlignOf<float3>(), Allocator.Persistent);
            var counters = (int*)UnsafeUtility.Malloc(2 * sizeof(int), sizeof(int), Allocator.Persistent);
            counters[0] = 0;
            counters[1] = 0;

            int workers = math.min(_pool.WorkerCount, count);
            var job = new PathBatch2DJob
            {
                Grid = targetGrid.AsReadOnlyData(),
                Queries = queries,
                Workspaces = _pool.Workspaces,
                Outputs = outputs,
                WaypointArena = arena,
                WaypointArenaCapacity = arenaCap,
                MaxWaypointsPerQuery = 256,
                NextQueryIndexPtr = counters,
                WaypointCursorPtr = counters + 1
            };

            _activeBatch2D = new ActiveBatch2D
            {
                Requests = batchReqs,
                Queries = queries,
                Outputs = outputs,
                Arena = arena,
                Counters = counters,
                Handle = job.Schedule(workers, 1)
            };
            JobHandle.ScheduleBatchedJobs();
        }

        private static void TryDispatchBatch3D()
        {
            if (_activeBatch3D != null || _pending3D.Count == 0) return;

            PathVolume3D targetVol = null;
            var batchReqs = new List<Pending3DRequest>(_pending3D.Count);
            int writeIdx = 0;

            for (int i = 0; i < _pending3D.Count; i++)
            {
                var req = _pending3D[i];
                if (_canceledIds.Remove(req.Id))
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.Canceled, Array.Empty<Vector3>(), 0f, 0, default, default));
                    continue;
                }
                if (req.Volume == null || !req.Volume.IsCreated)
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.InvalidGrid, Array.Empty<Vector3>(), 0f, 0, default, default));
                    continue;
                }
                if (targetVol == null) targetVol = req.Volume;
                if (req.Volume == targetVol)
                    batchReqs.Add(req);
                else
                    _pending3D[writeIdx++] = req;
            }
            _pending3D.RemoveRange(writeIdx, _pending3D.Count - writeIdx);

            if (batchReqs.Count == 0 || targetVol == null) return;

            EnsureRuntimeResources(targetVol.VoxelCount);
            int count = batchReqs.Count;
            int arenaCap = math.max(count * 64, 1024);
            var queries = new NativeArray<PathQuery>(count, Allocator.Persistent);
            var outputs = new NativeArray<PathSolveOutput>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++) queries[i] = batchReqs[i].Query;

            var arena = (float3*)UnsafeUtility.Malloc((long)arenaCap * sizeof(float3), UnsafeUtility.AlignOf<float3>(), Allocator.Persistent);
            var counters = (int*)UnsafeUtility.Malloc(2 * sizeof(int), sizeof(int), Allocator.Persistent);
            counters[0] = 0;
            counters[1] = 0;

            int workers = math.min(_pool.WorkerCount, count);
            var job = new PathBatch3DJob
            {
                Volume = targetVol.AsReadOnlyData(),
                Queries = queries,
                Workspaces = _pool.Workspaces,
                Outputs = outputs,
                WaypointArena = arena,
                WaypointArenaCapacity = arenaCap,
                MaxWaypointsPerQuery = 256,
                NextQueryIndexPtr = counters,
                WaypointCursorPtr = counters + 1
            };

            _activeBatch3D = new ActiveBatch3D
            {
                Requests = batchReqs,
                Queries = queries,
                Outputs = outputs,
                Arena = arena,
                Counters = counters,
                Handle = job.Schedule(workers, 1)
            };
            JobHandle.ScheduleBatchedJobs();
        }

        private static void DeliverBatch2D(ActiveBatch2D batch)
        {
            for (int i = 0; i < batch.Requests.Count; i++)
            {
                var req = batch.Requests[i];
                if (_canceledIds.Remove(req.Id))
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.Canceled, Array.Empty<Vector3>(), 0f, 0, default, default));
                    continue;
                }
                PathSolveOutput o = batch.Outputs[i];
                var arr = o.WaypointCount > 0 ? new Vector3[o.WaypointCount] : Array.Empty<Vector3>();
                for (int w = 0; w < o.WaypointCount; w++)
                {
                    float3 p = batch.Arena[o.WaypointOffset + w];
                    arr[w] = new Vector3(p.x, p.y, p.z);
                }
                req.Callback?.Invoke(new PathResult(o.Status, arr, o.TotalCost, o.VisitedNodes, o.StartCoord, o.EndCoord));
            }
        }

        private static void DeliverBatch3D(ActiveBatch3D batch)
        {
            for (int i = 0; i < batch.Requests.Count; i++)
            {
                var req = batch.Requests[i];
                if (_canceledIds.Remove(req.Id))
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.Canceled, Array.Empty<Vector3>(), 0f, 0, default, default));
                    continue;
                }
                PathSolveOutput o = batch.Outputs[i];
                var arr = o.WaypointCount > 0 ? new Vector3[o.WaypointCount] : Array.Empty<Vector3>();
                for (int w = 0; w < o.WaypointCount; w++)
                {
                    float3 p = batch.Arena[o.WaypointOffset + w];
                    arr[w] = new Vector3(p.x, p.y, p.z);
                }
                req.Callback?.Invoke(new PathResult(o.Status, arr, o.TotalCost, o.VisitedNodes, o.StartCoord, o.EndCoord));
            }
        }

        private static void PumpMainThreadBudgeted()
        {
            double budgetMs = math.max(0.25f, WebGLFrameBudgetMs);
            var sw = Stopwatch.StartNew();

            while (_pending2D.Count > 0)
            {
                var req = _pending2D[0];
                _pending2D.RemoveAt(0);
                if (_canceledIds.Remove(req.Id))
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.Canceled, Array.Empty<Vector3>(), 0f, 0, default, default));
                }
                else
                {
                    var res = FindPathSync(req.Grid, in req.Query);
                    req.Callback?.Invoke(res);
                }
                if (sw.Elapsed.TotalMilliseconds >= budgetMs) return;
            }

            while (_pending3D.Count > 0)
            {
                var req = _pending3D[0];
                _pending3D.RemoveAt(0);
                if (_canceledIds.Remove(req.Id))
                {
                    req.Callback?.Invoke(new PathResult(PathStatus.Canceled, Array.Empty<Vector3>(), 0f, 0, default, default));
                }
                else
                {
                    var res = FindFlightPath3DSync(req.Volume, in req.Query);
                    req.Callback?.Invoke(res);
                }
                if (sw.Elapsed.TotalMilliseconds >= budgetMs) return;
            }
        }

        [DefaultExecutionOrder(-50)]
        private sealed class PathfindingDriver : MonoBehaviour
        {
            private void Update() => TickFrame();
            private void OnDestroy()
            {
                if (_driver == this)
                {
                    _driver = null;
                    CompleteAllInFlightJobs();
                }
            }
        }
    }
}
