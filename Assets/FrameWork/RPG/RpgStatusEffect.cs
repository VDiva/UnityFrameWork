using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/RPG/Status Effect")]
    public sealed class RpgStatusEffect : ScriptableObject
    {
        [LabelText("持续时间（秒）")] [SerializeField, Min(0.1f)] private float duration = 5f;
        [LabelText("每秒治疗量")] [SerializeField, Min(0f)] private float healingPerSecond;
        [LabelText("护甲加成")] [SerializeField] private float armorBonus;
        [LabelText("移速倍率")] [SerializeField, Range(0f, 3f)] private float movementMultiplier = 1f;

        public float Duration => Mathf.Max(0.1f, duration);
        public float HealingPerSecond => Mathf.Max(0f, healingPerSecond);
        public float ArmorBonus => armorBonus;
        public float MovementMultiplier => Mathf.Clamp(movementMultiplier, 0f, 3f);
    }
}
