using System;
using System.Collections.Generic;
using GraphProcessor;

namespace FrameWork.FPS
{
    public sealed class RpgEncounterGraph : BaseGraph
    {
        public string BuildRoute(List<RpgEncounterNode> route)
        {
            route.Clear();
            RpgEncounterStart entry = null;
            foreach (var node in nodes)
            {
                if (node is RpgEncounterStart start && entry == null) entry = start;
                else if (!(node is RpgEncounterStep) && !(node is RpgEncounterEnd))
                    return "关卡图只能有一个入口。";
            }
            if (entry == null) return "缺少入口。";

            var links = new Dictionary<RpgEncounterNode, RpgEncounterNode>();
            foreach (var edge in edges)
            {
                if (edge == null || !(edge.outputNode is RpgEncounterNode source) ||
                    source is RpgEncounterEnd || !(edge.inputNode is RpgEncounterNode target) ||
                    target is RpgEncounterStart || links.ContainsKey(source))
                    return "每个步骤只能连接一个后续节点。";
                links.Add(source, target);
            }

            var visited = new HashSet<RpgEncounterNode>();
            RpgEncounterNode current = entry;
            while (visited.Add(current))
            {
                route.Add(current);
                if (current is RpgEncounterEnd)
                    return route.Count == nodes.Count ? null : "存在未连接的节点。";
                if (!links.TryGetValue(current, out current)) return "流程必须连接到结束节点。";
            }
            return "流程不能包含环。";
        }
    }

    public enum RpgEncounterLink { Flow }
    public enum RpgEncounterOperation { WaitForGuards, OpenGate, WaitForExtraction, WaitForSignal, InvokeEvent }

    [Serializable]
    public abstract class RpgEncounterNode : BaseNode { }

    [Serializable, NodeMenuItem("关卡/入口", typeof(RpgEncounterGraph))]
    public sealed class RpgEncounterStart : RpgEncounterNode
    {
        [Output("下一步", false), NonSerialized] public RpgEncounterLink next;
        public override string name => "入口";
    }

    [Serializable, NodeMenuItem("关卡/步骤", typeof(RpgEncounterGraph))]
    public sealed class RpgEncounterStep : RpgEncounterNode
    {
        [Input("进入", false), NonSerialized] public RpgEncounterLink previous;
        [Output("下一步", false), NonSerialized] public RpgEncounterLink next;
        [ShowAsDrawer] public RpgEncounterOperation operation;
        [ShowAsDrawer] public string key;
        public override string name => "关卡步骤";
    }

    [Serializable, NodeMenuItem("关卡/结束", typeof(RpgEncounterGraph))]
    public sealed class RpgEncounterEnd : RpgEncounterNode
    {
        [Input("进入", false), NonSerialized] public RpgEncounterLink previous;
        public override string name => "结束";
    }
}
