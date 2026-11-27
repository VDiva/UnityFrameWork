using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class FpsHealth : MonoBehaviour, IFpsDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float baseArmor;
        [SerializeField] private UnityEvent onDepleted = new UnityEvent();

        public sealed class ActiveEffect
        {
            public RpgStatusEffect Definition { get; }
            public GameObject Source { get; private set; }
            public float Remaining { get; private set; }

            internal ActiveEffect(RpgStatusEffect definition, GameObject source)
            {
                Definition = definition;
                Refresh(source);
            }

            internal void Refresh(GameObject source)
            {
                Source = source;
                Remaining = Definition.Duration;
            }

            internal float Advance(float deltaTime)
            {
                float elapsed = Mathf.Min(Remaining, deltaTime);
                Remaining -= elapsed;
                return elapsed;
            }
        }

        private readonly List<ActiveEffect> effects = new List<ActiveEffect>();
        private float cachedArmor;
        private float cachedMovementMultiplier = 1f;
        private bool statsDirty = true;
        public IReadOnlyList<ActiveEffect> ActiveEffects => effects;

        private void RecalculateStatsIfNeeded()
        {
            if (!statsDirty) return;
            float armor = baseArmor;
            float moveMul = 1f;
            for (int i = 0; i < effects.Count; i++)
            {
                var def = effects[i].Definition;
                if (def == null) continue;
                armor += def.ArmorBonus;
                moveMul *= def.MovementMultiplier;
            }
            cachedArmor = Mathf.Max(0f, armor);
            cachedMovementMultiplier = moveMul;
            statsDirty = false;
        }

        public float Armor
        {
            get
            {
                RecalculateStatsIfNeeded();
                return cachedArmor;
            }
        }

        public float MovementMultiplier
        {
            get
            {
                RecalculateStatsIfNeeded();
                return cachedMovementMultiplier;
            }
        }

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDepleted => CurrentHealth <= 0f;
        public event Action<float, float> HealthChanged;
        public event Action<GameObject> Defeated;

        private void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            effects.Clear();
            statsDirty = true;
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void ApplyDamage(float amount, GameObject instigator)
        {
            if (!isActiveAndEnabled || IsDepleted || !(amount > 0f)) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount * (100f / (100f + Armor)));
            bool depleted = IsDepleted;
            if (depleted)
            {
                effects.Clear();
                statsDirty = true;
            }
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (depleted)
            {
                Defeated?.Invoke(instigator);
                onDepleted.Invoke();
            }
        }

        public bool TryApplyStatus(RpgStatusEffect definition, GameObject source = null)
        {
            if (!isActiveAndEnabled || IsDepleted || definition == null) return false;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Definition != definition) continue;
                effects[i].Refresh(source);
                return true;
            }
            effects.Add(new ActiveEffect(definition, source));
            statsDirty = true;
            return true;
        }

        private void Update()
        {
            if (effects.Count == 0) return;
            float healing = 0f;
            bool removed = false;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                if (effect.Definition != null)
                    healing += effect.Advance(Time.deltaTime) * effect.Definition.HealingPerSecond;
                if (effect.Definition == null || effect.Remaining <= 0f)
                {
                    effects.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed) statsDirty = true;
            if (healing > 0f) TryHeal(healing);
        }

        public bool TryHeal(float amount)
        {
            if (!isActiveAndEnabled || IsDepleted || !(amount > 0f) || CurrentHealth >= maxHealth)
                return false;

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            return true;
        }
    }
}
