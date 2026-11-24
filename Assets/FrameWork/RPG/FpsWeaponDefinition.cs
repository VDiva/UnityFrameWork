using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    public enum FpsFireMode { [LabelText("半自动"), InspectorName("半自动")] SemiAutomatic, [LabelText("全自动"), InspectorName("全自动")] Automatic }
    public enum FpsReloadCompletion { [LabelText("按时长完成"), InspectorName("按时长完成")] Timed, [LabelText("由外部事件完成"), InspectorName("由外部事件完成")] External }
    public enum FpsWeaponKind { [LabelText("枪械"), InspectorName("枪械")] Firearm, [LabelText("近战"), InspectorName("近战")] Melee }

    [CreateAssetMenu(menuName = "FrameWork/FPS/Weapon")]
    public sealed class FpsWeaponDefinition : ScriptableObject
    {
        [LabelText("武器名称")] [SerializeField] private string displayName;
        [LabelText("武器图标")] [SerializeField] private Sprite icon;
        [LabelText("第一人称模型预制体")] [SerializeField] private GameObject viewModelPrefab;
        [LabelText("动画控制器")] [SerializeField] private RuntimeAnimatorController animatorController;
        [LabelText("武器类型")] [SerializeField] private FpsWeaponKind kind;
        [LabelText("弹药类型")] [SerializeField] private FpsAmmoType ammoType;
        [LabelText("开火模式")] [SerializeField] private FpsFireMode fireMode = FpsFireMode.Automatic;
        [LabelText("弹匣容量")] [SerializeField, Min(1)] private int magazineSize = 30;
        [LabelText("备用弹药上限")] [SerializeField, Min(0)] private int reserveCapacity = 120;
        [LabelText("初始备用弹药")] [SerializeField, Min(0)] private int startingReserve = 90;
        [LabelText("攻击间隔（秒）")] [SerializeField, Min(0.01f)] private float shotInterval = 0.12f;
        [LabelText("换弹时长（秒）")] [SerializeField, Min(0f)] private float reloadDuration = 1.3f;
        [LabelText("自动换弹")] [SerializeField] private bool autoReload = true;
        [LabelText("换弹完成方式")] [SerializeField] private FpsReloadCompletion reloadCompletion;
        [LabelText("攻击距离")] [SerializeField, Min(0.01f)] private float range = 100f;
        [LabelText("基础伤害")] [SerializeField, Min(0f)] private float damage = 25f;
        [LabelText("投射物预制体")] [SerializeField] private FpsProjectile projectilePrefab;
        [LabelText("投射物速度")] [SerializeField, Min(0.01f)] private float projectileSpeed = 35f;
        [LabelText("近战检测半径")] [SerializeField, Min(0.01f)] private float meleeRadius = 0.25f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public GameObject ViewModelPrefab => viewModelPrefab;
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
