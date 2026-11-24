using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    public enum RpgSkillTarget { Self, Aim, Nearby }
    public enum RpgSkillEffectKind { Damage, Heal, Status, Dash }

    [CreateAssetMenu(menuName = "FrameWork/RPG/Skill")]
    public sealed class RpgSkillDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Effect
        {
            public RpgSkillEffectKind kind;
            [Min(0f)] public float amount;
            public RpgStatusEffect status;
        }

        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int requiredLevel = 1;
        [SerializeField, Min(0f)] private float cooldown = 3f;
        [SerializeField] private RpgSkillTarget targeting;
        [SerializeField, Min(0f)] private float range = 8f;
        [SerializeField] private Effect[] effects = Array.Empty<Effect>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public int RequiredLevel => Mathf.Max(1, requiredLevel);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public RpgSkillTarget Targeting => targeting;
        public float Range => Mathf.Max(0f, range);
        public IReadOnlyList<Effect> Effects => effects;
    }
}
