using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/RPG/Enemy")]
    public sealed class RpgEnemyDefinition : ScriptableObject
    {
        [LabelText("敌人名称")] [SerializeField] private string displayName;
        [LabelText("侦测距离")] [SerializeField, Min(0.1f)] private float detectionRange = 8f;
        [LabelText("脱战距离")] [SerializeField, Min(0.1f)] private float leashRange = 12f;
        [LabelText("攻击距离")] [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
        [LabelText("移动速度")] [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [LabelText("转向速度（度／秒）")] [SerializeField, Min(1f)] private float turnSpeed = 540f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public float DetectionRange => Mathf.Max(0.1f, detectionRange);
        public float LeashRange => Mathf.Max(DetectionRange, leashRange);
        public float AttackRange => Mathf.Clamp(attackRange, 0.1f, DetectionRange);
        public float MoveSpeed => Mathf.Max(0f, moveSpeed);
        public float TurnSpeed => Mathf.Max(1f, turnSpeed);
    }
}
