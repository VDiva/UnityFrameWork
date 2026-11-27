using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 24 字节对齐搜索节点（配合递增 Stamp 实现 O(1) 免全图清零重置）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SearchNode
    {
        public ulong Priority; // (fCost << 32) | hCost，相同 F 值优先扩展更靠近终点的节点
        public uint GCost;
        public int ParentIndex;
        public int HeapIndex;
        public ushort Stamp;
        public byte State; // 0 = Unvisited, 1 = Open, 2 = Closed
        public byte Padding;
    }

    /// <summary>
    /// 单个 Worker 线程独占的非托管寻路工作区（同时兼容 Burst Job 并行线程与 WebGL 主线程）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SolverWorkspace
    {
        [NativeDisableUnsafePtrRestriction] public SearchNode* Nodes;
        [NativeDisableUnsafePtrRestriction] public int* Heap;
        [NativeDisableUnsafePtrRestriction] public int* RawPath;
        [NativeDisableUnsafePtrRestriction] public int* ExpandedPath;
        [NativeDisableUnsafePtrRestriction] public ushort* StampPtr;
        public int NodeCapacity;
        public int PathCapacity;
        public int HeapCount;
        public ushort CurrentStamp;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginSearch()
        {
            HeapCount = 0;
            ushort next = (ushort)(*StampPtr + 1);
            if (next == 0)
            {
                UnsafeUtility.MemClear(Nodes, (long)NodeCapacity * sizeof(SearchNode));
                next = 1;
            }
            *StampPtr = next;
            CurrentStamp = next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetState(int nodeIndex)
        {
            SearchNode* n = Nodes + nodeIndex;
            return n->Stamp == CurrentStamp ? n->State : (byte)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushNew(int nodeIndex, uint gCost, uint hCost, int parentIndex)
        {
            ulong prio = ((ulong)(gCost + hCost) << 32) | hCost;
            SearchNode* n = Nodes + nodeIndex;
            n->Priority = prio;
            n->GCost = gCost;
            n->ParentIndex = parentIndex;
            n->Stamp = CurrentStamp;
            n->State = 1;

            int idx = HeapCount++;
            SiftUp(idx, nodeIndex, prio);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DecreaseKey(int nodeIndex, uint gCost, uint hCost, int parentIndex)
        {
            ulong prio = ((ulong)(gCost + hCost) << 32) | hCost;
            SearchNode* n = Nodes + nodeIndex;
            n->Priority = prio;
            n->GCost = gCost;
            n->ParentIndex = parentIndex;
            SiftUp(n->HeapIndex, nodeIndex, prio);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int PopMin()
        {
            int top = Heap[0];
            Nodes[top].State = 2;
            int last = --HeapCount;
            if (last > 0)
            {
                int moved = Heap[last];
                SiftDown(0, moved, Nodes[moved].Priority);
            }
            return top;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SiftUp(int idx, int nodeIndex, ulong prio)
        {
            while (idx > 0)
            {
                int parent = (idx - 1) >> 1;
                int pNode = Heap[parent];
                if (Nodes[pNode].Priority <= prio) break;
                Heap[idx] = pNode;
                Nodes[pNode].HeapIndex = idx;
                idx = parent;
            }
            Heap[idx] = nodeIndex;
            Nodes[nodeIndex].HeapIndex = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SiftDown(int idx, int nodeIndex, ulong prio)
        {
            int half = HeapCount >> 1;
            while (idx < half)
            {
                int left = (idx << 1) + 1;
                int right = left + 1;
                int bestChild = left;
                int bestNode = Heap[left];
                ulong bestPrio = Nodes[bestNode].Priority;

                if (right < HeapCount)
                {
                    int rNode = Heap[right];
                    ulong rPrio = Nodes[rNode].Priority;
                    if (rPrio < bestPrio)
                    {
                        bestChild = right;
                        bestNode = rNode;
                        bestPrio = rPrio;
                    }
                }

                if (prio <= bestPrio) break;
                Heap[idx] = bestNode;
                Nodes[bestNode].HeapIndex = idx;
                idx = bestChild;
            }
            Heap[idx] = nodeIndex;
            Nodes[nodeIndex].HeapIndex = idx;
        }
    }

    /// <summary>
    /// 管理多个持久化非托管寻路工作区（按网格规模自动扩容，运行期零 GC）。
    /// </summary>
    internal sealed unsafe class SolverScratchPool : IDisposable
    {
        private NativeArray<SolverWorkspace> _workspaces;
        private bool _disposed;

        public NativeArray<SolverWorkspace> Workspaces => _workspaces;
        public int WorkerCount => _workspaces.IsCreated ? _workspaces.Length : 0;

        public SolverScratchPool(int workerCount, int initialNodeCapacity = 4096)
        {
            int count = math.max(1, workerCount);
            _workspaces = new NativeArray<SolverWorkspace>(count, Allocator.Persistent);
            int nodeCap = NextPow2(math.max(256, initialNodeCapacity));
            int pathCap = math.max(256, nodeCap);

            for (int i = 0; i < count; i++)
            {
                _workspaces[i] = AllocateWorkspace(nodeCap, pathCap);
            }
        }

        public void EnsureCapacity(int requiredNodes, int requiredPathCapacity = 0)
        {
            if (_disposed || !_workspaces.IsCreated)
                throw new ObjectDisposedException(nameof(SolverScratchPool));

            int targetNodes = math.max(256, requiredNodes);
            int targetPath = math.max(256, requiredPathCapacity > 0 ? requiredPathCapacity : targetNodes);

            for (int i = 0; i < _workspaces.Length; i++)
            {
                SolverWorkspace ws = _workspaces[i];
                if (ws.NodeCapacity >= targetNodes && ws.PathCapacity >= targetPath)
                    continue;

                int newNodes = ws.NodeCapacity >= targetNodes ? ws.NodeCapacity : NextPow2(targetNodes);
                int newPath = ws.PathCapacity >= targetPath ? ws.PathCapacity : NextPow2(targetPath);
                FreeWorkspace(ref ws);
                _workspaces[i] = AllocateWorkspace(newNodes, newPath);
            }
        }

        private static SolverWorkspace AllocateWorkspace(int nodeCap, int pathCap)
        {
            long nodesBytes = (long)nodeCap * sizeof(SearchNode);
            var nodes = (SearchNode*)UnsafeUtility.Malloc(nodesBytes, UnsafeUtility.AlignOf<SearchNode>(), Allocator.Persistent);
            UnsafeUtility.MemClear(nodes, nodesBytes);

            var heap = (int*)UnsafeUtility.Malloc((long)nodeCap * sizeof(int), sizeof(int), Allocator.Persistent);
            var rawPath = (int*)UnsafeUtility.Malloc((long)pathCap * sizeof(int), sizeof(int), Allocator.Persistent);
            var expandedPath = (int*)UnsafeUtility.Malloc((long)pathCap * sizeof(int), sizeof(int), Allocator.Persistent);
            var stampPtr = (ushort*)UnsafeUtility.Malloc(sizeof(ushort), sizeof(ushort), Allocator.Persistent);
            *stampPtr = 0;

            return new SolverWorkspace
            {
                Nodes = nodes,
                Heap = heap,
                RawPath = rawPath,
                ExpandedPath = expandedPath,
                StampPtr = stampPtr,
                NodeCapacity = nodeCap,
                PathCapacity = pathCap,
                HeapCount = 0,
                CurrentStamp = 0
            };
        }

        private static void FreeWorkspace(ref SolverWorkspace ws)
        {
            if (ws.Nodes != null) { UnsafeUtility.Free(ws.Nodes, Allocator.Persistent); ws.Nodes = null; }
            if (ws.Heap != null) { UnsafeUtility.Free(ws.Heap, Allocator.Persistent); ws.Heap = null; }
            if (ws.RawPath != null) { UnsafeUtility.Free(ws.RawPath, Allocator.Persistent); ws.RawPath = null; }
            if (ws.ExpandedPath != null) { UnsafeUtility.Free(ws.ExpandedPath, Allocator.Persistent); ws.ExpandedPath = null; }
            if (ws.StampPtr != null) { UnsafeUtility.Free(ws.StampPtr, Allocator.Persistent); ws.StampPtr = null; }
        }

        private static int NextPow2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            return math.max(256, v + 1);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_workspaces.IsCreated)
            {
                for (int i = 0; i < _workspaces.Length; i++)
                {
                    SolverWorkspace ws = _workspaces[i];
                    FreeWorkspace(ref ws);
                }
                _workspaces.Dispose();
            }
        }
    }
}
