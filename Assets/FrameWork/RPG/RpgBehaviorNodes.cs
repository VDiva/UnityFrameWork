using Sirenix.OdinInspector;
using System;
using GraphProcessor;
using UnityEngine;

namespace FrameWork.FPS
{
    public enum RpgBehaviorStatus { Success, Failure, Running }
    public enum RpgBehaviorLink { Flow }
    public enum RpgEnemyCondition
    {
        [LabelText("可追击"), InspectorName("可追击")] CanPursue,
        [LabelText("可攻击"), InspectorName("可攻击")] CanAttack,
        [LabelText("离开出生点"), InspectorName("离开出生点")] AwayFromHome
    }

    public enum RpgEnemyAction
    {
        [LabelText("追击目标"), InspectorName("追击目标")] Chase,
        [LabelText("攻击目标"), InspectorName("攻击目标")] Attack,
        [LabelText("返回出生点"), InspectorName("返回出生点")] ReturnHome,
        [LabelText("待机"), InspectorName("待机")] Idle
    }

    [Serializable]
    public abstract class RpgBehaviorNode : BaseNode
    {
        internal virtual int SlotCount => 0;
        internal virtual int GetSlot(string field) => -1;
        internal abstract RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime);
    }

    [Serializable]
    public abstract class RpgBehaviorChild : RpgBehaviorNode
    {
        [Input("父节点"), NonSerialized] public RpgBehaviorLink parent;
    }

    [Serializable]
    public abstract class RpgCustomBehaviorAction : RpgBehaviorChild
    {
        public override Color color => new Color(0.33f, 0.65f, 0.48f);

        internal sealed override RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime) =>
            Execute(agent, deltaTime);

        protected abstract RpgBehaviorStatus Execute(RpgEnemyAgent agent, float deltaTime);

        protected static bool CheckCondition(RpgEnemyAgent agent, RpgEnemyCondition condition) =>
            agent.CheckCondition(condition);

        protected static RpgBehaviorStatus RunBuiltIn(RpgEnemyAgent agent, RpgEnemyAction action, float deltaTime) =>
            agent.ExecuteAction(action, deltaTime);
    }

    [Serializable, NodeMenuItem("行为树/入口", typeof(RpgBehaviorTree))]
    public sealed class RpgBehaviorRoot : RpgBehaviorNode
    {
        [Output("开始", false), NonSerialized] public RpgBehaviorLink child;
        public override string name => "行为树入口";
        public override Color color => new Color(0.25f, 0.62f, 0.81f);
        internal override int SlotCount => 1;
        internal override int GetSlot(string field) => field == nameof(child) ? 0 : -1;
        internal override RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime) =>
            tree.TickNode(tree.GetChild(this, 0), this, agent, deltaTime);
    }

    [Serializable]
    public abstract class RpgBehaviorBranch : RpgBehaviorChild
    {
        [Output("1", false), NonSerialized] public RpgBehaviorLink first;
        [Output("2", false), NonSerialized] public RpgBehaviorLink second;
        [Output("3", false), NonSerialized] public RpgBehaviorLink third;
        [Output("4", false), NonSerialized] public RpgBehaviorLink fourth;

        public override Color color => new Color(0.44f, 0.52f, 0.74f);
        internal override int SlotCount => 4;
        internal override int GetSlot(string field) => field switch
        {
            nameof(first) => 0,
            nameof(second) => 1,
            nameof(third) => 2,
            nameof(fourth) => 3,
            _ => -1
        };

        internal RpgBehaviorStatus TickChildren(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime,
            RpgBehaviorStatus continueOn)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var child = tree.GetChild(this, i);
                if (child == null) continue;
                var result = tree.TickNode(child, this, agent, deltaTime);
                if (result != continueOn) return result;
            }
            return continueOn;
        }
    }

    [Serializable, NodeMenuItem("行为树/组合/选择", typeof(RpgBehaviorTree))]
    public sealed class RpgBehaviorSelector : RpgBehaviorBranch
    {
        public override string name => "选择";
        internal override RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime) =>
            TickChildren(tree, agent, deltaTime, RpgBehaviorStatus.Failure);
    }

    [Serializable, NodeMenuItem("行为树/组合/顺序", typeof(RpgBehaviorTree))]
    public sealed class RpgBehaviorSequence : RpgBehaviorBranch
    {
        public override string name => "顺序";
        internal override RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime) =>
            TickChildren(tree, agent, deltaTime, RpgBehaviorStatus.Success);
    }

    [Serializable, NodeMenuItem("行为树/装饰/反转", typeof(RpgBehaviorTree))]
    public sealed class RpgBehaviorInverter : RpgBehaviorChild
    {
        [Output("子节点", false), NonSerialized] public RpgBehaviorLink child;
        public override string name => "反转";
        internal override int SlotCount => 1;
        internal override int GetSlot(string field) => field == nameof(child) ? 0 : -1;

        internal override RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime)
        {
            var result = tree.TickNode(tree.GetChild(this, 0), this, agent, deltaTime);
            if (result == RpgBehaviorStatus.Success) return RpgBehaviorStatus.Failure;
            return result == RpgBehaviorStatus.Failure ? RpgBehaviorStatus.Success : result;
        }
    }

    [Serializable, NodeMenuItem("行为树/叶节点/条件", typeof(RpgBehaviorTree))]
    public sealed class RpgBehaviorConditionNode : RpgBehaviorChild
    {
        [LabelText("执行条件"), InspectorName("执行条件")] [ShowAsDrawer] public RpgEnemyCondition condition;
        public override string name => "条件";
        public override Color color => new Color(0.80f, 0.57f, 0.30f);

        internal override RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime) =>
            agent.CheckCondition(condition) ? RpgBehaviorStatus.Success : RpgBehaviorStatus.Failure;
    }

    [Serializable, NodeMenuItem("行为树/叶节点/动作", typeof(RpgBehaviorTree))]
    public sealed class RpgBehaviorActionNode : RpgBehaviorChild
    {
        [LabelText("执行动作"), InspectorName("执行动作")] [ShowAsDrawer] public RpgEnemyAction action;
        public override string name => "动作";
        public override Color color => new Color(0.33f, 0.65f, 0.48f);

        internal override RpgBehaviorStatus Tick(RpgBehaviorProgram tree, RpgEnemyAgent agent, float deltaTime) =>
            agent.ExecuteAction(action, deltaTime);
    }
}
