using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FrameWork.FPS;
using GraphProcessor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FrameWork.RPG.Editor
{
    internal sealed class RpgBehaviorDebugGraphView : BaseGraphView
    {
        internal RpgBehaviorDebugGraphView(EditorWindow window) : base(window) {}
        public override EdgeView CreateEdgeView() => new RpgBehaviorDebugEdge();
    }

    internal sealed class RpgBehaviorDebugEdge : EdgeView
    {
        private RpgBehaviorStatus? result;

        internal void SetTraceResult(RpgBehaviorStatus? status)
        {
            if (result == status) return;
            result = status;
            UpdateEdgeControl();
            edgeControl.MarkDirtyRepaint();
        }

        public override bool UpdateEdgeControl()
        {
            if (!base.UpdateEdgeControl()) return false;
            if (result.HasValue)
            {
                Color color = RpgBehaviorDebugView.ResultColor(result.Value);
                edgeControl.inputColor = color;
                edgeControl.outputColor = color;
                edgeControl.edgeWidth = result == RpgBehaviorStatus.Running ? 5 : 3;
            }
            return true;
        }
    }

    internal sealed class RpgBehaviorDebugView : VisualElement, IDisposable
    {
        private const string BadgeName = "rpg-behavior-trace-badge";

        private readonly BaseGraphView graphView;
        private readonly RpgBehaviorTree graph;
        private readonly PopupField<RpgEnemyAgent> enemyPicker;
        private readonly Label summary;
        private readonly Label parameters;
        private readonly Label execution;
        private readonly List<RpgEnemyAgent> enemies = new List<RpgEnemyAgent>();
        private readonly Dictionary<RpgBehaviorNode, (int Index, RpgBehaviorTraceStep Step)> visited =
            new Dictionary<RpgBehaviorNode, (int Index, RpgBehaviorTraceStep Step)>();
        private readonly StringBuilder text = new StringBuilder();
        private RpgEnemyAgent agent;
        private bool monitoring;
        private double refreshAt;
        private double refreshEnemiesAt;

        internal RpgBehaviorDebugView(BaseGraphView view, RpgBehaviorTree tree)
        {
            graphView = view;
            graph = tree;
            style.position = Position.Absolute;
            style.top = 36f;
            style.right = 12f;
            style.width = 285f;
            style.maxHeight = Length.Percent(86f);
            style.paddingLeft = 9f;
            style.paddingRight = 9f;
            style.paddingTop = 6f;
            style.paddingBottom = 8f;
            style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.15f, 0.19f, 0.98f) : new Color(0.86f, 0.90f, 0.95f, 0.98f);
            style.display = DisplayStyle.None;

            var foldout = new Foldout { text = "行为树运行调试", value = true };
            foldout.style.flexShrink = 1f;
            enemyPicker = new PopupField<RpgEnemyAgent> { label = "追踪敌人" };
            enemyPicker.formatListItemCallback = EnemyName;
            enemyPicker.formatSelectedValueCallback = EnemyName;
            enemyPicker.RegisterValueChangedCallback(change => SelectAgent(change.newValue));
            foldout.Add(enemyPicker);
            summary = AddLabel(foldout, true);
            var legend = AddLabel(foldout, false);
            legend.text = "蓝色 进行中   ·   绿色 成功   ·   红色 失败";
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 295f;
            foldout.Add(scroll);
            parameters = AddLabel(scroll, false);
            parameters.name = "rpg-runtime-parameters";
            execution = AddLabel(scroll, false);
            execution.name = "rpg-execution-order";
            execution.style.marginTop = 7f;
            Add(foldout);
        }

        internal void SelectAgent(RpgEnemyAgent next)
        {
            if (next != null && next.BehaviorTree != graph) return;
            if (ReferenceEquals(agent, next)) return;

            if (agent != null) agent.SetBehaviorTracing(false);
            agent = next;
            if (agent != null && EditorApplication.isPlaying) agent.SetBehaviorTracing(true);
            enemyPicker.SetValueWithoutNotify(agent);
            refreshEnemiesAt = 0d;
            refreshAt = 0d;
            ApplyGraphTrace(null);
        }

        internal void Refresh()
        {
            if (!EditorApplication.isPlaying)
            {
                if (!monitoring) return;
                monitoring = false;
                style.display = DisplayStyle.None;
                if (agent != null) agent.SetBehaviorTracing(false);
                ApplyGraphTrace(null);
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < refreshAt) return;
            refreshAt = now + 0.10d;
            if (!monitoring)
            {
                monitoring = true;
                style.display = DisplayStyle.Flex;
                refreshEnemiesAt = 0d;
            }

            if (now >= refreshEnemiesAt)
            {
                RefreshEnemies();
                refreshEnemiesAt = now + 0.8d;
            }

            if (agent == null)
            {
                summary.text = "场景中没有绑定这棵树的敌人。";
                parameters.text = "";
                execution.text = "";
                ApplyGraphTrace(null);
                return;
            }

            if (!agent.IsTracingBehavior) agent.SetBehaviorTracing(true);
            var trace = agent.BehaviorTrace;
            ApplyGraphTrace(trace);
            DrawRuntimeParameters();
            DrawTraceList(trace);
        }

        private void RefreshEnemies()
        {
            enemies.Clear();
            foreach (var candidate in Object.FindObjectsOfType<RpgEnemyAgent>(true))
            {
                if (candidate.BehaviorTree == graph) enemies.Add(candidate);
            }
            enemyPicker.choices = enemies;
            if (agent == null || !enemies.Contains(agent))
                SelectAgent(enemies.Find(candidate => candidate.isActiveAndEnabled) ??
                    (enemies.Count > 0 ? enemies[0] : null));
            enemyPicker.SetValueWithoutNotify(agent);
        }

        private void DrawRuntimeParameters()
        {
            string frame = agent.BehaviorTraceFrame >= 0 ? agent.BehaviorTraceFrame.ToString() : "等待执行";
            summary.text = $"{StateText(agent.State)}  ·  帧 {frame}";
            text.Clear();
            text.Append("运行参数\n");
            text.Append("目标  ").AppendLine(agent.Target != null ? agent.Target.name : "无");
            text.Append("目标距离  ").AppendLine(Distance(agent.TargetDistance));
            text.Append("距出生点  ").AppendLine(Distance(agent.HomeDistance));
            if (agent.Definition != null)
            {
                var definition = agent.Definition;
                text.Append("感知 / 追击 / 攻击  ")
                    .Append(definition.DetectionRange.ToString("0.0")).Append(" / ")
                    .Append(definition.LeashRange.ToString("0.0")).Append(" / ")
                    .Append(definition.AttackRange.ToString("0.0")).AppendLine(" m");
                text.Append("移速  ").Append(definition.MoveSpeed.ToString("0.0")).AppendLine(" m/s");
            }
            text.Append("可追击  ").AppendLine(Condition(agent.LastCanPursue));
            text.Append("可攻击  ").AppendLine(Condition(agent.LastCanAttack));
            text.Append("视线无遮挡  ").AppendLine(Condition(agent.LastHasLineOfSight));

            if (agent.TryGetComponent(out FpsHealth health))
                text.Append("生命  ").Append(health.CurrentHealth.ToString("0")).Append(" / ")
                    .AppendLine(health.MaxHealth.ToString("0"));
            if (agent.Weapon != null)
            {
                var weapon = agent.Weapon;
                text.Append("武器  ").AppendLine(weapon.Definition != null ? weapon.Definition.DisplayName : weapon.name);
                if (!weapon.IsMelee)
                    text.Append("弹匣 / 备弹  ").Append(weapon.MagazineAmmo).Append(" / ")
                        .Append(weapon.ReserveAmmo).AppendLine(weapon.IsReloading ? "（换弹中）" : "");
            }
            if (agent.BehaviorParameters != null)
                foreach (var parameter in agent.BehaviorParameters)
                    text.Append(parameter.Key).Append("  ").AppendLine(parameter.Value);
            if (!string.IsNullOrEmpty(agent.LastBehaviorError))
                text.Append("树校验  ").AppendLine(agent.LastBehaviorError);
            parameters.text = text.ToString().TrimEnd();
        }

        private void DrawTraceList(IReadOnlyList<RpgBehaviorTraceStep> trace)
        {
            if (trace == null || trace.Count == 0)
            {
                execution.text = agent.isActiveAndEnabled ? "尚无可显示的执行步骤。" : "该敌人当前未运行。";
                return;
            }

            text.Clear();
            text.AppendLine("执行顺序");
            for (int i = 0; i < trace.Count; i++)
            {
                var step = trace[i];
                text.Append(i + 1).Append(". ").Append(NodeText(step.Node)).Append("  ·  ")
                    .AppendLine(ResultText(step.Result));
            }
            execution.text = text.ToString().TrimEnd();
        }

        private void ApplyGraphTrace(IReadOnlyList<RpgBehaviorTraceStep> trace)
        {
            visited.Clear();
            if (trace != null)
                for (int i = 0; i < trace.Count; i++)
                    visited[trace[i].Node] = (i + 1, trace[i]);

            foreach (var pair in graphView.nodeViewsPerNode)
            {
                var title = pair.Value.titleContainer;
                var badge = title.Q<Label>(BadgeName);
                if (pair.Key is RpgBehaviorNode node && visited.TryGetValue(node, out var entry))
                {
                    if (badge == null)
                    {
                        badge = new Label { name = BadgeName, pickingMode = PickingMode.Ignore };
                        badge.style.marginLeft = 6f;
                        badge.style.marginRight = 5f;
                        badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                        title.Add(badge);
                    }
                    Color color = ResultColor(entry.Step.Result);
                    title.style.backgroundColor = new Color(color.r, color.g, color.b, 0.32f);
                    badge.text = $"{entry.Index} {ResultText(entry.Step.Result)}";
                    badge.style.color = EditorGUIUtility.isProSkin ? color : color * new Color(0.38f, 0.38f, 0.38f, 1f);
                    badge.style.display = DisplayStyle.Flex;
                }
                else if (badge != null)
                {
                    title.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                    badge.style.display = DisplayStyle.None;
                }
            }

            foreach (var edge in graphView.edgeViews)
            {
                if (!(edge is RpgBehaviorDebugEdge debugEdge)) continue;
                var model = edge.serializedEdge;
                RpgBehaviorStatus? result = null;
                if (model != null && model.inputNode is RpgBehaviorNode child &&
                    visited.TryGetValue(child, out var entry) && ReferenceEquals(entry.Step.Parent, model.outputNode))
                    result = entry.Step.Result;
                debugEdge.SetTraceResult(result);
            }
        }

        public void Dispose()
        {
            if (agent != null) agent.SetBehaviorTracing(false);
            ApplyGraphTrace(null);
            RemoveFromHierarchy();
        }

        private static Label AddLabel(VisualElement parent, bool bold)
        {
            var label = new Label();
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 5f;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(label);
            return label;
        }

        private static string EnemyName(RpgEnemyAgent enemy) => enemy != null ? enemy.name : "无";
        private static string Distance(float value) => float.IsNaN(value) ? "—" : value.ToString("0.00") + " m";
        private static string Condition(bool? value) => value.HasValue ? value.Value ? "是" : "否" : "未检查";

        private static string NodeText(RpgBehaviorNode node)
        {
            if (node is RpgBehaviorActionNode action) return EnumLabel(action.action);
            if (node is RpgBehaviorConditionNode condition) return EnumLabel(condition.condition);
            return node.name;
        }

        private static string EnumLabel(Enum value)
        {
            string name = value.ToString();
            var field = value.GetType().GetField(name);
            return field?.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? name;
        }

        private static string StateText(RpgEnemyState state) => state switch
        {
            RpgEnemyState.Pursuing => "追击",
            RpgEnemyState.Attacking => "攻击",
            RpgEnemyState.Returning => "返回出生点",
            RpgEnemyState.Defeated => "已击败",
            _ => "待机"
        };

        private static string ResultText(RpgBehaviorStatus result) => result switch
        {
            RpgBehaviorStatus.Running => "进行中",
            RpgBehaviorStatus.Success => "成功",
            _ => "失败"
        };

        internal static Color ResultColor(RpgBehaviorStatus result) => result switch
        {
            RpgBehaviorStatus.Running => new Color(0.30f, 0.72f, 1f),
            RpgBehaviorStatus.Success => new Color(0.42f, 0.87f, 0.50f),
            _ => new Color(1f, 0.40f, 0.39f)
        };
    }
}
