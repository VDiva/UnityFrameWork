using System;
using FrameWork.FPS;
using GraphProcessor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace FrameWork.RPG.Editor
{
    public sealed class RpgBehaviorTreeWindow : BaseGraphWindow
    {
        [MenuItem("FrameWork/RPG/行为树编辑器")]
        public static void OpenFromSelection()
        {
            var tree = Selection.activeObject as RpgBehaviorTree;
            if (tree == null) tree = CreateAsset();
            if (tree != null) Open(tree);
        }

        [OnOpenAsset]
        private static bool OpenFromProject(int instanceId, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceId) is RpgBehaviorTree tree)) return false;
            Open(tree);
            return true;
        }

        internal static RpgBehaviorTreeWindow Open(RpgBehaviorTree tree)
        {
            if (tree == null) return null;
            var window = GetWindow<RpgBehaviorTreeWindow>();
            if (!window.isGraphLoaded || window.graph != tree)
            {
                window.graphView?.SaveGraphToDisk();
                window.graphView?.Dispose();
                window.graphView = null;
                window.InitializeGraph(tree);
            }
            window.Show();
            window.Focus();
            return window;
        }

        internal static void AddAction(RpgBehaviorTree tree, RpgEnemyAction action)
        {
            var node = BaseNode.CreateFromType<RpgBehaviorActionNode>(Vector2.zero);
            node.action = action;
            AddActionNode(tree, node);
        }

        internal static void AddCustomAction(RpgBehaviorTree tree, Type actionType)
        {
            if (BaseNode.CreateFromType(actionType, Vector2.zero) is RpgCustomBehaviorAction node)
                AddActionNode(tree, node);
        }

        private static void AddActionNode(RpgBehaviorTree tree, RpgBehaviorChild node)
        {
            var window = Open(tree);
            if (window == null || !window.isGraphLoaded) return;

            float x = 20f;
            float y = 100f;
            foreach (var existing in tree.nodes)
            {
                x = Mathf.Max(x, existing.position.x + Mathf.Max(150f, existing.position.width) + 64f);
                if (existing is RpgBehaviorRoot) y = existing.position.y;
            }

            Undo.RegisterCompleteObjectUndo(tree, "添加行为树动作");
            node.position.position = new Vector2(x, y);
            var graph = window.graphView;
            var view = graph.AddNode(node);
            EditorUtility.SetDirty(tree);
            graph.ClearSelection();
            graph.AddToSelection(view);
            graph.schedule.Execute(() => graph.FrameSelection());
        }

        internal static RpgBehaviorTree CreateAsset()
        {
            var tree = RpgAssetCreation.CreateAsset<RpgBehaviorTree>("新敌人行为树", BuildStarter);
            if (tree != null) AssetDatabase.SaveAssetIfDirty(tree);
            return tree;
        }

        protected override void InitializeWindow(BaseGraph data)
        {
            titleContent = new GUIContent("RPG 行为树 · " + data.name);
            minSize = new Vector2(520f, 300f);
            if (graphView == null)
            {
                graphView = new BaseGraphView(this);
                graphView.Add(new BehaviorToolbar(graphView));
            }
            rootView.Add(graphView);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Undo.undoRedoPerformed += OnUndo;
        }

        protected override void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            base.OnDisable();
            if (graph != null) AssetDatabase.SaveAssetIfDirty(graph);
            graphView?.Dispose();
            graphView = null;
        }

        private void OnUndo()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null && graph is RpgBehaviorTree tree) tree.ValidateTree();
            };
        }

        private static void BuildStarter(RpgBehaviorTree tree)
        {
            var root = Add<RpgBehaviorRoot>(tree, 20, 135);
            var selector = Add<RpgBehaviorSelector>(tree, 210, 135);
            var attack = Add<RpgBehaviorSequence>(tree, 390, 25);
            var chase = Add<RpgBehaviorSequence>(tree, 390, 168);
            var returnHome = Add<RpgBehaviorActionNode>(tree, 390, 336);
            returnHome.action = RpgEnemyAction.ReturnHome;

            var inRange = Add<RpgBehaviorConditionNode>(tree, 620, 0);
            inRange.condition = RpgEnemyCondition.CanAttack;
            var attackTarget = Add<RpgBehaviorActionNode>(tree, 620, 75);
            attackTarget.action = RpgEnemyAction.Attack;
            var hasTarget = Add<RpgBehaviorConditionNode>(tree, 620, 155);
            hasTarget.condition = RpgEnemyCondition.CanPursue;
            var chaseTarget = Add<RpgBehaviorActionNode>(tree, 620, 230);
            chaseTarget.action = RpgEnemyAction.Chase;

            Connect(tree, root, nameof(root.child), selector);
            Connect(tree, selector, nameof(selector.first), attack);
            Connect(tree, selector, nameof(selector.second), chase);
            Connect(tree, selector, nameof(selector.third), returnHome);
            Connect(tree, attack, nameof(attack.first), inRange);
            Connect(tree, attack, nameof(attack.second), attackTarget);
            Connect(tree, chase, nameof(chase.first), hasTarget);
            Connect(tree, chase, nameof(chase.second), chaseTarget);
        }

        private static T Add<T>(RpgBehaviorTree tree, float x, float y) where T : RpgBehaviorNode
        {
            var node = BaseNode.CreateFromType<T>(new Vector2(x, y));
            tree.AddNode(node);
            return node;
        }

        private static void Connect(RpgBehaviorTree tree, RpgBehaviorNode parent, string output, RpgBehaviorChild child)
        {
            tree.Connect(child.GetPort(nameof(child.parent), null), parent.GetPort(output, null));
        }

        private sealed class BehaviorToolbar : ToolbarView
        {
            private string status = "从入口连接节点；组合按 1–4 执行。";

            public BehaviorToolbar(BaseGraphView view) : base(view) {}

            protected override void AddButtons()
            {
                AddButton("重置视图", graphView.ResetPositionAndZoom);
                AddButton("检查连线", Validate);
                AddButton("保存", Save);
                AddCustom(() => GUILayout.Label(status, EditorStyles.miniLabel));
            }

            private void Validate()
            {
                var tree = (RpgBehaviorTree)graphView.graph;
                status = tree.ValidateTree() ?? "行为树结构有效";
            }

            private void Save()
            {
                Validate();
                graphView.SaveGraphToDisk();
                AssetDatabase.SaveAssetIfDirty(graphView.graph);
            }
        }
    }
}
