using System.Collections.Generic;
using GraphProcessor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace FrameWork.FPS.Editor
{
    public sealed class RpgEncounterGraphWindow : BaseGraphWindow
    {
        private const string Example = "Assets/FrameWork/RPG/Example/Dungeon/EmberVaultEncounter.asset";
        private RpgDungeonDemo preview;

        [MenuItem("FrameWork/RPG/关卡事件图")]
        private static void OpenMenu()
        {
            var asset = Selection.activeObject as RpgEncounterGraph ??
                        AssetDatabase.LoadAssetAtPath<RpgEncounterGraph>(Example);
            if (asset != null) Open(asset);
        }

        [MenuItem("FrameWork/RPG/新建关卡事件图")]
        private static void CreateGraph()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建关卡事件图", "Encounter", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                EditorUtility.DisplayDialog("文件已存在", "请选择不同的文件名。", "确定");
                return;
            }
            var asset = CreateInstance<RpgEncounterGraph>();
            var start = BaseNode.CreateFromType<RpgEncounterStart>(new Vector2(30, 100));
            var end = BaseNode.CreateFromType<RpgEncounterEnd>(new Vector2(350, 100));
            asset.AddNode(start);
            asset.AddNode(end);
            asset.Connect(end.GetPort(nameof(RpgEncounterEnd.previous), null),
                start.GetPort(nameof(RpgEncounterStart.next), null));
            AssetDatabase.CreateAsset(asset, path);
            Open(asset);
        }

        [OnOpenAsset]
        private static bool OpenAsset(int id, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(id) is RpgEncounterGraph asset)) return false;
            Open(asset);
            return true;
        }

        private static void Open(RpgEncounterGraph asset)
        {
            var window = GetWindow<RpgEncounterGraphWindow>();
            if (!window.isGraphLoaded || window.graph != asset)
            {
                window.Save();
                window.graphView?.Dispose();
                window.graphView = null;
                window.preview = null;
                window.InitializeGraph(asset);
            }
            window.Show();
            window.Focus();
        }

        protected override void InitializeWindow(BaseGraph data)
        {
            titleContent = new GUIContent("关卡事件图 · " + data.name);
            if (graphView == null)
            {
                graphView = new BaseGraphView(this);
                graphView.Add(new EncounterToolbar(graphView));
            }
            rootView.Add(graphView);
        }

        protected override void Update()
        {
            if (!isGraphLoaded) base.Update();
            if (graphView == null) return;
            if (!EditorApplication.isPlaying) preview = null;
            else if (preview == null || preview.EncounterGraph != graph)
            {
                foreach (var demo in Object.FindObjectsOfType<RpgDungeonDemo>())
                {
                    if (demo.EncounterGraph != graph) continue;
                    preview = demo;
                    break;
                }
            }
            var current = preview != null ? preview.CurrentNode : null;
            foreach (var pair in graphView.nodeViewsPerNode)
                pair.Value.titleContainer.style.backgroundColor = current == pair.Key
                    ? new StyleColor(new Color(0.12f, 0.65f, 0.95f, 0.45f))
                    : new StyleColor(StyleKeyword.Null);
        }

        protected override void OnDisable()
        {
            Save();
            graphView?.Dispose();
            graphView = null;
        }

        private void Save()
        {
            if (graphView == null || graph == null) return;
            graphView.SaveGraphToDisk();
            AssetDatabase.SaveAssetIfDirty(graph);
        }

        private sealed class EncounterToolbar : ToolbarView
        {
            private string status = "按 Space 添加步骤，连接入口与结束。";

            public EncounterToolbar(BaseGraphView view) : base(view) { }

            protected override void AddButtons()
            {
                AddButton("检查连线", Validate);
                AddButton("保存", () =>
                {
                    Validate();
                    graphView.SaveGraphToDisk();
                    AssetDatabase.SaveAssetIfDirty(graphView.graph);
                });
                AddCustom(() => GUILayout.Label(status, EditorStyles.miniLabel));
            }

            private void Validate() => status =
                ((RpgEncounterGraph)graphView.graph).BuildRoute(new List<RpgEncounterNode>()) ?? "连线有效";
        }
    }
}
