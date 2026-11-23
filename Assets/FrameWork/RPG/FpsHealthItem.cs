using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/FPS/Items/Health")]
    public sealed class FpsHealthItem : FpsItemDefinition
    {
        [SerializeField, Min(1f)] private float amount = 35f;

        public override bool TryApply(GameObject user)
        {
            var health = user.GetComponent<FpsHealth>();
            return health != null && health.TryHeal(amount);
        }
    }
}
