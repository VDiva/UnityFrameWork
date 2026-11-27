using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// Burst 编译的 2D 单次寻路 Job（同步快速执行）。
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    internal unsafe struct PathSingle2DJob : IJob
    {
        [ReadOnly] public PathGridData Grid;
        public PathQuery Query;
        [NativeDisableParallelForRestriction] public NativeArray<SolverWorkspace> Workspaces;
        [NativeDisableUnsafePtrRestriction] public float3* OutWaypoints;
        public int MaxWaypoints;
        public NativeArray<PathSolveOutput> Output;

        public void Execute()
        {
            SolverWorkspace ws = Workspaces[0];
            Output[0] = PathSolver2D.Solve(in Grid, in Query, ref ws, OutWaypoints, MaxWaypoints);
            Workspaces[0] = ws;
        }
    }

    /// <summary>
    /// Burst 编译的 3D 立体空域单次寻路 Job。
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    internal unsafe struct PathSingle3DJob : IJob
    {
        [ReadOnly] public PathVolume3DData Volume;
        public PathQuery Query;
        [NativeDisableParallelForRestriction] public NativeArray<SolverWorkspace> Workspaces;
        [NativeDisableUnsafePtrRestriction] public float3* OutWaypoints;
        public int MaxWaypoints;
        public NativeArray<PathSolveOutput> Output;

        public void Execute()
        {
            SolverWorkspace ws = Workspaces[0];
            Output[0] = PathSolver3D.Solve(in Volume, in Query, ref ws, OutWaypoints, MaxWaypoints);
            Workspaces[0] = ws;
        }
    }

    /// <summary>
    /// Burst 多线程 2D 批量寻路 Job：按 Worker 槽位并行窃取查询队列，零竞争使用独立 SolverWorkspace。
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    internal unsafe struct PathBatch2DJob : IJobParallelFor
    {
        [ReadOnly] public PathGridData Grid;
        [ReadOnly] public NativeArray<PathQuery> Queries;
        [NativeDisableParallelForRestriction] public NativeArray<SolverWorkspace> Workspaces;
        [NativeDisableParallelForRestriction] public NativeArray<PathSolveOutput> Outputs;
        [NativeDisableUnsafePtrRestriction] public float3* WaypointArena;
        public int WaypointArenaCapacity;
        public int MaxWaypointsPerQuery;
        [NativeDisableUnsafePtrRestriction] public int* NextQueryIndexPtr;
        [NativeDisableUnsafePtrRestriction] public int* WaypointCursorPtr;

        public void Execute(int workerIndex)
        {
            SolverWorkspace ws = Workspaces[workerIndex];
            int queryCount = Queries.Length;
            float3* localBuf = stackalloc float3[256];

            while (true)
            {
                int qIdx = Interlocked.Increment(ref *NextQueryIndexPtr) - 1;
                if (qIdx >= queryCount) break;

                int maxWp = math.min(256, MaxWaypointsPerQuery);
                PathQuery q = Queries[qIdx];
                PathSolveOutput res = PathSolver2D.Solve(in Grid, in q, ref ws, localBuf, maxWp);

                if (res.WaypointCount > 0)
                {
                    int endPos = Interlocked.Add(ref *WaypointCursorPtr, res.WaypointCount);
                    int offset = endPos - res.WaypointCount;
                    if (offset + res.WaypointCount <= WaypointArenaCapacity)
                    {
                        UnsafeUtility.MemCpy(
                            WaypointArena + offset,
                            localBuf,
                            (long)res.WaypointCount * sizeof(float3));
                        res.WaypointOffset = offset;
                    }
                    else
                    {
                        res.WaypointOffset = 0;
                        res.WaypointCount = 0;
                    }
                }

                Outputs[qIdx] = res;
            }

            Workspaces[workerIndex] = ws;
        }
    }

    /// <summary>
    /// Burst 多线程 3D 立体空域批量寻路 Job。
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    internal unsafe struct PathBatch3DJob : IJobParallelFor
    {
        [ReadOnly] public PathVolume3DData Volume;
        [ReadOnly] public NativeArray<PathQuery> Queries;
        [NativeDisableParallelForRestriction] public NativeArray<SolverWorkspace> Workspaces;
        [NativeDisableParallelForRestriction] public NativeArray<PathSolveOutput> Outputs;
        [NativeDisableUnsafePtrRestriction] public float3* WaypointArena;
        public int WaypointArenaCapacity;
        public int MaxWaypointsPerQuery;
        [NativeDisableUnsafePtrRestriction] public int* NextQueryIndexPtr;
        [NativeDisableUnsafePtrRestriction] public int* WaypointCursorPtr;

        public void Execute(int workerIndex)
        {
            SolverWorkspace ws = Workspaces[workerIndex];
            int queryCount = Queries.Length;
            float3* localBuf = stackalloc float3[256];

            while (true)
            {
                int qIdx = Interlocked.Increment(ref *NextQueryIndexPtr) - 1;
                if (qIdx >= queryCount) break;

                int maxWp = math.min(256, MaxWaypointsPerQuery);
                PathQuery q = Queries[qIdx];
                PathSolveOutput res = PathSolver3D.Solve(in Volume, in q, ref ws, localBuf, maxWp);

                if (res.WaypointCount > 0)
                {
                    int endPos = Interlocked.Add(ref *WaypointCursorPtr, res.WaypointCount);
                    int offset = endPos - res.WaypointCount;
                    if (offset + res.WaypointCount <= WaypointArenaCapacity)
                    {
                        UnsafeUtility.MemCpy(
                            WaypointArena + offset,
                            localBuf,
                            (long)res.WaypointCount * sizeof(float3));
                        res.WaypointOffset = offset;
                    }
                    else
                    {
                        res.WaypointOffset = 0;
                        res.WaypointCount = 0;
                    }
                }

                Outputs[qIdx] = res;
            }

            Workspaces[workerIndex] = ws;
        }
    }
}
