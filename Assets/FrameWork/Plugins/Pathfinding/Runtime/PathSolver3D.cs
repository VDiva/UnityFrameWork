using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 3D 立体空域体素寻路内核：
    /// 1) 3D Supercover 体素视线检测快路径
    /// 2) 26 邻域 3D 体积 A*（正交 1000 / 面对角 1414 / 体对角 1732，支持严格不切角与空域 Penalty）
    /// 3) 3D 视线弦拉直 / 拐点压缩 / 全体素输出
    /// </summary>
    internal static unsafe class PathSolver3D
    {
        private const uint CostCardinal = 1000;
        private const uint CostFaceDiag = 1414;
        private const uint CostSpaceDiag = 1732;

        public static PathSolveOutput Solve(
            in PathVolume3DData volume,
            in PathQuery query,
            ref SolverWorkspace ws,
            float3* outWaypoints,
            int maxWaypoints)
        {
            var output = new PathSolveOutput { Status = PathStatus.InvalidGrid };
            if (!volume.Voxels.IsCreated || volume.SizeX <= 0 || volume.SizeY <= 0 || volume.SizeZ <= 0)
                return output;
            if (ws.NodeCapacity < volume.VoxelCount)
                return output;

            PathOptions opt = query.Options;
            float3 startWorld = query.Start;
            float3 endWorld = query.End;
            int3 start = volume.WorldToVoxel(startWorld);
            int3 end = volume.WorldToVoxel(endWorld);
            uint tagMask = opt.WalkableTagMask == 0 ? uint.MaxValue : opt.WalkableTagMask;
            int maxSnap = math.max(0, opt.MaxSnapRadius);

            bool startSnapped = false;
            if (!volume.InBounds(start.x, start.y, start.z) || !volume.IsFlyable(start.x, start.y, start.z, tagMask))
            {
                if (!opt.SnapUnwalkableStart || !TrySnapToFlyable(in volume, start, maxSnap, tagMask, out start))
                {
                    output.Status = PathStatus.StartOutOfBounds;
                    return output;
                }
                startSnapped = true;
            }

            bool endSnapped = false;
            if (!volume.InBounds(end.x, end.y, end.z) || !volume.IsFlyable(end.x, end.y, end.z, tagMask))
            {
                if (!opt.SnapUnwalkableEnd || !TrySnapToFlyable(in volume, end, maxSnap, tagMask, out end))
                {
                    output.Status = PathStatus.EndOutOfBounds;
                    return output;
                }
                endSnapped = true;
            }

            output.StartCoord = start;
            output.EndCoord = end;

            float3 startPoint = startSnapped ? volume.VoxelToWorld(start.x, start.y, start.z) : startWorld;
            float3 endPoint = endSnapped ? volume.VoxelToWorld(end.x, end.y, end.z) : endWorld;

            if (start.x == end.x && start.y == end.y && start.z == end.z)
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

            // 快路径：无 Penalty 且开启平滑时，3D 直线无障碍直接返回 2 个路点
            if (opt.OutputMode == PathOutputMode.SmoothedWaypoints &&
                volume.HasPenalties == 0 &&
                HasLineOfSight3D(in volume, start, end, tagMask))
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

            bool reachedGoal = RunAStar3D(in volume, start, end, in opt, tagMask, ref ws, out int visited, out int targetIdx);
            int startIdx = volume.ToIndex(start.x, start.y, start.z);
            output.VisitedNodes = visited;

            if (!reachedGoal)
            {
                if (!opt.AllowPartialPath || targetIdx < 0 || targetIdx == startIdx)
                {
                    output.Status = PathStatus.Unreachable;
                    return output;
                }
                output.Status = PathStatus.PartialPath;
                int3 p = volume.FromIndex(targetIdx);
                output.EndCoord = p;
                endPoint = volume.VoxelToWorld(p.x, p.y, p.z);
            }
            else
            {
                output.Status = PathStatus.Success;
            }

            output.TotalCost = (ws.Nodes[targetIdx].GCost / 1000f) * volume.VoxelSize;
            output.WaypointCount = BuildWaypoints3D(
                in volume, ref ws, startIdx, targetIdx, startPoint, endPoint, in opt, tagMask, outWaypoints, maxWaypoints);
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Heuristic3D(int3 a, int3 b, bool diag)
        {
            int dx = math.abs(a.x - b.x);
            int dy = math.abs(a.y - b.y);
            int dz = math.abs(a.z - b.z);
            if (!diag) return (uint)(dx + dy + dz) * CostCardinal;

            int dMin = math.min(dx, math.min(dy, dz));
            int dMax = math.max(dx, math.max(dy, dz));
            int dMid = dx + dy + dz - dMin - dMax;
            return (uint)dMin * CostSpaceDiag + (uint)(dMid - dMin) * CostFaceDiag + (uint)(dMax - dMid) * CostCardinal;
        }

        private static bool TrySnapToFlyable(
            in PathVolume3DData volume, int3 origin, int maxRadius, uint tagMask, out int3 snapped)
        {
            int cx = math.clamp(origin.x, 0, volume.SizeX - 1);
            int cy = math.clamp(origin.y, 0, volume.SizeY - 1);
            int cz = math.clamp(origin.z, 0, volume.SizeZ - 1);
            if (volume.IsFlyable(cx, cy, cz, tagMask))
            {
                snapped = new int3(cx, cy, cz);
                return true;
            }

            int bestDistSq = int.MaxValue;
            int3 best = default;
            bool found = false;

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int y = cy + dy;
                    if ((uint)y >= (uint)volume.SizeY) continue;
                    for (int dz = -r; dz <= r; dz++)
                    {
                        int z = cz + dz;
                        if ((uint)z >= (uint)volume.SizeZ) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (math.abs(dx) != r && math.abs(dy) != r && math.abs(dz) != r) continue;
                            int x = cx + dx;
                            if (!volume.IsFlyable(x, y, z, tagMask)) continue;
                            int ox = x - origin.x;
                            int oy = y - origin.y;
                            int oz = z - origin.z;
                            int d2 = ox * ox + oy * oy + oz * oz;
                            if (d2 < bestDistSq)
                            {
                                bestDistSq = d2;
                                best = new int3(x, y, z);
                                found = true;
                            }
                        }
                    }
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

        private static bool RunAStar3D(
            in PathVolume3DData volume, int3 start, int3 end, in PathOptions opt, uint tagMask,
            ref SolverWorkspace ws, out int visited, out int bestNodeIdx)
        {
            ws.BeginSearch();
            int startIdx = volume.ToIndex(start.x, start.y, start.z);
            int endIdx = volume.ToIndex(end.x, end.y, end.z);
            bool diag = opt.AllowDiagonal;
            bool cut = opt.CutCorners;
            int maxNodes = opt.MaxExpandedNodes;

            uint startH = Heuristic3D(start, end, diag);
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
                int3 cur = volume.FromIndex(curIdx);
                uint curH = Heuristic3D(cur, end, diag);
                if (curH < bestH || (curH == bestH && curG < bestG))
                {
                    bestH = curH;
                    bestG = curG;
                    bestNodeIdx = curIdx;
                }

                if (maxNodes > 0 && visited >= maxNodes)
                    return false;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int axes = math.abs(dx) + math.abs(dy) + math.abs(dz);
                            if (axes == 0) continue;
                            if (!diag && axes > 1) continue;

                            int nx = cur.x + dx;
                            int ny = cur.y + dy;
                            int nz = cur.z + dz;
                            if (!volume.IsFlyable(nx, ny, nz, tagMask)) continue;

                            // 不切角校验：面对角检查 2 个正交邻格；体对角检查 3 个正交邻格与 3 个面对角邻格
                            if (!cut && axes > 1)
                            {
                                if (axes == 2)
                                {
                                    if (dx != 0 && !volume.IsFlyable(cur.x + dx, cur.y, cur.z, tagMask)) continue;
                                    if (dy != 0 && !volume.IsFlyable(cur.x, cur.y + dy, cur.z, tagMask)) continue;
                                    if (dz != 0 && !volume.IsFlyable(cur.x, cur.y, cur.z + dz, tagMask)) continue;
                                }
                                else // axes == 3
                                {
                                    if (!volume.IsFlyable(cur.x + dx, cur.y, cur.z, tagMask) ||
                                        !volume.IsFlyable(cur.x, cur.y + dy, cur.z, tagMask) ||
                                        !volume.IsFlyable(cur.x, cur.y, cur.z + dz, tagMask) ||
                                        !volume.IsFlyable(cur.x + dx, cur.y + dy, cur.z, tagMask) ||
                                        !volume.IsFlyable(cur.x + dx, cur.y, cur.z + dz, tagMask) ||
                                        !volume.IsFlyable(cur.x, cur.y + dy, cur.z + dz, tagMask))
                                        continue;
                                }
                            }

                            int nIdx = volume.ToIndex(nx, ny, nz);
                            byte state = ws.GetState(nIdx);
                            if (state == 2) continue;

                            uint stepCost = axes == 1 ? CostCardinal : (axes == 2 ? CostFaceDiag : CostSpaceDiag);
                            uint penalty = volume.HasPenalties != 0 ? (uint)volume.Voxels[nIdx].Penalty * 1000u : 0u;
                            uint newG = curG + stepCost + penalty;
                            uint h = Heuristic3D(new int3(nx, ny, nz), end, diag);

                            if (state == 0)
                                ws.PushNew(nIdx, newG, h, curIdx);
                            else if (newG < ws.Nodes[nIdx].GCost)
                                ws.DecreaseKey(nIdx, newG, h, curIdx);
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 3D 体素视线检测（Amanatides &amp; Woo 3D DDA，保证穿透的所有体素均可飞行）。
        /// </summary>
        public static bool HasLineOfSight3D(in PathVolume3DData volume, int3 a, int3 b, uint tagMask)
        {
            if (!volume.IsFlyable(a.x, a.y, a.z, tagMask) || !volume.IsFlyable(b.x, b.y, b.z, tagMask))
                return false;

            int x = a.x, y = a.y, z = a.z;
            int dx = b.x - a.x;
            int dy = b.y - a.y;
            int dz = b.z - a.z;
            int sx = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int sy = dy > 0 ? 1 : (dy < 0 ? -1 : 0);
            int sz = dz > 0 ? 1 : (dz < 0 ? -1 : 0);

            float tDeltaX = dx != 0 ? math.abs(1f / dx) : 1e30f;
            float tDeltaY = dy != 0 ? math.abs(1f / dy) : 1e30f;
            float tDeltaZ = dz != 0 ? math.abs(1f / dz) : 1e30f;

            float tMaxX = dx != 0 ? tDeltaX * 0.5f : 1e30f;
            float tMaxY = dy != 0 ? tDeltaY * 0.5f : 1e30f;
            float tMaxZ = dz != 0 ? tDeltaZ * 0.5f : 1e30f;

            int maxSteps = math.abs(dx) + math.abs(dy) + math.abs(dz) + 1;
            for (int i = 0; i < maxSteps && (x != b.x || y != b.y || z != b.z); i++)
            {
                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    x += sx;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY <= tMaxZ)
                {
                    y += sy;
                    tMaxY += tDeltaY;
                }
                else
                {
                    z += sz;
                    tMaxZ += tDeltaZ;
                }

                if (!volume.IsFlyable(x, y, z, tagMask))
                    return false;
            }
            return true;
        }

        private static int BuildWaypoints3D(
            in PathVolume3DData volume, ref SolverWorkspace ws, int startIdx, int targetIdx,
            float3 startPoint, float3 endPoint, in PathOptions opt, uint tagMask,
            float3* outWaypoints, int maxWaypoints)
        {
            if (maxWaypoints <= 0) return 0;

            int rawCount = 0;
            int cur = targetIdx;
            while (cur >= 0 && rawCount < ws.PathCapacity)
            {
                ws.RawPath[rawCount++] = cur;
                if (cur == startIdx) break;
                cur = ws.Nodes[cur].ParentIndex;
            }
            for (int i = 0, j = rawCount - 1; i < j; i++, j--)
            {
                int tmp = ws.RawPath[i];
                ws.RawPath[i] = ws.RawPath[j];
                ws.RawPath[j] = tmp;
            }

            if (opt.OutputMode == PathOutputMode.FullGridCells)
            {
                int n = math.min(rawCount, maxWaypoints);
                for (int i = 0; i < n; i++)
                {
                    int3 c = volume.FromIndex(ws.RawPath[i]);
                    outWaypoints[i] = i == 0 ? startPoint : (i == rawCount - 1 ? endPoint : volume.VoxelToWorld(c.x, c.y, c.z));
                }
                return n;
            }

            if (opt.OutputMode == PathOutputMode.SmoothedWaypoints && volume.HasPenalties == 0 && rawCount > 2)
            {
                int outCount = 0;
                int anchor = 0;
                outWaypoints[outCount++] = startPoint;

                while (anchor < rawCount - 1 && outCount < maxWaypoints)
                {
                    int3 a = volume.FromIndex(ws.RawPath[anchor]);
                    int best = anchor + 1;
                    for (int probe = rawCount - 1; probe > anchor + 1; probe--)
                    {
                        int3 p = volume.FromIndex(ws.RawPath[probe]);
                        if (HasLineOfSight3D(in volume, a, p, tagMask))
                        {
                            best = probe;
                            break;
                        }
                    }
                    anchor = best;
                    if (anchor == rawCount - 1)
                        outWaypoints[outCount++] = endPoint;
                    else
                    {
                        int3 c = volume.FromIndex(ws.RawPath[anchor]);
                        outWaypoints[outCount++] = volume.VoxelToWorld(c.x, c.y, c.z);
                    }
                }
                return outCount;
            }

            int count = 0;
            outWaypoints[count++] = startPoint;
            int3 prevDir = new int3(int.MaxValue, int.MaxValue, int.MaxValue);
            for (int i = 1; i < rawCount && count < maxWaypoints; i++)
            {
                int3 prev = volume.FromIndex(ws.RawPath[i - 1]);
                int3 curr = volume.FromIndex(ws.RawPath[i]);
                int3 dir = math.clamp(curr - prev, -1, 1);
                if (i > 1 && (dir.x != prevDir.x || dir.y != prevDir.y || dir.z != prevDir.z))
                {
                    outWaypoints[count++] = volume.VoxelToWorld(prev.x, prev.y, prev.z);
                }
                prevDir = dir;
            }
            if (count < maxWaypoints)
                outWaypoints[count++] = endPoint;
            return count;
        }
    }
}
