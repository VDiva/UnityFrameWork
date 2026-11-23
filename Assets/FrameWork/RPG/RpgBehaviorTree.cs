using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

namespace FrameWork.FPS
{
    public sealed class RpgBehaviorTree : BaseGraph
    {
        private RpgBehaviorProgram program;
        private string validationError;
        private bool built;

        protected override void OnEnable()
        {
            base.OnEnable();
            onGraphChanges += OnGraphChanged;
            built = false;
        }

        protected override void OnDisable()
        {
            onGraphChanges -= OnGraphChanged;
            built = false;
            base.OnDisable();
        }

        private void OnGraphChanged(GraphChanges changes) => built = false;

        public string ValidateTree()
        {
            TryBuild(out program, out validationError);
            built = true;
            return validationError;
        }

        internal bool TryTick(RpgEnemyAgent agent, float deltaTime, out string error)
        {
            if (!built) ValidateTree();
            error = validationError;
            if (program == null) return false;

            program.Tick(agent, deltaTime);
            return true;
        }

        private bool TryBuild(out RpgBehaviorProgram result, out string error)
        {
            result = null;
            error = null;
            RpgBehaviorRoot root = null;
            var branches = new Dictionary<RpgBehaviorNode, RpgBehaviorNode[]>();

            foreach (var candidate in nodes)
            {
                if (!(candidate is RpgBehaviorNode node) || branches.ContainsKey(node))
                {
                    error = "图中含有无效或重复节点。";
                    return false;
                }
                branches.Add(node, new RpgBehaviorNode[node.SlotCount]);
                if (node is RpgBehaviorRoot entry)
                {
                    if (root != null)
                    {
                        error = "只能有一个行为树入口。";
                        return false;
                    }
                    root = entry;
                }
            }

            if (root == null)
            {
                error = "缺少行为树入口。";
                return false;
            }

            var parents = new HashSet<RpgBehaviorChild>();
            foreach (var edge in edges)
            {
                if (edge == null || !(edge.outputNode is RpgBehaviorNode parent) ||
                    !(edge.inputNode is RpgBehaviorChild child) ||
                    !branches.TryGetValue(parent, out var slots) || !branches.ContainsKey(child) ||
                    edge.inputFieldName != nameof(RpgBehaviorChild.parent))
                {
                    error = "存在无效连线。请将父节点的输出连接到子节点的父节点端口。";
                    return false;
                }
                int index = parent.GetSlot(edge.outputFieldName);
                if (index < 0 || index >= slots.Length || slots[index] != null || !parents.Add(child))
                {
                    error = "一个输出端口只能有一个子节点，每个子节点也只能有一个父节点。";
                    return false;
                }
                slots[index] = child;
            }

            var visited = new HashSet<RpgBehaviorNode>();
            if (!Visit(root, branches, visited, 1, out error)) return false;
            if (visited.Count != branches.Count)
            {
                error = "存在未连接到入口的节点或闭环。";
                return false;
            }

            result = new RpgBehaviorProgram(root, branches);
            return true;
        }

        private static bool Visit(RpgBehaviorNode node, Dictionary<RpgBehaviorNode, RpgBehaviorNode[]> branches,
            HashSet<RpgBehaviorNode> visited, int depth, out string error)
        {
            error = null;
            if (depth > 64 || !visited.Add(node))
            {
                error = "检测到循环或超过 64 层的树。";
                return false;
            }

            var children = branches[node];
            bool connected = children.Length == 0;
            foreach (var child in children)
            {
                if (child == null) continue;
                connected = true;
                if (!Visit(child, branches, visited, depth + 1, out error)) return false;
            }

            if (!connected) error = node.name + "没有连接子节点。";
            return connected;
        }
    }

    internal sealed class RpgBehaviorProgram
    {
        private readonly RpgBehaviorRoot root;
        private readonly Dictionary<RpgBehaviorNode, RpgBehaviorNode[]> branches;

        public RpgBehaviorProgram(RpgBehaviorRoot root, Dictionary<RpgBehaviorNode, RpgBehaviorNode[]> branches)
        {
            this.root = root;
            this.branches = branches;
        }

        public RpgBehaviorNode GetChild(RpgBehaviorNode node, int index) => branches[node][index];

        public void Tick(RpgEnemyAgent agent, float deltaTime) => root.Tick(this, agent, deltaTime);
    }
}
