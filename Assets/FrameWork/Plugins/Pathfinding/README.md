# FrameWork.Pathfinding 高性能跨平台通用寻路插件

内置于 `Assets/FrameWork/Plugins/Pathfinding` 的轻量、零 GC、支持 2D/3D 地面与飞行单位的通用寻路系统（已替代旧版 `AstarPathfindingProject`）。

## 核心特性
- **全场景覆盖**：
  - **2D / 3D 地面网格 (`PathGrid`)**：支持 `GridPlane.XZ` 与 `GridPlane.XY`，支持地形高度与坡度烘焙、32 位 Tag 掩码、区域代价权重 (`Penalty`)、二进制导入导出。
  - **2D 双域飞行 (`MovementDomain.Air`)**：与地面共用同一 `PathGrid`，飞行单位自动无视地面障碍/水面，仅受高空禁飞区阻挡，支持 `FlightAltitudeOffset` 巡航高度。
  - **3D 立体空域 (`PathVolume3D`)**：26 邻域 X-Y-Z 自由飞行寻路，可飞越屋顶、穿行桥底、绕过空中动态障碍。
- **极致性能**：
  - `ushort Stamp` 代际标记非托管搜索工作区，`O(1)` 复位免全图清零。
  - 无障碍直线 **Supercover 视线快路径**（0 节点展开直接返回 2 个路点）。
  - 均匀代价 8 方向自动启用 **迭代式无递归 JPS**，否则回退至二叉堆 **A\***（支持部分路径与最大节点限制）。
  - 2D / 3D **Supercover 视线弦拉直**，输出平滑路点不贴墙穿模。
- **跨平台自动调度**：
  - **非 Web 平台**（Windows / macOS / Linux / Android / iOS 等）：自动使用 `[BurstCompile]` + `IJobParallelFor` 多线程 Worker 并行批处理。
  - **WebGL 平台**：自动切为主线程时间片预算分帧调度（默认 `WebGLFrameBudgetMs = 2.5f` ms），避免浏览器卡顿。
  - 网格与动态障碍物修改前自动通过 `BeforeMutate` 同步完成进行中的 Job，杜绝竞态。

## 快速使用

```csharp
using Cysharp.Threading.Tasks;
using FrameWork.Pathfinding;
using UnityEngine;

// 1. 地面异步寻路（非 Web 自动走多线程 Burst Job，WebGL 自动分帧）
PathResult groundPath = await PathfindingSystem.FindPathAsync(startPos, endPos);

// 2. 2D/3D 平面飞行单位寻路（无视地面障碍，巡航高度 +8）
PathResult airPath = await PathfindingSystem.FindFlightPathAsync(startPos, endPos, altitudeOffset: 8f);

// 3. 3D 立体空域自由飞行寻路
PathResult vol3DPath = await PathfindingSystem.FindFlightPath3DAsync(dronePos, targetSkyPos);

// 4. 零 GC 同步寻路（复用 List<Vector3>）
PathStatus status = PathfindingSystem.FindPathSync(
    PathfindingSystem.DefaultGrid,
    new PathQuery(startPos, endPos),
    reusableList, out float cost, out int visited);
```

## 编辑器验证
在 Unity 菜单点击 **`FrameWork -> 寻路 -> 运行寻路正确性与性能基准测试`**，可一键验证 JPS 与 A\* 等价性、2D 双域飞行、动态障碍引用计数、3D 立体爬升越障及 Burst Job 耗时。
