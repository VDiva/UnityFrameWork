using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    public enum RpgSkillTarget
    {
        [LabelText("自身"), InspectorName("自身")] Self,
        [LabelText("瞄准目标"), InspectorName("瞄准目标")] Aim,
        [LabelText("附近目标"), InspectorName("附近目标")] Nearby
    }

    public enum RpgSkillEffectKind
    {
        [LabelText("伤害"), InspectorName("伤害")] Damage,
        [LabelText("治疗"), InspectorName("治疗")] Heal,
        [LabelText("附加状态"), InspectorName("附加状态")] Status,
        [LabelText("冲刺"), InspectorName("冲刺")] Dash
    }

    [CreateAssetMenu(menuName = "FrameWork/RPG/Skill")]
    public sealed class RpgSkillDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Effect
        {
            [LabelText("效果类型")] public RpgSkillEffectKind kind;
            [LabelText("效果数值")] [Min(0f)] public float amount;
            [LabelText("状态效果")] public RpgStatusEffect status;
        }

        [LabelText("技能名称")] [SerializeField] private string displayName;
        [LabelText("技能图标")] [SerializeField] private Sprite icon;
        [LabelText("所需等级")] [SerializeField, Min(1)] private int requiredLevel = 1;
        [LabelText("冷却时间（秒）")] [SerializeField, Min(0f)] private float cooldown = 3f;
        [LabelText("选取目标方式")] [SerializeField] private RpgSkillTarget targeting;
        [LabelText("施法距离")] [SerializeField, Min(0f)] private float range = 8f;
        [LabelText("内置效果列表")] [SerializeField] private Effect[] effects = Array.Empty<Effect>();
        [SerializeField, Tooltip("指定后由脚本执行整个技能，旧效果列表仅用于未指定脚本的技能。")]
        [LabelText("技能执行脚本")] private RpgSkillExecution execution;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public int RequiredLevel => Mathf.Max(1, requiredLevel);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public RpgSkillTarget Targeting => targeting;
        public float Range => Mathf.Max(0f, range);
        public IReadOnlyList<Effect> Effects => effects;
        public RpgSkillExecution Execution => execution;
    }
}
