using UnityEngine;

namespace FrameWork.FPS
{
    /// <summary>技能资源引用的 C# 执行脚本。运行时状态由施法者或独立任务持有。</summary>
    public abstract class RpgSkillExecution : ScriptableObject
    {
        /// <returns>至少产生一个效果时返回 true，技能栏随后开始冷却。</returns>
        public abstract bool Execute(RpgSkillContext context);
    }

    public readonly struct RpgSkillContext
    {
        public RpgSkillDefinition Skill { get; }
        public FpsHealth Caster { get; }
        public Ray Aim { get; }
        public LayerMask TargetLayers { get; }
        public Vector3 Origin { get; }

        internal RpgSkillContext(RpgSkillDefinition skill, FpsHealth caster, Ray aim, LayerMask layers, Vector3 origin)
        {
            Skill = skill;
            Caster = caster;
            Aim = aim;
            TargetLayers = layers;
            Origin = origin;
        }

        public FpsHealth FindAimTarget(float range) => RpgSkillBar.FirstHit(Aim, range, TargetLayers, Caster.transform);

        public bool Damage(FpsHealth target, float amount)
        {
            if (!IsAlive(target) || !(amount > 0f) || float.IsInfinity(amount)) return false;
            target.ApplyDamage(amount, Caster.gameObject);
            return true;
        }

        public bool Heal(FpsHealth target, float amount) => IsAlive(target) && target.TryHeal(amount);

        public bool ApplyStatus(FpsHealth target, RpgStatusEffect status) =>
            IsAlive(target) && target.TryApplyStatus(status, Caster.gameObject);

        public bool DamageArea(Vector3 center, float radius, float amount, bool requireLineOfSight = true)
        {
            bool applied = false;
            foreach (var target in FindAreaTargets(center, radius, requireLineOfSight))
                applied |= Damage(target, amount);
            return applied;
        }

        public System.Collections.Generic.IReadOnlyList<FpsHealth> FindAreaTargets(Vector3 center, float radius, bool requireLineOfSight = true) =>
            RpgSkillBar.FindAreaTargets(center, radius, TargetLayers, Caster, requireLineOfSight);

        private static bool IsAlive(FpsHealth target) => target != null && target.isActiveAndEnabled && !target.IsDepleted;
    }
}
