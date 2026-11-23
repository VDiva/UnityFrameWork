using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/RPG/Enemy")]
    public sealed class RpgEnemyDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, Min(0.1f)] private float detectionRange = 8f;
        [SerializeField, Min(0.1f)] private float leashRange = 12f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(1f)] private float turnSpeed = 540f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public float DetectionRange => Mathf.Max(0.1f, detectionRange);
        public float LeashRange => Mathf.Max(DetectionRange, leashRange);
        public float AttackRange => Mathf.Clamp(attackRange, 0.1f, DetectionRange);
        public float MoveSpeed => Mathf.Max(0f, moveSpeed);
        public float TurnSpeed => Mathf.Max(1f, turnSpeed);
    }
}
