using UnityEngine;

namespace FrameWork.FPS
{
    public enum FpsFireMode { SemiAutomatic, Automatic }
    public enum FpsReloadCompletion { Timed, External }
    public enum FpsWeaponKind { Firearm, Melee }

    [CreateAssetMenu(menuName = "FrameWork/FPS/Weapon")]
    public sealed class FpsWeaponDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private FpsWeaponKind kind;
        [SerializeField] private FpsAmmoType ammoType;
        [SerializeField] private FpsFireMode fireMode = FpsFireMode.Automatic;
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int reserveCapacity = 120;
        [SerializeField, Min(0)] private int startingReserve = 90;
        [SerializeField, Min(0.01f)] private float shotInterval = 0.12f;
        [SerializeField, Min(0f)] private float reloadDuration = 1.3f;
        [SerializeField] private bool autoReload = true;
        [SerializeField] private FpsReloadCompletion reloadCompletion;
        [SerializeField, Min(0.01f)] private float range = 100f;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField] private FpsProjectile projectilePrefab;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 35f;
        [SerializeField, Min(0.01f)] private float meleeRadius = 0.25f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public FpsWeaponKind Kind => kind;
        public FpsAmmoType AmmoType => ammoType;
        public FpsFireMode FireMode => fireMode;
        public int MagazineSize => Mathf.Max(1, magazineSize);
        public int ReserveCapacity => Mathf.Max(0, reserveCapacity);
        public int StartingReserve => Mathf.Clamp(startingReserve, 0, ReserveCapacity);
        public float ShotInterval => Mathf.Max(0.01f, shotInterval);
        public float ReloadDuration => Mathf.Max(0f, reloadDuration);
        public bool AutoReload => autoReload;
        public FpsReloadCompletion ReloadCompletion => reloadCompletion;
        public float Range => Mathf.Max(0.01f, range);
        public float Damage => Mathf.Max(0f, damage);
        public FpsProjectile ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => Mathf.Max(0.01f, projectileSpeed);
        public float MeleeRadius => Mathf.Max(0.01f, meleeRadius);
    }
}
