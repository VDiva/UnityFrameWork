using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 非托管单次寻路输出元数据（Burst Job 与主线程共享）。
    /// </summary>
    internal struct PathSolveOutput
    {
        public PathStatus Status;
        public int WaypointOffset;
        public int WaypointCount;
        public float TotalCost;
        public int VisitedNodes;
        public int3 StartCoord;
        public int3 EndCoord;
    }

    /// <summary>
    /// 高性能 2D（XZ / XY）双域寻路内核：
    /// 1) 直线 Supercover LOS 零展开快路径
    /// 2) 迭代式无递归 JPS（无 Penalty、8 方向、不切角自动启用）
    /// 3) 二叉堆 A*（支持 4/8 方向、切角、Penalty、最大节点限制、部分路径）
    /// 4) 2D Supercover 视线平滑 / 拐点压缩 / 全格子输出
    /// </summary>
    internal static unsafe partial class PathSolver2D
    {
        private const uint CostStraight = 1000;
        private const uint CostDiagonal = 1414;

        public static PathSolveOutput Solve(
            in PathGridData grid,
            in PathQuery query,
            ref SolverWorkspace ws,
            float3* outWaypoints,
            int maxWaypoints)
        {
            var output = new PathSolveOutput { Status = PathStatus.InvalidGrid };
            if (!grid.Cells.IsCreated || grid.Width <= 0 || grid.Height <= 0)
                return output;
            if (ws.NodeCapacity < grid.CellCount)
                return output;

            PathOptions opt = query.Options;
            float3 startWorld = query.Start;
            float3 endWorld = query.End;
            int2 start = grid.WorldToCell(startWorld);
            int2 end = grid.WorldToCell(endWorld);
            uint tagMask = opt.WalkableTagMask == 0 ? uint.MaxValue : opt.WalkableTagMask;
            MovementDomain domain = opt.Domain;
            int maxSnap = math.max(0, opt.MaxSnapRadius);

            bool startSnapped = false;
            if (!grid.InBounds(start.x, start.y))
            {
                if (!opt.SnapUnwalkableStart || !TrySnapToWalkable(in grid, start, maxSnap, tagMask, domain, out start))
                {
                    output.Status = PathStatus.StartOutOfBounds;
                    return output;
                }
                startSnapped = true;
            }
            else if (!grid.IsWalkable(start.x, start.y, tagMask, domain))
            {
                if (!opt.SnapUnwalkableStart || !TrySnapToWalkable(in grid, start, maxSnap, tagMask, domain, out start))
                {
                    output.Status = PathStatus.StartOutOfBounds;
                    return output;
                }
                startSnapped = true;
            }

            bool endSnapped = false;
            if (!grid.InBounds(end.x, end.y))
            {
                if (!opt.SnapUnwalkableEnd || !TrySnapToWalkable(in grid, end, maxSnap, tagMask, domain, out end))
                {
                    output.Status = PathStatus.EndOutOfBounds;
                    return output;
                }
                endSnapped = true;
            }
            else if (!grid.IsWalkable(end.x, end.y, tagMask, domain))
            {
                if (!opt.SnapUnwalkableEnd || !TrySnapToWalkable(in grid, end, maxSnap, tagMask, domain, out end))
                {
                    output.Status = PathStatus.EndOutOfBounds;
                    return output;
                }
                endSnapped = true;
            }

            output.StartCoord = new int3(start.x, start.y, 0);
            output.EndCoord = new int3(end.x, end.y, 0);

            float3 startPoint = startSnapped
                ? grid.CellToWorld(start.x, start.y, domain, opt.FlightAltitudeOffset)
                : ResolveExactPoint(in grid, start, startWorld, domain, opt.FlightAltitudeOffset);
            float3 endPoint = endSnapped
                ? grid.CellToWorld(end.x, end.y, domain, opt.FlightAltitudeOffset)
                : ResolveExactPoint(in grid, end, endWorld, domain, opt.FlightAltitudeOffset);

            if (start.x == end.x && start.y == end.y)
            {
                output.Status = PathStatus.Success;
                output.TotalCost = 0f;
                output.VisitedNodes = 1;
                if (maxWaypoints > 0)
                {
                    outWaypoints[0] = endPoint;
                    output.WaypointCount = 1;
                }
                return output;
            }

            // 快路径：无 Penalty 且开启平滑时，直接 2D Supercover LOS 检测
            if (opt.OutputMode == PathOutputMode.SmoothedWaypoints &&
                grid.HasPenalties == 0 &&
                HasLineOfSight(in grid, start.x, start.y, end.x, end.y, tagMask, domain, !opt.CutCorners))
            {
                output.Status = PathStatus.Success;
                output.TotalCost = math.distance(startPoint, endPoint);
                output.VisitedNodes = 2;
                int count = 0;
                if (count < maxWaypoints) outWaypoints[count++] = startPoint;
                if (count < maxWaypoints) outWaypoints[count++] = endPoint;
                output.WaypointCount = count;
                return output;
            }

            bool canUseJps = (opt.Algorithm == PathAlgorithm.Auto || opt.Algorithm == PathAlgorithm.JPS)
                             && opt.AllowDiagonal && !opt.CutCorners && grid.HasPenalties == 0;

            int visited = 0;
            int goalIdx = grid.ToIndex(end.x, end.y);
            int startIdx = grid.ToIndex(start.x, start.y);
            int targetIdx = -1;
            bool usedJps = false;
            bool reachedGoal = false;

            if (canUseJps)
            {
                reachedGoal = RunJps(in grid, start, end, tagMask, domain, opt.MaxExpandedNodes, ref ws, out visited);
                if (reachedGoal)
                {
                    targetIdx = goalIdx;
                    usedJps = true;
                }
                else if (opt.AllowPartialPath)
                {
                    // JPS 未达终点时回退到 A*，获取精确最近可达点
                    reachedGoal = RunAStar(in grid, start, end, in opt, tagMask, ref ws, out visited, out targetIdx);
                    usedJps = false;
                }
            }
            else
            {
                reachedGoal = RunAStar(in grid, start, end, in opt, tagMask, ref ws, out visited, out targetIdx);
            }

            output.VisitedNodes = visited;
            if (!reachedGoal)
            {
                if (!opt.AllowPartialPath || targetIdx < 0 || targetIdx == startIdx)
                {
                    output.Status = PathStatus.Unreachable;
                    return output;
                }
                output.Status = PathStatus.PartialPath;
                int2 partialCell = grid.FromIndex(targetIdx);
                output.EndCoord = new int3(partialCell.x, partialCell.y, 0);
                endPoint = grid.CellToWorld(partialCell.x, partialCell.y, domain, opt.FlightAltitudeOffset);
            }
            else
            {
                output.Status = PathStatus.Success;
            }

            output.TotalCost = (ws.Nodes[targetIdx].GCost / 1000f) * grid.CellSize;
            output.WaypointCount = BuildWaypoints(
                in grid, ref ws, startIdx, targetIdx, usedJps,
                startPoint, endPoint, in opt, tagMask, outWaypoints, maxWaypoints);
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveExactPoint(
            in PathGridData grid, int2 cell, float3 worldPos, MovementDomain domain, float altitudeOffset)
        {
            float3 center = grid.CellToWorld(cell.x, cell.y, domain, altitudeOffset);
            return grid.Plane == GridPlane.XZ
                ? new float3(worldPos.x, center.y, worldPos.z)
                : new float3(worldPos.x, worldPos.y, center.z);
        }

        private static bool TrySnapToWalkable(
            in PathGridData grid, int2 origin, int maxRadius, uint tagMask, MovementDomain domain, out int2 snapped)
        {
            int cx = math.clamp(origin.x, 0, grid.Width - 1);
            int cy = math.clamp(origin.y, 0, grid.Height - 1);
            if (grid.IsWalkable(cx, cy, tagMask, domain))
            {
                snapped = new int2(cx, cy);
                return true;
            }

            int bestDistSq = int.MaxValue;
            int2 best = default;
            bool found = false;

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    CheckSnapCandidate(in grid, cx + dx, cy - r, origin, tagMask, domain, ref found, ref bestDistSq, ref best);
                    CheckSnapCandidate(in grid, cx + dx, cy + r, origin, tagMask, domain, ref found, ref bestDistSq, ref best);
                }
                for (int dy = -r + 1; dy <= r - 1; dy++)
                {
                    CheckSnapCandidate(in grid, cx - r, cy + dy, origin, tagMask, domain, ref found, ref bestDistSq, ref best);
                    CheckSnapCandidate(in grid, cx + r, cy + dy, origin, tagMask, domain, ref found, ref bestDistSq, ref best);
                }
                if (found)
                {
                    snapped = best;
                    return true;
                }
            }

            snapped = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckSnapCandidate(
            in PathGridData grid, int x, int y, int2 origin, uint tagMask, MovementDomain domain,
            ref bool found, ref int bestDistSq, ref int2 best)
        {
            if (!grid.IsWalkable(x, y, tagMask, domain)) return;
            int dx = x - origin.x;
            int dy = y - origin.y;
            int d2 = dx * dx + dy * dy;
            if (d2 < bestDistSq)
            {
                bestDistSq = d2;
                best = new int2(x, y);
                found = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Heuristic(int x, int y, int ex, int ey, bool allowDiagonal)
        {
            int dx = math.abs(x - ex);
            int dy = math.abs(y - ey);
            if (!allowDiagonal)
                return (uint)(dx + dy) * CostStraight;
            int min = math.min(dx, dy);
            int max = math.max(dx, dy);
            return (uint)min * CostDiagonal + (uint)(max - min) * CostStraight;
        }

        private static bool RunAStar(
            in PathGridData grid, int2 start, int2 end, in PathOptions opt, uint tagMask,
            ref SolverWorkspace ws, out int visited, out int bestNodeIdx)
        {
            ws.BeginSearch();
            int startIdx = grid.ToIndex(start.x, start.y);
            int endIdx = grid.ToIndex(end.x, end.y);
            bool diag = opt.AllowDiagonal;
            bool cut = opt.CutCorners;
            MovementDomain domain = opt.Domain;
            int maxNodes = opt.MaxExpandedNodes;

            uint startH = Heuristic(start.x, start.y, end.x, end.y, diag);
            ws.PushNew(startIdx, 0, startH, -1);

            visited = 0;
            bestNodeIdx = startIdx;
            uint bestH = startH;
            uint bestG = 0;

            while (ws.HeapCount > 0)
            {
                int curIdx = ws.PopMin();
                visited++;
                if (curIdx == endIdx)
                {
                    bestNodeIdx = endIdx;
                    return true;
                }

                uint curG = ws.Nodes[curIdx].GCost;
                int2 cur = grid.FromIndex(curIdx);
                uint curH = Heuristic(cur.x, cur.y, end.x, end.y, diag);
                if (curH < bestH || (curH == bestH && curG < bestG))
                {
                    bestH = curH;
                    bestG = curG;
                    bestNodeIdx = curIdx;
                }

                if (maxNodes > 0 && visited >= maxNodes)
                    return false;

                // 4 方向
                TryExpand2D(in grid, curIdx, cur.x + 1, cur.y, curG, CostStraight, end, diag, tagMask, domain, ref ws);
                TryExpand2D(in grid, curIdx, cur.x - 1, cur.y, curG, CostStraight, end, diag, tagMask, domain, ref ws);
                TryExpand2D(in grid, curIdx, cur.x, cur.y + 1, curG, CostStraight, end, diag, tagMask, domain, ref ws);
                TryExpand2D(in grid, curIdx, cur.x, cur.y - 1, curG, CostStraight, end, diag, tagMask, domain, ref ws);

                if (diag)
                {
                    bool r = grid.IsWalkable(cur.x + 1, cur.y, tagMask, domain);
                    bool l = grid.IsWalkable(cur.x - 1, cur.y, tagMask, domain);
                    bool u = grid.IsWalkable(cur.x, cur.y + 1, tagMask, domain);
                    bool d = grid.IsWalkable(cur.x, cur.y - 1, tagMask, domain);

                    if (cut ? (r || u) : (r && u))
                        TryExpand2D(in grid, curIdx, cur.x + 1, cur.y + 1, curG, CostDiagonal, end, true, tagMask, domain, ref ws);
                    if (cut ? (l || u) : (l && u))
                        TryExpand2D(in grid, curIdx, cur.x - 1, cur.y + 1, curG, CostDiagonal, end, true, tagMask, domain, ref ws);
                    if (cut ? (r || d) : (r && d))
                        TryExpand2D(in grid, curIdx, cur.x + 1, cur.y - 1, curG, CostDiagonal, end, true, tagMask, domain, ref ws);
                    if (cut ? (l || d) : (l && d))
                        TryExpand2D(in grid, curIdx, cur.x - 1, cur.y - 1, curG, CostDiagonal, end, true, tagMask, domain, ref ws);
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryExpand2D(
            in PathGridData grid, int parentIdx, int nx, int ny, uint parentG, uint stepCost,
            int2 end, bool diag, uint tagMask, MovementDomain domain, ref SolverWorkspace ws)
        {
            if (!grid.IsWalkable(nx, ny, tagMask, domain)) return;
            int nIdx = grid.ToIndex(nx, ny);
            byte state = ws.GetState(nIdx);
            if (state == 2) return;

            uint penalty = grid.HasPenalties != 0 ? (uint)grid.Cells[nIdx].Penalty * 1000u : 0u;
            uint newG = parentG + stepCost + penalty;
            if (state == 0)
            {
                ws.PushNew(nIdx, newG, Heuristic(nx, ny, end.x, end.y, diag), parentIdx);
            }
            else if (newG < ws.Nodes[nIdx].GCost)
            {
                ws.DecreaseKey(nIdx, newG, Heuristic(nx, ny, end.x, end.y, diag), parentIdx);
            }
        }
    }
}
