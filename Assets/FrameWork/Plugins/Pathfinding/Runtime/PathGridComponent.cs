using UnityEngine;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 2D（XZ / XY）双域寻路网格场景组件：支持自动物理烘焙、Gizmos 可视化与默认网格注册。
    /// </summary>
    [AddComponentMenu("FrameWork/Pathfinding/Path Grid Component")]
    public sealed class PathGridComponent : MonoBehaviour
    {
        [Header("网格尺寸")]
        public GridPlane Plane = GridPlane.XZ;
        [Min(2)] public int Width = 64;
        [Min(2)] public int Height = 64;
        [Min(0.1f)] public float CellSize = 1f;
        public bool EnableHeightMap = false;

        [Header("烘焙设置")]
        public bool RegisterAsDefault = true;
        public bool BakeOnAwake = true;
        public LayerMask GroundMask = ~0;
        public LayerMask ObstacleMask = 0;
        public LayerMask AirObstacleMask = 0;
        public float RaycastStartHeight = 50f;
        public float RaycastDistance = 120f;
        [Range(0f, 89f)] public float MaxSlopeAngle = 45f;
        [Range(0.1f, 0.95f)] public float AgentRadiusRatio = 0.4f;
        public float AirCheckHeight = 6f;

        [Header("调试可视化")]
        public bool DrawGizmos = true;
        public bool DrawAirBlockedGizmos = false;

        public PathGrid Grid { get; private set; }

        private void Awake()
        {
            RebuildAndBake(BakeOnAwake);
        }

        private void OnDestroy()
        {
            if (PathfindingSystem.DefaultGrid == Grid)
                PathfindingSystem.DefaultGrid = null;
            Grid?.Dispose();
            Grid = null;
        }

        public void RebuildAndBake(bool bakePhysics = true)
        {
            if (PathfindingSystem.DefaultGrid == Grid)
                PathfindingSystem.DefaultGrid = null;
            Grid?.Dispose();

            Grid = new PathGrid(Width, Height, CellSize, transform.position, Plane, EnableHeightMap);
            if (bakePhysics)
            {
                if (Plane == GridPlane.XY)
                {
                    float r = CellSize * AgentRadiusRatio;
                    Grid.BakePhysics2D(ObstacleMask, r, AirObstacleMask, r);
                }
                else
                {
                    float r = CellSize * AgentRadiusRatio;
                    Grid.BakePhysics3D(
                        ObstacleMask, GroundMask, r, 1.8f,
                        RaycastStartHeight, RaycastDistance, MaxSlopeAngle, AirObstacleMask, AirCheckHeight, r);
                }
            }

            if (RegisterAsDefault)
                PathfindingSystem.SetDefaultGrid(Grid);
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmos) return;
            Vector3 origin = transform.position;
            Vector3 size = Plane == GridPlane.XZ
                ? new Vector3(Width * CellSize, 0.1f, Height * CellSize)
                : new Vector3(Width * CellSize, Height * CellSize, 0.1f);
            Vector3 center = origin + (Plane == GridPlane.XZ
                ? new Vector3(size.x * 0.5f, 0f, size.z * 0.5f)
                : new Vector3(size.x * 0.5f, size.y * 0.5f, 0f));

            Gizmos.color = new Color(0.2f, 0.85f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(center, size);

            if (Grid == null || !Grid.IsCreated || Grid.CellCount > 16384) return;
            Vector3 cellBox = Plane == GridPlane.XZ
                ? new Vector3(CellSize * 0.9f, 0.08f, CellSize * 0.9f)
                : new Vector3(CellSize * 0.9f, CellSize * 0.9f, 0.08f);

            for (int y = 0; y < Grid.Height; y++)
            {
                for (int x = 0; x < Grid.Width; x++)
                {
                    bool groundOk = Grid.IsWalkable(x, y, uint.MaxValue, MovementDomain.Ground);
                    bool airOk = Grid.IsWalkable(x, y, uint.MaxValue, MovementDomain.Air);
                    if (!groundOk)
                    {
                        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.35f);
                        Gizmos.DrawCube(Grid.CellToWorld(x, y), cellBox);
                    }
                    else if (DrawAirBlockedGizmos && !airOk)
                    {
                        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
                        Gizmos.DrawCube(Grid.CellToWorld(x, y, MovementDomain.Air, AirCheckHeight), cellBox);
                    }
                }
            }
        }
    }
}
