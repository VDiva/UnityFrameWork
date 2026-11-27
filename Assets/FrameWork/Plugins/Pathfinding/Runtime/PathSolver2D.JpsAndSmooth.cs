using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace FrameWork.Pathfinding
{
    internal static unsafe partial class PathSolver2D
    {
        /// <summary>
        /// 迭代式（无递归）Jump Point Search，严格遵循不切角规则（CutCorners == false），
        /// 与 8 方向无 Penalty A* 保持完全等价的最短路径代价。
        /// </summary>
        private static bool RunJps(
            in PathGridData grid, int2 start, int2 end, uint tagMask, MovementDomain domain,
            int maxNodes, ref SolverWorkspace ws, out int visited)
        {
            ws.BeginSearch();
            int startIdx = grid.ToIndex(start.x, start.y);
            int endIdx = grid.ToIndex(end.x, end.y);
            ws.PushNew(startIdx, 0, Heuristic(start.x, start.y, end.x, end.y, true), -1);
            visited = 0;

            while (ws.HeapCount > 0)
            {
                int curIdx = ws.PopMin();
                visited++;
                if (curIdx == endIdx) return true;
                if (maxNodes > 0 && visited >= maxNodes) return false;

                int2 cur = grid.FromIndex(curIdx);
                uint curG = ws.Nodes[curIdx].GCost;
                int parentIdx = ws.Nodes[curIdx].ParentIndex;

                if (parentIdx < 0)
                {
                    // 起点：展开全部 8 个方向
                    TryJump(in grid, curIdx, cur, 1, 0, curG, end, tagMask, domain, ref ws);
                    TryJump(in grid, curIdx, cur, -1, 0, curG, end, tagMask, domain, ref ws);
                    TryJump(in grid, curIdx, cur, 0, 1, curG, end, tagMask, domain, ref ws);
                    TryJump(in grid, curIdx, cur, 0, -1, curG, end, tagMask, domain, ref ws);
                    TryJump(in grid, curIdx, cur, 1, 1, curG, end, tagMask, domain, ref ws);
                    TryJump(in grid, curIdx, cur, -1, 1, curG, end, tagMask, domain, ref ws);
                    TryJump(in grid, curIdx, cur, 1, -1, curG, end, tagMask, domain, ref ws);
                    TryJump(in grid, curIdx, cur, -1, -1, curG, end, tagMask, domain, ref ws);
                    continue;
                }

                int2 parent = grid.FromIndex(parentIdx);
                int dx = math.clamp(cur.x - parent.x, -1, 1);
                int dy = math.clamp(cur.y - parent.y, -1, 1);

                if (dx != 0 && dy != 0)
                {
                    // 对角方向自然邻居：水平分量、垂直分量、以及对角延续
                    bool wX = grid.IsWalkable(cur.x + dx, cur.y, tagMask, domain);
                    bool wY = grid.IsWalkable(cur.x, cur.y + dy, tagMask, domain);
                    if (wX) TryJump(in grid, curIdx, cur, dx, 0, curG, end, tagMask, domain, ref ws);
                    if (wY) TryJump(in grid, curIdx, cur, 0, dy, curG, end, tagMask, domain, ref ws);
                    if (wX && wY) TryJump(in grid, curIdx, cur, dx, dy, curG, end, tagMask, domain, ref ws);
                    // 严格不切角规则下，对角移动不存在额外的侧后方强迫邻居
                }
                else if (dx != 0)
                {
                    // 水平方向
                    bool fwd = grid.IsWalkable(cur.x + dx, cur.y, tagMask, domain);
                    if (fwd)
                    {
                        TryJump(in grid, curIdx, cur, dx, 0, curG, end, tagMask, domain, ref ws);
                        // 强迫邻居（上方被阻挡但右上方通畅 -> 允许向 (dx, +1) 对角跳，前提是 (cur.x, cur.y+1) 通畅且 (cur.x-dx, cur.y+1) 阻挡）
                        if (!grid.IsWalkable(cur.x - dx, cur.y + 1, tagMask, domain) &&
                            grid.IsWalkable(cur.x, cur.y + 1, tagMask, domain))
                            TryJump(in grid, curIdx, cur, dx, 1, curG, end, tagMask, domain, ref ws);
                        if (!grid.IsWalkable(cur.x - dx, cur.y - 1, tagMask, domain) &&
                            grid.IsWalkable(cur.x, cur.y - 1, tagMask, domain))
                            TryJump(in grid, curIdx, cur, dx, -1, curG, end, tagMask, domain, ref ws);
                    }
                    // 垂直方向的强迫邻居（严格不切角：阻挡在 (cur.x-dx, cur.y±1) 时，垂直方向 (0, ±1) 也可能是绕角起点）
                    if (!grid.IsWalkable(cur.x - dx, cur.y + 1, tagMask, domain) &&
                        grid.IsWalkable(cur.x, cur.y + 1, tagMask, domain))
                        TryJump(in grid, curIdx, cur, 0, 1, curG, end, tagMask, domain, ref ws);
                    if (!grid.IsWalkable(cur.x - dx, cur.y - 1, tagMask, domain) &&
                        grid.IsWalkable(cur.x, cur.y - 1, tagMask, domain))
                        TryJump(in grid, curIdx, cur, 0, -1, curG, end, tagMask, domain, ref ws);
                }
                else
                {
                    // 垂直方向
                    bool fwd = grid.IsWalkable(cur.x, cur.y + dy, tagMask, domain);
                    if (fwd)
                    {
                        TryJump(in grid, curIdx, cur, 0, dy, curG, end, tagMask, domain, ref ws);
                        if (!grid.IsWalkable(cur.x + 1, cur.y - dy, tagMask, domain) &&
                            grid.IsWalkable(cur.x + 1, cur.y, tagMask, domain))
                            TryJump(in grid, curIdx, cur, 1, dy, curG, end, tagMask, domain, ref ws);
                        if (!grid.IsWalkable(cur.x - 1, cur.y - dy, tagMask, domain) &&
                            grid.IsWalkable(cur.x - 1, cur.y, tagMask, domain))
                            TryJump(in grid, curIdx, cur, -1, dy, curG, end, tagMask, domain, ref ws);
                    }
                    if (!grid.IsWalkable(cur.x + 1, cur.y - dy, tagMask, domain) &&
                        grid.IsWalkable(cur.x + 1, cur.y, tagMask, domain))
                        TryJump(in grid, curIdx, cur, 1, 0, curG, end, tagMask, domain, ref ws);
                    if (!grid.IsWalkable(cur.x - 1, cur.y - dy, tagMask, domain) &&
                        grid.IsWalkable(cur.x - 1, cur.y, tagMask, domain))
                        TryJump(in grid, curIdx, cur, -1, 0, curG, end, tagMask, domain, ref ws);
                }
            }

            return false;
        }

        private static void TryJump(
            in PathGridData grid, int parentIdx, int2 from, int dx, int dy, uint parentG,
            int2 end, uint tagMask, MovementDomain domain, ref SolverWorkspace ws)
        {
            int jx, jy;
            uint dist;
            if (!JumpIterative(in grid, from.x, from.y, dx, dy, end, tagMask, domain, out jx, out jy, out dist))
                return;

            int jIdx = grid.ToIndex(jx, jy);
            byte state = ws.GetState(jIdx);
            if (state == 2) return;

            uint newG = parentG + dist;
            if (state == 0)
            {
                ws.PushNew(jIdx, newG, Heuristic(jx, jy, end.x, end.y, true), parentIdx);
            }
            else if (newG < ws.Nodes[jIdx].GCost)
            {
                ws.DecreaseKey(jIdx, newG, Heuristic(jx, jy, end.x, end.y, true), parentIdx);
            }
        }

        private static bool JumpIterative(
            in PathGridData grid, int sx, int sy, int dx, int dy, int2 end,
            uint tagMask, MovementDomain domain, out int jx, out int jy, out uint cost)
        {
            int x = sx;
            int y = sy;
            cost = 0;

            if (dx != 0 && dy != 0)
            {
                while (true)
                {
                    if (!grid.IsWalkable(x + dx, y, tagMask, domain) ||
                        !grid.IsWalkable(x, y + dy, tagMask, domain))
                    {
                        jx = 0; jy = 0;
                        return false;
                    }
                    x += dx;
                    y += dy;
                    if (!grid.IsWalkable(x, y, tagMask, domain))
                    {
                        jx = 0; jy = 0;
                        return false;
                    }
                    cost += CostDiagonal;
                    if (x == end.x && y == end.y)
                    {
                        jx = x; jy = y;
                        return true;
                    }
                    if (JumpStraight(in grid, x, y, dx, 0, end, tagMask, domain, out _, out _, out _) ||
                        JumpStraight(in grid, x, y, 0, dy, end, tagMask, domain, out _, out _, out _))
                    {
                        jx = x; jy = y;
                        return true;
                    }
                }
            }

            return JumpStraight(in grid, sx, sy, dx, dy, end, tagMask, domain, out jx, out jy, out cost);
        }

        private static bool JumpStraight(
            in PathGridData grid, int sx, int sy, int dx, int dy, int2 end,
            uint tagMask, MovementDomain domain, out int jx, out int jy, out uint cost)
        {
            int x = sx;
            int y = sy;
            cost = 0;

            if (dx != 0)
            {
                while (true)
                {
                    x += dx;
                    if (!grid.IsWalkable(x, y, tagMask, domain))
                    {
                        jx = 0; jy = 0;
                        return false;
                    }
                    cost += CostStraight;
                    if (x == end.x && y == end.y)
                    {
                        jx = x; jy = y;
                        return true;
                    }
                    // 严格不切角强迫邻居检测：上一格侧边被挡且当前格侧边通畅
                    if ((!grid.IsWalkable(x - dx, y + 1, tagMask, domain) && grid.IsWalkable(x, y + 1, tagMask, domain)) ||
                        (!grid.IsWalkable(x - dx, y - 1, tagMask, domain) && grid.IsWalkable(x, y - 1, tagMask, domain)))
                    {
                        jx = x; jy = y;
                        return true;
                    }
                }
            }
            else
            {
                while (true)
                {
                    y += dy;
                    if (!grid.IsWalkable(x, y, tagMask, domain))
                    {
                        jx = 0; jy = 0;
                        return false;
                    }
                    cost += CostStraight;
                    if (x == end.x && y == end.y)
                    {
                        jx = x; jy = y;
                        return true;
                    }
                    if ((!grid.IsWalkable(x + 1, y - dy, tagMask, domain) && grid.IsWalkable(x + 1, y, tagMask, domain)) ||
                        (!grid.IsWalkable(x - 1, y - dy, tagMask, domain) && grid.IsWalkable(x - 1, y, tagMask, domain)))
                    {
                        jx = x; jy = y;
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// 2D Supercover 视线检测（穿过网格交点时同时校验两侧相邻格子，杜绝贴墙穿模）。
        /// </summary>
        public static bool HasLineOfSight(
            in PathGridData grid, int x0, int y0, int x1, int y1,
            uint tagMask, MovementDomain domain, bool strictCorners)
        {
            int dx = math.abs(x1 - x0);
            int dy = math.abs(y1 - y0);
            int x = x0;
            int y = y0;
            int sx = x1 > x0 ? 1 : -1;
            int sy = y1 > y0 ? 1 : -1;

            if (!grid.IsWalkable(x, y, tagMask, domain)) return false;

            if (dx >= dy)
            {
                int err = dx;
                int errPrev = dx;
                int ddx = dx << 1;
                int ddy = dy << 1;
                for (int i = 0; i < dx; i++)
                {
                    x += sx;
                    err += ddy;
                    if (err > ddx)
                    {
                        y += sy;
                        err -= ddx;
                        if (err + errPrev < ddx)
                        {
                            if (!grid.IsWalkable(x, y - sy, tagMask, domain)) return false;
                        }
                        else if (err + errPrev > ddx)
                        {
                            if (!grid.IsWalkable(x - sx, y, tagMask, domain)) return false;
                        }
                        else if (strictCorners)
                        {
                            if (!grid.IsWalkable(x, y - sy, tagMask, domain) ||
                                !grid.IsWalkable(x - sx, y, tagMask, domain))
                                return false;
                        }
                    }
                    if (!grid.IsWalkable(x, y, tagMask, domain)) return false;
                    errPrev = err;
                }
            }
            else
            {
                int err = dy;
                int errPrev = dy;
                int ddx = dx << 1;
                int ddy = dy << 1;
                for (int i = 0; i < dy; i++)
                {
                    y += sy;
                    err += ddx;
                    if (err > ddy)
                    {
                        x += sx;
                        err -= ddy;
                        if (err + errPrev < ddy)
                        {
                            if (!grid.IsWalkable(x - sx, y, tagMask, domain)) return false;
                        }
                        else if (err + errPrev > ddy)
                        {
                            if (!grid.IsWalkable(x, y - sy, tagMask, domain)) return false;
                        }
                        else if (strictCorners)
                        {
                            if (!grid.IsWalkable(x - sx, y, tagMask, domain) ||
                                !grid.IsWalkable(x, y - sy, tagMask, domain))
                                return false;
                        }
                    }
                    if (!grid.IsWalkable(x, y, tagMask, domain)) return false;
                    errPrev = err;
                }
            }
            return true;
        }

        private static int BuildWaypoints(
            in PathGridData grid, ref SolverWorkspace ws, int startIdx, int targetIdx, bool usedJps,
            float3 startPoint, float3 endPoint, in PathOptions opt, uint tagMask,
            float3* outWaypoints, int maxWaypoints)
        {
            if (maxWaypoints <= 0) return 0;

            // 回溯 RawPath（逆序）
            int rawCount = 0;
            int cur = targetIdx;
            while (cur >= 0 && rawCount < ws.PathCapacity)
            {
                ws.RawPath[rawCount++] = cur;
                if (cur == startIdx) break;
                cur = ws.Nodes[cur].ParentIndex;
            }
            // 反转 RawPath -> 正序
            for (int i = 0, j = rawCount - 1; i < j; i++, j--)
            {
                int tmp = ws.RawPath[i];
                ws.RawPath[i] = ws.RawPath[j];
                ws.RawPath[j] = tmp;
            }

            int* cells = ws.RawPath;
            int cellCount = rawCount;

            // 如果是 JPS 跳点序列，展开成连续逐格序列以支持高精度视线拉直或全格输出
            if (usedJps && rawCount >= 2 && opt.OutputMode != PathOutputMode.CornerWaypoints)
            {
                int exp = 0;
                ws.ExpandedPath[exp++] = ws.RawPath[0];
                for (int i = 1; i < rawCount && exp < ws.PathCapacity; i++)
                {
                    int2 a = grid.FromIndex(ws.RawPath[i - 1]);
                    int2 b = grid.FromIndex(ws.RawPath[i]);
                    int stepX = math.clamp(b.x - a.x, -1, 1);
                    int stepY = math.clamp(b.y - a.y, -1, 1);
                    int cx = a.x;
                    int cy = a.y;
                    while ((cx != b.x || cy != b.y) && exp < ws.PathCapacity)
                    {
                        cx += stepX;
                        cy += stepY;
                        ws.ExpandedPath[exp++] = grid.ToIndex(cx, cy);
                    }
                }
                cells = ws.ExpandedPath;
                cellCount = exp;
            }

            if (opt.OutputMode == PathOutputMode.FullGridCells)
            {
                int n = math.min(cellCount, maxWaypoints);
                for (int i = 0; i < n; i++)
                {
                    int2 c = grid.FromIndex(cells[i]);
                    outWaypoints[i] = i == 0 ? startPoint : (i == cellCount - 1 ? endPoint : grid.CellToWorld(c.x, c.y, opt.Domain, opt.FlightAltitudeOffset));
                }
                return n;
            }

            if (opt.OutputMode == PathOutputMode.SmoothedWaypoints && grid.HasPenalties == 0 && cellCount > 2)
            {
                int outCount = 0;
                int anchor = 0;
                outWaypoints[outCount++] = startPoint;

                while (anchor < cellCount - 1 && outCount < maxWaypoints)
                {
                    int2 a = grid.FromIndex(cells[anchor]);
                    int best = anchor + 1;
                    // 从远到近找最远可见格子（贪心弦拉直）
                    for (int probe = cellCount - 1; probe > anchor + 1; probe--)
                    {
                        int2 p = grid.FromIndex(cells[probe]);
                        if (HasLineOfSight(in grid, a.x, a.y, p.x, p.y, tagMask, opt.Domain, !opt.CutCorners))
                        {
                            best = probe;
                            break;
                        }
                    }
                    anchor = best;
                    if (anchor == cellCount - 1)
                    {
                        outWaypoints[outCount++] = endPoint;
                    }
                    else
                    {
                        int2 c = grid.FromIndex(cells[anchor]);
                        outWaypoints[outCount++] = grid.CellToWorld(c.x, c.y, opt.Domain, opt.FlightAltitudeOffset);
                    }
                }
                return outCount;
            }

            // CornerWaypoints（或含 Penalty 时的拐点压缩）
            int count = 0;
            outWaypoints[count++] = startPoint;
            int prevDx = int.MaxValue;
            int prevDy = int.MaxValue;
            for (int i = 1; i < cellCount && count < maxWaypoints; i++)
            {
                int2 prev = grid.FromIndex(cells[i - 1]);
                int2 curC = grid.FromIndex(cells[i]);
                int dx = math.clamp(curC.x - prev.x, -1, 1);
                int dy = math.clamp(curC.y - prev.y, -1, 1);
                if (i > 1 && (dx != prevDx || dy != prevDy))
                {
                    outWaypoints[count++] = grid.CellToWorld(prev.x, prev.y, opt.Domain, opt.FlightAltitudeOffset);
                }
                prevDx = dx;
                prevDy = dy;
            }
            if (count < maxWaypoints)
                outWaypoints[count++] = endPoint;
            return count;
        }
    }
}
