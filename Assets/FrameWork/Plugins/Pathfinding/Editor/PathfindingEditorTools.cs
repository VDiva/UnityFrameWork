using System.Diagnostics;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FrameWork.Pathfinding.Editor
{
    [CustomEditor(typeof(PathGridComponent))]
    public sealed class PathGridComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var comp = (PathGridComponent)target;

            EditorGUILayout.Space(6);
            string mode = PathfindingSystem.IsUsingMultithreadedJobs ? "Burst 多线程 Job" : "主线程分帧 (WebGL/单核)";
            EditorGUILayout.HelpBox(
                $"当前调度模式：{mode}\n" +
                $"网格规模：{comp.Width} x {comp.Height} = {comp.Width * comp.Height:N0} 格",
                MessageType.Info);

            if (GUILayout.Button("重新烘焙 2D 双域网格 (Bake)"))
            {
                comp.RebuildAndBake(true);
                SceneView.RepaintAll();
            }
        }
    }

    [CustomEditor(typeof(PathVolume3DComponent))]
    public sealed class PathVolume3DComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var comp = (PathVolume3DComponent)target;

            EditorGUILayout.Space(6);
            string mode = PathfindingSystem.IsUsingMultithreadedJobs ? "Burst 多线程 Job" : "主线程分帧 (WebGL/单核)";
            EditorGUILayout.HelpBox(
                $"当前调度模式：{mode}\n" +
                $"3D 空域规模：{comp.SizeX} x {comp.SizeY} x {comp.SizeZ} = {comp.SizeX * comp.SizeY * comp.SizeZ:N0} 体素",
                MessageType.Info);

            if (GUILayout.Button("重新烘焙 3D 飞行空域 (Bake)"))
            {
                comp.RebuildAndBake(true);
                SceneView.RepaintAll();
            }
        }
    }

    public static class PathfindingBenchmarkMenu
    {
        [MenuItem("FrameWork/寻路/运行寻路正确性与性能基准测试")]
        public static void RunVerificationAndBenchmark()
        {
            int passed = 0;
            int total = 0;

            // 1. 2D 迷宫 JPS 与 A* 等价性测试
            total++;
            using (var grid = new PathGrid(128, 128, 1f, Vector3.zero, GridPlane.XZ))
            {
                // 构造多道带缺口的阻挡墙
                for (int y = 0; y < 120; y++) grid.SetWalkable(30, y, false);
                for (int y = 8; y < 128; y++) grid.SetWalkable(64, y, false);
                for (int y = 0; y < 118; y++) grid.SetWalkable(96, y, false);

                var optJps = PathOptions.Default;
                optJps.Algorithm = PathAlgorithm.JPS;
                optJps.OutputMode = PathOutputMode.CornerWaypoints;

                var optAStar = PathOptions.Default;
                optAStar.Algorithm = PathAlgorithm.AStar;
                optAStar.OutputMode = PathOutputMode.CornerWaypoints;

                Vector3 start = new Vector3(2.5f, 0f, 2.5f);
                Vector3 end = new Vector3(124.5f, 0f, 124.5f);

                var resJps = PathfindingSystem.FindPathSync(grid, new PathQuery(start, end, optJps));
                var resAStar = PathfindingSystem.FindPathSync(grid, new PathQuery(start, end, optAStar));

                bool costMatch = resJps.IsSuccess && resAStar.IsSuccess &&
                                 Mathf.Abs(resJps.TotalCost - resAStar.TotalCost) < 0.05f;
                if (costMatch) passed++;
                Debug.Log($"[PathfindingTest] 1. JPS vs A* 等价性: {(costMatch ? "PASS" : "FAIL")} | " +
                          $"JPS(cost={resJps.TotalCost:F2}, visited={resJps.VisitedNodes}) vs " +
                          $"A*(cost={resAStar.TotalCost:F2}, visited={resAStar.VisitedNodes})");

                // 2. 双域飞行测试：飞行单位无视地面墙直飞
                total++;
                var resAir = PathfindingSystem.FindPathSync(
                    grid, new PathQuery(start, end, PathOptions.DefaultFlying(8f)));
                bool airDirect = resAir.IsSuccess && resAir.WaypointCount == 2 &&
                                 Mathf.Approximately(resAir.Waypoints[0].y, 8f);
                if (airDirect) passed++;
                Debug.Log($"[PathfindingTest] 2. 2D 飞行域无视地面墙: {(airDirect ? "PASS" : "FAIL")} | " +
                          $"waypoints={resAir.WaypointCount}, altitude={resAir.Waypoints[0].y:F1}");

                // 3. 动态障碍物引用计数测试
                total++;
                grid.AddDynamicObstacleRect(10, 10, 14, 14, true, false);
                grid.AddDynamicObstacleRect(10, 10, 14, 14, true, false);
                grid.RemoveDynamicObstacleRect(10, 10, 14, 14, true, false);
                bool stillBlocked = !grid.IsWalkable(12, 12, uint.MaxValue, MovementDomain.Ground);
                grid.RemoveDynamicObstacleRect(10, 10, 14, 14, true, false);
                bool unblocked = grid.IsWalkable(12, 12, uint.MaxValue, MovementDomain.Ground);
                if (stillBlocked && unblocked) passed++;
                Debug.Log($"[PathfindingTest] 3. 动态障碍引用计数: {(stillBlocked && unblocked ? "PASS" : "FAIL")}");

                // 4. 性能基准（100 次 128x128 迷宫寻路：Burst Job vs MainThread）
                var prevMode = PathfindingSystem.ExecutionMode;
                PathfindingSystem.ExecutionMode = PathExecutionMode.ForceJobs;
                PathfindingSystem.FindPathSync(grid, new PathQuery(start, end, PathOptions.Default)); // 预热 Burst
                var swJob = Stopwatch.StartNew();
                for (int i = 0; i < 100; i++)
                    PathfindingSystem.FindPathSync(grid, new PathQuery(start, end, PathOptions.Default));
                swJob.Stop();

                PathfindingSystem.ExecutionMode = PathExecutionMode.ForceMainThread;
                var swMain = Stopwatch.StartNew();
                for (int i = 0; i < 100; i++)
                    PathfindingSystem.FindPathSync(grid, new PathQuery(start, end, PathOptions.Default));
                swMain.Stop();
                PathfindingSystem.ExecutionMode = prevMode;

                Debug.Log($"[PathfindingBench] 100x 128x128 迷宫 JPS+LOS | " +
                          $"Burst Job: {swJob.Elapsed.TotalMilliseconds:F2} ms | " +
                          $"MainThread: {swMain.Elapsed.TotalMilliseconds:F2} ms");
            }

            // 5. 3D 立体空域绕越建筑/穹顶测试
            total++;
            using (var vol = new PathVolume3D(24, 16, 24, 1f, Vector3.zero))
            {
                // 中间竖起一堵 10 格高的建筑墙，飞行单位需爬升越过顶部 (y >= 10)
                for (int y = 0; y < 10; y++)
                    for (int z = 0; z < 24; z++)
                        vol.SetFlyable(12, y, z, false);

                var res3D = PathfindingSystem.FindFlightPath3DSync(
                    vol, new PathQuery(new Vector3(2.5f, 2.5f, 12.5f), new Vector3(21.5f, 2.5f, 12.5f), PathOptions.DefaultFlying()));
                float maxY = 0f;
                for (int i = 0; i < res3D.WaypointCount; i++)
                    maxY = math.max(maxY, res3D.Waypoints[i].y);
                bool climbedOver = res3D.IsSuccess && maxY >= 10f;
                if (climbedOver) passed++;
                Debug.Log($"[PathfindingTest] 4. 3D 空域爬升越障: {(climbedOver ? "PASS" : "FAIL")} | " +
                          $"waypoints={res3D.WaypointCount}, maxY={maxY:F1}, visited={res3D.VisitedNodes}");
            }

            Debug.Log($"[PathfindingTest] 总结: {passed}/{total} 通过 | 平台多线程支持={PathfindingSystem.SupportsMultithreadedJobs}");
        }
    }
}
