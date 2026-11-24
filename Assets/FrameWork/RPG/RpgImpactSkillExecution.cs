using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/RPG/Skill Execution/Impact Combo")]
    public sealed class RpgImpactSkillExecution : RpgSkillExecution
    {
        [LabelText("施法者增益")] [SerializeField] private RpgStatusEffect casterBuff;
        [LabelText("主目标状态效果")] [SerializeField] private RpgStatusEffect targetStatus;
        [LabelText("直接伤害")] [SerializeField, Min(0f)] private float directDamage = 20f;
        [LabelText("范围伤害")] [SerializeField, Min(0f)] private float areaDamage = 10f;
        [LabelText("范围半径")] [SerializeField, Min(0f)] private float radius = 3f;
        [LabelText("遮挡阻止范围伤害")] [SerializeField] private bool requireLineOfSight = true;

        public override bool Execute(RpgSkillContext context)
        {
            var target = context.FindAimTarget(context.Skill.Range);
            Vector3 center = target != null ? target.transform.position : context.Origin;
            // 先确定范围目标，避免直接伤害触发死亡回调后改变命中结果。
            var nearby = areaDamage > 0f && radius > 0f
                ? context.FindAreaTargets(center, radius, requireLineOfSight) : null;
            bool applied = casterBuff != null && context.ApplyStatus(context.Caster, casterBuff);
            if (targetStatus != null) applied |= context.ApplyStatus(target, targetStatus);
            applied |= context.Damage(target, directDamage);
            if (nearby != null)
                foreach (var other in nearby)
                    if (other != target) applied |= context.Damage(other, areaDamage);
            return applied;
        }
    }
}
