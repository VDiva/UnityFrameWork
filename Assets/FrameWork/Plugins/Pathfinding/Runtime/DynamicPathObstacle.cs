using Unity.Mathematics;
using UnityEngine;

namespace FrameWork.Pathfinding
{
    /// <summary>
    /// 动态寻路障碍物组件：利用引用计数安全地向 2D 网格（地面/空中）和 3D 飞行空域注册/注销阻挡区域。
    /// 多个障碍物重叠移动或销毁时不会互相误解锁。
    /// </summary>
    [AddComponentMenu("FrameWork/Pathfinding/Dynamic Path Obstacle")]
    public sealed class DynamicPathObstacle : MonoBehaviour
    {
        public Vector3 LocalCenter = Vector3.zero;
        public Vector3 Size = Vector3.one;
        public bool BlockGround = true;
        public bool BlockAir2D = false;
        public bool BlockVolume3D = false;
        public bool UpdateOnMove = true;
        [Min(0.01f)] public float MoveThreshold = 0.2f;

        private bool _applied;
        private int _minX, _minY, _maxX, _maxY;
        private bool _applied2D;
        private int3 _voxMin, _voxMax;
        private bool _applied3D;
        private Vector3 _lastPosition;
        private PathGrid _boundGrid;
        private PathVolume3D _boundVolume;

        private void OnEnable() => Apply();
        private void OnDisable() => Remove();

        private void LateUpdate()
        {
            if (!UpdateOnMove || !_applied) return;
            if ((transform.position - _lastPosition).sqrMagnitude >= MoveThreshold * MoveThreshold)
            {
                Remove();
                Apply();
            }
        }

        public void Refresh()
        {
            Remove();
            Apply();
        }

        private void Apply()
        {
            if (_applied) return;
            Bounds bounds = ComputeWorldBounds();
            _lastPosition = transform.position;

            _boundGrid = PathfindingSystem.DefaultGrid;
            if ((BlockGround || BlockAir2D) && _boundGrid != null && _boundGrid.IsCreated)
            {
                _boundGrid.WorldBoundsToCellRect(bounds, 0f, out _minX, out _minY, out _maxX, out _maxY);
                _boundGrid.AddDynamicObstacleRect(_minX, _minY, _maxX, _maxY, BlockGround, BlockAir2D);
                _applied2D = true;
            }

            _boundVolume = PathfindingSystem.DefaultVolume3D;
            if (BlockVolume3D && _boundVolume != null && _boundVolume.IsCreated)
            {
                _boundVolume.WorldBoundsToVoxelBox(bounds, 0f, out _voxMin, out _voxMax);
                _boundVolume.AddDynamicObstacleBox(_voxMin, _voxMax);
                _applied3D = true;
            }

            _applied = true;
        }

        private void Remove()
        {
            if (!_applied) return;
            if (_applied2D && _boundGrid != null && _boundGrid.IsCreated)
                _boundGrid.RemoveDynamicObstacleRect(_minX, _minY, _maxX, _maxY, BlockGround, BlockAir2D);
            if (_applied3D && _boundVolume != null && _boundVolume.IsCreated)
                _boundVolume.RemoveDynamicObstacleBox(_voxMin, _voxMax);

            _boundGrid = null;
            _boundVolume = null;
            _applied2D = false;
            _applied3D = false;
            _applied = false;
        }

        private Bounds ComputeWorldBounds()
        {
            Vector3 worldCenter = transform.TransformPoint(LocalCenter);
            Vector3 lossy = transform.lossyScale;
            Vector3 size = new Vector3(
                Mathf.Abs(Size.x * lossy.x),
                Mathf.Abs(Size.y * lossy.y),
                Mathf.Abs(Size.z * lossy.z));
            return new Bounds(worldCenter, size);
        }

        private void OnDrawGizmosSelected()
        {
            Bounds b = ComputeWorldBounds();
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.75f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
