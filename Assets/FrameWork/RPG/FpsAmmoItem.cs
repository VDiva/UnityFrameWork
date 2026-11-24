using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/FPS/Items/Ammo")]
    public sealed class FpsAmmoItem : FpsItemDefinition
    {
        [LabelText("弹药类型")] [SerializeField] private FpsAmmoType ammoType;
        [LabelText("补充弹药数量")] [SerializeField, Min(1)] private int rounds = 30;

        public override bool TryApply(GameObject user)
        {
            var player = user.GetComponent<FpsPlayer>();
            var weapon = player != null ? player.EquippedWeapon : null;
            return ammoType != null && weapon != null && weapon.Definition != null &&
                weapon.Definition.AmmoType == ammoType && weapon.AddReserveAmmo(rounds) > 0;
        }
    }
}
