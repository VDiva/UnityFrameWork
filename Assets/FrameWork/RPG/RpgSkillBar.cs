using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FpsHealth))]
    public sealed class RpgSkillBar : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public KeyCode key;
            public RpgSkillDefinition skill;
        }

        [SerializeField] private Slot[] slots = Array.Empty<Slot>();
        [SerializeField] private LayerMask targetLayers = Physics.DefaultRaycastLayers;

        private readonly Dictionary<RpgSkillDefinition, float> readyTimes = new Dictionary<RpgSkillDefinition, float>();
        private FpsHealth health;
        private RpgProgression progression;
        private CharacterController controller;
        private bool casting;

        public IReadOnlyList<Slot> Slots => slots;
        public event Action<RpgSkillDefinition> Cast;

        private void Awake()
        {
            health = GetComponent<FpsHealth>();
            progression = GetComponent<RpgProgression>();
            controller = GetComponent<CharacterController>();
        }

        public float GetCooldown(RpgSkillDefinition skill) =>
            skill != null && readyTimes.TryGetValue(skill, out float ready)
                ? Mathf.Max(0f, ready - Time.time)
                : 0f;

        public bool MeetsLevel(RpgSkillDefinition skill) =>
            skill != null && (progression != null ? progression.Level : 1) >= skill.RequiredLevel;

        internal void ReadInput(Ray aim)
        {
            foreach (var slot in slots)
                if (slot != null && slot.key != KeyCode.None && Input.GetKeyDown(slot.key))
                    TryCast(slot.skill, aim);
        }

        public bool TryCast(RpgSkillDefinition skill, Ray aim)
        {
            if (!isActiveAndEnabled || health == null || health.IsDepleted || casting ||
                Time.timeScale <= 0f || !MeetsLevel(skill) || GetCooldown(skill) > 0f)
                return false;

            casting = true;
            try
            {
                bool applied = ApplySkill(skill, aim);
                if (!applied) return false;
                readyTimes[skill] = Time.time + skill.Cooldown;
                Cast?.Invoke(skill);
                return true;
            }
            finally { casting = false; }
        }

        private bool ApplySkill(RpgSkillDefinition skill, Ray aim)
        {
            switch (skill.Targeting)
            {
                case RpgSkillTarget.Self:
                    return ApplyEffects(skill, health, aim.direction);
                case RpgSkillTarget.Aim:
                    var target = FirstHit(aim, skill.Range);
                    return target != null && ApplyEffects(skill, target, aim.direction);
                case RpgSkillTarget.Nearby:
                    Vector3 origin = controller != null ? transform.TransformPoint(controller.center) : transform.position;
                    var seen = new HashSet<FpsHealth>();
                    bool applied = false;
                    foreach (var collider in Physics.OverlapSphere(origin, skill.Range, targetLayers, QueryTriggerInteraction.Ignore))
                    {
                        var nearby = collider.GetComponentInParent<FpsHealth>();
                        if (nearby == null || nearby == health || !seen.Add(nearby)) continue;
                        Vector3 offset = collider.bounds.center - origin;
                        if (offset.sqrMagnitude > 0f && FirstHit(new Ray(origin, offset.normalized), offset.magnitude) != nearby)
                        {
                            seen.Remove(nearby);
                            continue;
                        }
                        applied |= ApplyEffects(skill, nearby, aim.direction);
                    }
                    return applied;
                default:
                    return false;
            }
        }

        private FpsHealth FirstHit(Ray ray, float distance)
        {
            float nearest = distance;
            FpsHealth result = null;
            foreach (var hit in Physics.RaycastAll(ray, distance, targetLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance > nearest || hit.collider.transform.IsChildOf(transform)) continue;
                nearest = hit.distance;
                result = hit.collider.GetComponentInParent<FpsHealth>();
            }
            return result;
        }

        private bool ApplyEffects(RpgSkillDefinition skill, FpsHealth target, Vector3 direction)
        {
            bool applied = false;
            foreach (var effect in skill.Effects)
            {
                if (effect == null || target == null || !target.isActiveAndEnabled || target.IsDepleted) continue;
                switch (effect.kind)
                {
                    case RpgSkillEffectKind.Damage when effect.amount > 0f:
                        target.ApplyDamage(effect.amount, gameObject);
                        applied = true;
                        break;
                    case RpgSkillEffectKind.Heal:
                        applied |= target.TryHeal(effect.amount);
                        break;
                    case RpgSkillEffectKind.Status:
                        applied |= target.TryApplyStatus(effect.status, gameObject);
                        break;
                    case RpgSkillEffectKind.Dash when target == health && controller != null && controller.enabled:
                        Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up);
                        if (effect.amount <= 0f || planar.sqrMagnitude < 0.0001f) break;
                        Vector3 start = transform.position;
                        controller.Move(planar.normalized * effect.amount);
                        applied |= (transform.position - start).sqrMagnitude > 0.000001f;
                        break;
                }
            }
            return applied;
        }
    }
}
