using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class RpgEncounterGate : MonoBehaviour
    {
        [SerializeField] private FpsHealth[] requiredEnemies = Array.Empty<FpsHealth>();
        [SerializeField] private GameObject barrier;
        [SerializeField] private bool openAutomatically = true;

        private readonly HashSet<FpsHealth> enemies = new HashSet<FpsHealth>();
        private bool started;

        public int Remaining { get; private set; }
        public bool IsOpen { get; private set; }
        public event Action Opened;

        private void OnEnable()
        {
            foreach (var enemy in requiredEnemies)
            {
                if (enemy == null || !enemies.Add(enemy)) continue;
                enemy.Defeated += OnEnemyDefeated;
            }
            if (started) Refresh();
        }

        private void Start()
        {
            started = true;
            Refresh();
        }

        private void OnDisable()
        {
            foreach (var enemy in enemies)
                if (enemy != null) enemy.Defeated -= OnEnemyDefeated;
            enemies.Clear();
        }

        private void OnEnemyDefeated(GameObject attacker) => Refresh();

        private void Refresh()
        {
            if (IsOpen) return;

            int count = 0;
            foreach (var enemy in enemies)
                if (enemy != null && !enemy.IsDepleted) count++;

            Remaining = count;
            if (count == 0 && openAutomatically) Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (barrier != null) barrier.SetActive(false);
            Opened?.Invoke();
        }
    }
}
