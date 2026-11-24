using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/FPS/Items/Weapon")]
    public sealed class FpsWeaponItem : FpsItemDefinition
    {
        [LabelText("武器预制体")] [SerializeField] private FpsWeapon weaponPrefab;

        public FpsWeapon WeaponPrefab => weaponPrefab;

        public override bool TryApply(GameObject user)
        {
            var loadout = user.GetComponent<FpsLoadout>();
            return loadout != null && loadout.TryAcquire(weaponPrefab);
        }
    }
}
