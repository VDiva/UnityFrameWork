using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    public abstract class FpsItemDefinition : ScriptableObject
    {
        [LabelText("物品名称")] [SerializeField] private string displayName;
        [LabelText("物品图标")] [SerializeField] private Sprite icon;
        [LabelText("携带上限")] [SerializeField, Min(1)] private int carryLimit = 5;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public int CarryLimit => Mathf.Max(1, carryLimit);

        /// <summary>返回 true 时，库存消耗一个道具。</summary>
        public abstract bool TryApply(GameObject user);
    }
}
