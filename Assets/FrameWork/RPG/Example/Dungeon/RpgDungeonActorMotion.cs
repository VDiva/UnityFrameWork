using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    public sealed class RpgDungeonActorMotion : MonoBehaviour
    {
        private const float DefeatDuration = 0.75f;

        private readonly List<WeaponPose> weapons = new List<WeaponPose>();
        private Transform body;
        private FpsHealth health;
        private Renderer[] bodyRenderers;
        private MaterialPropertyBlock tint;
        private Vector3 bodyPosition;
        private Quaternion bodyRotation;
        private Vector3 previousPosition;
        private float previousHealth;
        private float stride;
        private float walking;
        private float hurtAt = float.NegativeInfinity;
        private float defeatedAt = float.NegativeInfinity;
        private bool hideOnDefeat;
        private bool tinted;
        private RpgDungeonSkeletalMotion skeletal;

        public void Initialize(Transform model, string motionResource, bool disableAfterDefeat)
        {
            body = model;
            bodyPosition = body.localPosition;
            bodyRotation = body.localRotation;
            previousPosition = transform.position;
            hideOnDefeat = disableAfterDefeat;
            bodyRenderers = body.GetComponentsInChildren<Renderer>(true);
            tint = new MaterialPropertyBlock();
            skeletal = RpgDungeonSkeletalMotion.Create(model, motionResource);
            health = GetComponent<FpsHealth>();
            if (health == null) return;

            previousHealth = health.CurrentHealth;
            health.HealthChanged += OnHealthChanged;
            health.Defeated += OnDefeated;
        }

        public void BindWeapon(FpsWeapon weapon, Transform visual)
        {
            if (weapon != null && visual != null) weapons.Add(new WeaponPose(weapon, visual));
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current < previousHealth) hurtAt = Time.time;
            previousHealth = current;
        }

        private void OnDefeated(GameObject attacker)
        {
            defeatedAt = Time.time;
            if (hideOnDefeat && TryGetComponent(out CharacterController motor)) motor.enabled = false;
        }

        private void LateUpdate()
        {
            if (body == null) return;

            float now = Time.time;
            Vector3 offset = transform.position - previousPosition;
            previousPosition = transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            float speed = Time.deltaTime > 0f && distance < 1f ? distance / Time.deltaTime : 0f;
            walking = Mathf.MoveTowards(walking, Mathf.Clamp01(speed / 2.2f), Time.deltaTime * 9f);
            stride += Time.deltaTime * (7f + 5f * Mathf.Clamp01(speed / 5f));

            float recoil = 0f;
            float sweep = 0f;
            WeaponPose activeWeapon = null;
            foreach (var pose in weapons)
            {
                if (!pose.IsActive) continue;
                activeWeapon = pose;
                pose.Apply(now, out float shot, out float swing);
                recoil = Mathf.Max(recoil, shot);
                sweep = Mathf.Max(sweep, swing);
            }

            float pain = Pulse(now - hurtAt, 0.24f);
            bool isDefeated = !float.IsNegativeInfinity(defeatedAt);
            float defeat = isDefeated ? Mathf.Clamp01((now - defeatedAt) / DefeatDuration) : 0f;
            float falling = skeletal != null && skeletal.HasDefeat ? 0f : Mathf.SmoothStep(0f, 1f, defeat);
            float step = Mathf.Sin(stride);
            float sway = skeletal == null ? walking : 0f;
            float bob = (Mathf.Abs(step) * 0.085f * sway + Mathf.Sin(now * 2.8f) * 0.018f) * (1f - defeat);
            body.localPosition = bodyPosition + new Vector3(step * 0.045f * sway, bob - falling * 0.24f, 0.06f * sweep);
            body.localRotation = bodyRotation * Quaternion.Euler(
                sway * 7f - recoil * 10f - pain * 18f, sweep * -24f, step * 6f * sway + falling * 86f);
            SetHurtTint(pain > 0.01f);

            if (body.gameObject.activeInHierarchy)
                skeletal?.Sample(now, speed, walking, activeWeapon?.FireProgress(now) ?? -1f,
                    activeWeapon?.MeleeProgress(now) ?? -1f, activeWeapon?.ReloadProgress(now) ?? -1f,
                    isDefeated ? defeat : -1f);

            if (hideOnDefeat && defeat >= 1f) gameObject.SetActive(false);
        }

        private void SetHurtTint(bool enabled)
        {
            if (tinted == enabled) return;
            tinted = enabled;
            foreach (var renderer in bodyRenderers)
            {
                renderer.GetPropertyBlock(tint);
                tint.SetColor("_Color", enabled ? new Color(1f, 0.35f, 0.35f) : Color.white);
                renderer.SetPropertyBlock(tint);
            }
        }

        private void OnDestroy()
        {
            skeletal?.Dispose();
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                health.Defeated -= OnDefeated;
            }
            foreach (var pose in weapons) pose.Release();
        }

        private static float Pulse(float elapsed, float duration)
        {
            if (elapsed < 0f || elapsed >= duration) return 0f;
            return Mathf.Sin(elapsed / duration * Mathf.PI);
        }

        private sealed class WeaponPose
        {
            private readonly FpsWeapon weapon;
            private readonly Transform visual;
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private float firedAt = float.NegativeInfinity;
            private float swungAt = float.NegativeInfinity;
            private float reloadAt = float.NegativeInfinity;
            public bool IsActive => weapon != null && visual != null && weapon.isActiveAndEnabled;
            private float ReloadDuration => weapon.Definition != null ? weapon.Definition.ReloadDuration : 1.3f;

            public WeaponPose(FpsWeapon source, Transform model)
            {
                weapon = source;
                visual = model;
                position = model.localPosition;
                rotation = model.localRotation;
                weapon.Fired += OnFire;
                weapon.MeleeSwung += OnSwing;
                weapon.ReloadStarted += OnReload;
            }

            private void OnFire() => firedAt = Time.time;
            private void OnSwing() => swungAt = Time.time;
            private void OnReload(int requestId) => reloadAt = Time.time;

            public float FireProgress(float now) => Progress(now - firedAt, 0.23f);
            public float MeleeProgress(float now) => Progress(now - swungAt, 0.32f);
            public float ReloadProgress(float now) => weapon.IsReloading
                ? Progress(now - reloadAt, Mathf.Max(0.05f, ReloadDuration)) : -1f;

            private static float Progress(float elapsed, float duration) =>
                elapsed >= 0f && elapsed < duration ? elapsed / duration : -1f;

            public void Apply(float now, out float recoil, out float swing)
            {
                recoil = 0f;
                swing = 0f;
                if (!IsActive) return;

                recoil = Pulse(now - firedAt, 0.13f);
                swing = Pulse(now - swungAt, 0.32f);
                float reload = weapon.IsReloading ? Pulse(now - reloadAt, Mathf.Max(0.05f, ReloadDuration)) : 0f;
                visual.localPosition = position + new Vector3(-swing * 0.29f, -reload * 0.25f, -recoil * 0.14f + swing * 0.13f);
                visual.localRotation = rotation * Quaternion.Euler(
                    -recoil * 20f + reload * 45f - swing * 28f, -swing * 100f, reload * 18f - swing * 75f);
            }

            public void Release()
            {
                if (weapon == null) return;
                weapon.Fired -= OnFire;
                weapon.MeleeSwung -= OnSwing;
                weapon.ReloadStarted -= OnReload;
            }
        }
    }
}
