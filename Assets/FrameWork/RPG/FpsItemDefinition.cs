using UnityEngine;

namespace FrameWork.FPS
{
    public abstract class FpsItemDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int carryLimit = 5;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public int CarryLimit => Mathf.Max(1, carryLimit);

        /// <summary>返回 true 时，库存消耗一个道具。</summary>
        public abstract bool TryApply(GameObject user);
    }
}
