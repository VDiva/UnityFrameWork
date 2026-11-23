using System;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class RpgProgression : MonoBehaviour
    {
        [SerializeField] private RpgLevelCurve levelCurve;

        public int Level { get; private set; } = 1;
        public int CurrentExperience { get; private set; }
        public int RequiredExperience => levelCurve != null ? levelCurve.GetRequiredExperience(Level) : 0;
        public bool IsMaxLevel => RequiredExperience == 0;

        public event Action Changed;
        public event Action<int, int> LevelChanged;

        /// <summary>增加经验并返回实际接收的数量。达到最高等级后停止接收。</summary>
        public int GainExperience(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;

            int previousLevel = Level;
            int remaining = amount;
            while (remaining > 0)
            {
                int required = RequiredExperience;
                if (required == 0) break;

                int added = Mathf.Min(remaining, Mathf.Max(0, required - CurrentExperience));
                CurrentExperience += added;
                remaining -= added;
                if (CurrentExperience >= required)
                {
                    Level++;
                    CurrentExperience = 0;
                }
            }

            int accepted = amount - remaining;
            if (accepted > 0)
            {
                if (Level != previousLevel) LevelChanged?.Invoke(previousLevel, Level);
                Changed?.Invoke();
            }
            return accepted;
        }
    }
}
