using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/FPS/Items/Health")]
    public sealed class FpsHealthItem : FpsItemDefinition
    {
        [LabelText("即时治疗量")] [SerializeField, Min(1f)] private float amount = 35f;
        [LabelText("附加状态效果")] [SerializeField] private RpgStatusEffect statusEffect;

        public override bool TryApply(GameObject user)
        {
            var health = user.GetComponent<FpsHealth>();
            if (health == null) return false;
            if (statusEffect == null) return health.TryHeal(amount);
            return health.CurrentHealth < health.MaxHealth && health.TryApplyStatus(statusEffect, user);
        }
    }
}
