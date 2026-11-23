using System;
using UnityEngine;
using UnityEngine.Events;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class FpsHealth : MonoBehaviour, IFpsDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private UnityEvent onDepleted = new UnityEvent();

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
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void ApplyDamage(float amount, GameObject instigator)
        {
            if (!isActiveAndEnabled || IsDepleted || !(amount > 0f)) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            bool depleted = IsDepleted;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (depleted)
            {
                Defeated?.Invoke(instigator);
                onDepleted.Invoke();
            }
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
