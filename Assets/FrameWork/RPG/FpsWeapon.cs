using System;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class FpsWeapon : MonoBehaviour
    {
        [SerializeField] private FpsWeaponDefinition definition;
        [SerializeField] private Transform muzzle;
        [Header("Defaults used without a definition")]
        [SerializeField] private bool automatic = true;
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int startingReserve = 90;
        [SerializeField, Min(0.01f)] private float shotInterval = 0.12f;
        [SerializeField, Min(0f)] private float reloadDuration = 1.3f;
        [SerializeField, Min(0.01f)] private float range = 100f;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;

        private float nextShotTime;
        private float reloadEndTime;
        private int reloadRequestId;

        public int MagazineAmmo { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }
        public int? PendingReloadRequestId => IsReloading ? reloadRequestId : (int?)null;
        public FpsWeaponDefinition Definition => definition;
        public bool IsMelee => definition != null && definition.Kind == FpsWeaponKind.Melee;
        public bool Automatic => !IsMelee && (definition != null ? definition.FireMode == FpsFireMode.Automatic : automatic);
        private int MagazineSize => definition != null ? definition.MagazineSize : magazineSize;
        private float ReloadDuration => definition != null ? definition.ReloadDuration : reloadDuration;
        private bool AutoReload => !IsMelee && (definition == null || definition.AutoReload);
        private bool UsesExternalReload => definition != null && definition.ReloadCompletion == FpsReloadCompletion.External;

        public event Action AmmoChanged;
        public event Action Fired;
        public event Action MeleeSwung;
        public event Action<RaycastHit> Hit;
        /// <summary>参数为本次换弹编号，可在动画完成后传给 TryCompleteReload。</summary>
        public event Action<int> ReloadStarted;

        private void Awake()
        {
            if (IsMelee) return;
            MagazineAmmo = MagazineSize;
            ReserveAmmo = definition != null ? definition.StartingReserve : startingReserve;
        }

        private void OnEnable()
        {
            if (AutoReload && MagazineAmmo == 0) TryReload();
        }

        private void Update()
        {
            if (IsReloading && !UsesExternalReload && Time.time >= reloadEndTime)
                TryCompleteReload(reloadRequestId);
        }

        private void OnDisable()
        {
            TryCancelReload(reloadRequestId);
        }

        public bool TryFire(Ray aim, GameObject instigator)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || IsReloading)
                return false;

            if (IsMelee) return TrySwing(aim, instigator);

            if (MagazineAmmo <= 0)
            {
                if (AutoReload) TryReload();
                return false;
            }
            if (Time.time < nextShotTime) return false;

            MagazineAmmo--;
            nextShotTime = Time.time + (definition != null ? definition.ShotInterval : shotInterval);
            float maxDistance = definition != null ? definition.Range : range;
            float hitDamage = definition != null ? definition.Damage : damage;
            var projectilePrefab = definition != null ? definition.ProjectilePrefab : null;
            Ray shot = GetShotRay(aim, maxDistance);
            if (projectilePrefab != null)
            {
                var projectile = Instantiate(projectilePrefab, shot.origin, Quaternion.LookRotation(shot.direction));
                projectile.Launch(definition.ProjectileSpeed, maxDistance, hitDamage, hitLayers, instigator, this);
            }
            else if (Physics.Raycast(shot, out var hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
                ResolveHit(hit, hitDamage, instigator, this);

            AmmoChanged?.Invoke();
            Fired?.Invoke();
            if (MagazineAmmo == 0 && AutoReload) TryReload();
            return true;
        }

        internal static void ResolveHit(RaycastHit hit, float damage, GameObject instigator, FpsWeapon weapon)
        {
            hit.collider.GetComponentInParent<IFpsDamageable>()?.ApplyDamage(damage, instigator);
            if (weapon != null) weapon.Hit?.Invoke(hit);
        }

        private bool TrySwing(Ray aim, GameObject instigator)
        {
            if (Time.time < nextShotTime) return false;
            nextShotTime = Time.time + definition.ShotInterval;

            // Reach starts at the character even when the camera is behind them.
            Vector3 origin = muzzle != null ? muzzle.position : transform.position;
            float aimDistance = definition.Range + Vector3.Distance(aim.origin, origin);
            Vector3 direction = GetShotRay(aim, aimDistance, origin).direction;
            if (Physics.SphereCast(origin, definition.MeleeRadius, direction, out var hit,
                definition.Range, hitLayers, QueryTriggerInteraction.Ignore))
                ResolveHit(hit, definition.Damage, instigator, this);

            MeleeSwung?.Invoke();
            return true;
        }

        private Ray GetShotRay(Ray aim, float maxDistance)
        {
            if (muzzle == null) return aim;
            return GetShotRay(aim, maxDistance, muzzle.position);
        }

        private Ray GetShotRay(Ray aim, float maxDistance, Vector3 origin)
        {
            Vector3 target = Physics.Raycast(aim, out var aimHit, maxDistance, hitLayers,
                QueryTriggerInteraction.Ignore) ? aimHit.point : aim.GetPoint(maxDistance);
            Vector3 offset = target - origin;
            Vector3 direction = Vector3.Dot(offset, aim.direction) > 0f ? offset.normalized : aim.direction;
            return new Ray(origin, direction);
        }

        public bool TryReload()
        {
            if (IsMelee || !isActiveAndEnabled || Time.timeScale <= 0f || IsReloading ||
                MagazineAmmo >= MagazineSize || ReserveAmmo <= 0)
                return false;

            IsReloading = true;
            int requestId = ++reloadRequestId;
            float duration = ReloadDuration;
            reloadEndTime = Time.time + duration;
            ReloadStarted?.Invoke(requestId);
            if (IsReloading && reloadRequestId == requestId)
            {
                if (!UsesExternalReload && duration <= 0f) TryCompleteReload(requestId);
                else AmmoChanged?.Invoke();
            }
            return true;
        }

        public int AddReserveAmmo(int amount)
        {
            if (definition == null || IsMelee || amount <= 0) return 0;

            int added = Mathf.Min(amount, definition.ReserveCapacity - ReserveAmmo);
            if (added <= 0) return 0;

            ReserveAmmo += added;
            AmmoChanged?.Invoke();
            if (AutoReload && MagazineAmmo == 0) TryReload();
            return added;
        }

        /// <summary>只完成当前有效的换弹请求。切枪或重复回调不会补充弹匣。</summary>
        public bool TryCompleteReload(int requestId)
        {
            if (!isActiveAndEnabled || !IsReloading || requestId != reloadRequestId) return false;

            int count = Mathf.Min(Mathf.Max(0, MagazineSize - MagazineAmmo), ReserveAmmo);
            MagazineAmmo += count;
            ReserveAmmo -= count;
            IsReloading = false;
            AmmoChanged?.Invoke();
            return true;
        }

        public bool TryCancelReload(int requestId)
        {
            if (!IsReloading || requestId != reloadRequestId) return false;

            IsReloading = false;
            AmmoChanged?.Invoke();
            return true;
        }
    }
}
