using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class FpsWeaponAnimator : MonoBehaviour
    {
        public const string EquipTrigger = "Equip";
        public const string FireTrigger = "Fire";
        public const string MeleeTrigger = "Melee";
        public const string ReloadTrigger = "Reload";

        private static readonly int EquipHash = Animator.StringToHash(EquipTrigger);
        private static readonly int FireHash = Animator.StringToHash(FireTrigger);
        private static readonly int MeleeHash = Animator.StringToHash(MeleeTrigger);
        private static readonly int ReloadHash = Animator.StringToHash(ReloadTrigger);

        [SerializeField] private FpsLoadout loadout;
        [SerializeField] private FpsPlayer player;

        private readonly HashSet<int> triggers = new HashSet<int>();
        private Animator animator;
        private RuntimeAnimatorController defaultController;
        private FpsWeapon weapon;
        private int? reloadRequest;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            defaultController = animator.runtimeAnimatorController;
            if (loadout == null) loadout = GetComponentInParent<FpsLoadout>();
            if (player == null) player = GetComponentInParent<FpsPlayer>();
        }

        private void OnEnable()
        {
            if (loadout != null) loadout.EquippedChanged += RefreshWeapon;
            RefreshWeapon();
        }

        private void Start() => RefreshWeapon();

        private void OnDisable()
        {
            if (loadout != null) loadout.EquippedChanged -= RefreshWeapon;
            if (weapon != null && reloadRequest.HasValue)
                weapon.TryCancelReload(reloadRequest.Value);
            UnbindWeapon();
        }

        private void RefreshWeapon()
        {
            var next = loadout != null ? loadout.EquippedWeapon : player != null ? player.EquippedWeapon : null;
            if (ReferenceEquals(weapon, next)) return;

            UnbindWeapon();
            weapon = next;
            var controller = weapon != null && weapon.Definition != null ? weapon.Definition.AnimatorController : null;
            animator.runtimeAnimatorController = controller != null ? controller : defaultController;
            triggers.Clear();
            if (animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger)
                        triggers.Add(parameter.nameHash);
            }

            if (weapon == null) return;
            weapon.Fired += PlayFire;
            weapon.MeleeSwung += PlayMelee;
            weapon.ReloadStarted += PlayReload;
            if (weapon.PendingReloadRequestId is int requestId) PlayReload(requestId);
            else SetTrigger(EquipHash);
        }

        private void UnbindWeapon()
        {
            if (weapon != null)
            {
                weapon.Fired -= PlayFire;
                weapon.MeleeSwung -= PlayMelee;
                weapon.ReloadStarted -= PlayReload;
            }
            weapon = null;
            reloadRequest = null;
        }

        private void PlayFire() => SetTrigger(FireHash);
        private void PlayMelee() => SetTrigger(MeleeHash);

        private void PlayReload(int requestId)
        {
            if (reloadRequest.HasValue && reloadRequest.Value != requestId && animator.runtimeAnimatorController != null)
                animator.Rebind();

            reloadRequest = weapon.Definition != null &&
                weapon.Definition.ReloadCompletion == FpsReloadCompletion.External ? requestId : (int?)null;
            if (triggers.Contains(ReloadHash))
            {
                if (triggers.Contains(FireHash)) animator.ResetTrigger(FireHash);
                if (triggers.Contains(EquipHash)) animator.ResetTrigger(EquipHash);
            }
            SetTrigger(ReloadHash);
        }

        private void SetTrigger(int trigger)
        {
            if (triggers.Contains(trigger)) animator.SetTrigger(trigger);
        }

        /// <summary>在装弹完成帧由 Animation Event 调用。</summary>
        public void CompleteReloadFromAnimation()
        {
            var request = reloadRequest;
            reloadRequest = null;
            if (weapon != null && request.HasValue) weapon.TryCompleteReload(request.Value);
        }
    }
}
