using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/RPG/Status Effect")]
    public sealed class RpgStatusEffect : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float duration = 5f;
        [SerializeField, Min(0f)] private float healingPerSecond;
        [SerializeField] private float armorBonus;
        [SerializeField, Range(0f, 3f)] private float movementMultiplier = 1f;

        public float Duration => Mathf.Max(0.1f, duration);
        public float HealingPerSecond => Mathf.Max(0f, healingPerSecond);
        public float ArmorBonus => armorBonus;
        public float MovementMultiplier => Mathf.Clamp(movementMultiplier, 0f, 3f);
    }
}
