using UnityEngine;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 3D 立体飞行空域体素组件：用于无人机、飞龙、空中载具的真 3D 自由飞行寻路。
    /// </summary>
    [AddComponentMenu("FrameWork/Pathfinding/Path Volume 3D Component")]
    public sealed class PathVolume3DComponent : MonoBehaviour
    {
        [Header("空域体素尺寸")]
        [Min(2)] public int SizeX = 32;
        [Min(2)] public int SizeY = 16;
        [Min(2)] public int SizeZ = 32;
        [Min(0.25f)] public float VoxelSize = 2f;

        [Header("烘焙设置")]
        public bool RegisterAsDefault = true;
        public bool BakeOnAwake = true;
        public LayerMask ObstacleMask = ~0;
        [Range(0.1f, 0.95f)] public float HalfExtentsRatio = 0.45f;

        [Header("调试可视化")]
        public bool DrawGizmos = true;
        public bool DrawBlockedVoxels = false;

        public PathVolume3D Volume { get; private set; }

        private void Awake()
        {
            RebuildAndBake(BakeOnAwake);
        }

        private void OnDestroy()
        {
            if (PathfindingSystem.DefaultVolume3D == Volume)
                PathfindingSystem.DefaultVolume3D = null;
            Volume?.Dispose();
            Volume = null;
        }

        public void RebuildAndBake(bool bakePhysics = true)
        {
            if (PathfindingSystem.DefaultVolume3D == Volume)
                PathfindingSystem.DefaultVolume3D = null;
            Volume?.Dispose();

            Volume = new PathVolume3D(SizeX, SizeY, SizeZ, VoxelSize, transform.position);
            if (bakePhysics)
                Volume.BakePhysics3D(ObstacleMask, VoxelSize * HalfExtentsRatio);

            if (RegisterAsDefault)
                PathfindingSystem.SetDefaultVolume3D(Volume);
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmos) return;
            Vector3 size = new Vector3(SizeX * VoxelSize, SizeY * VoxelSize, SizeZ * VoxelSize);
            Vector3 center = transform.position + size * 0.5f;
            Gizmos.color = new Color(0.25f, 0.75f, 1f, 0.85f);
            Gizmos.DrawWireCube(center, size);

            if (!DrawBlockedVoxels || Volume == null || !Volume.IsCreated || Volume.VoxelCount > 16384) return;
            Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.25f);
            Vector3 box = Vector3.one * (VoxelSize * 0.85f);
            for (int y = 0; y < Volume.SizeY; y++)
            {
                for (int z = 0; z < Volume.SizeZ; z++)
                {
                    for (int x = 0; x < Volume.SizeX; x++)
                    {
                        if (!Volume.IsFlyable(x, y, z))
                            Gizmos.DrawCube(Volume.VoxelToWorld(x, y, z), box);
                    }
                }
            }
        }
    }
}
