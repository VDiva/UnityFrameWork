using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/FPS/Ammo Type")]
    public sealed class FpsAmmoType : ScriptableObject
    {
        [LabelText("弹药名称")] [SerializeField] private string displayName;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}
