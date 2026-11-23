using System.IO;
using GraphProcessor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FrameWork.InteractiveFilm.Editor
{
    public sealed class FilmEditorWindow : BaseGraphWindow
    {
        private const string ExampleGraph = "Assets/FrameWork/InteractiveFilm/Example/FogHarbor.asset";
        private const string ExampleScene = "Assets/FrameWork/InteractiveFilm/Example/FogHarbor.unity";

        [MenuItem("FrameWork/影游/影游编辑器")]
        public static void OpenFromMenu()
        {
            var story = Selection.activeObject as FilmGraph ?? AssetDatabase.LoadAssetAtPath<FilmGraph>(ExampleGraph);
            if (story == null) story = CreateStory();
            if (story != null) Open(story);
        }

        [MenuItem("FrameWork/影游/新建剧情图")]
        private static void NewStory()
        {
            var story = CreateStory();
            if (story != null) Open(story);
        }

        [MenuItem("FrameWork/影游/打开雾港示例")]
        private static void OpenExample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ExampleScene);
        }

        [OnOpenAsset]
        private static bool OpenFromProject(int id, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(id) is FilmGraph story)) return false;
            Open(story);
            return true;
        }

        private static void Open(FilmGraph story)
        {
            var window = GetWindow<FilmEditorWindow>();
            if (!window.isGraphLoaded || window.graph != story)
            {
                window.SaveCurrent();
                window.graphView?.Dispose();
                window.graphView = null;
                window.InitializeGraph(story);
            }
            window.Show();
            window.Focus();
        }

        protected override void InitializeWindow(BaseGraph data)
        {
            titleContent = new GUIContent("影游 · " + data.name);
            minSize = new Vector2(550f, 320f);
            if (graphView == null)
            {
                graphView = new BaseGraphView(this);
                graphView.Add(new FilmToolbar(graphView));
            }
            rootView.Add(graphView);
        }

        protected override void Update()
        {
            if (!isGraphLoaded) base.Update();
        }

        protected override void OnDisable()
        {
            SaveCurrent();
            graphView?.Dispose();
            graphView = null;
        }

        private void SaveCurrent()
        {
            if (graphView == null || graph == null) return;
            graphView.SaveGraphToDisk();
            AssetDatabase.SaveAssetIfDirty(graph);
        }

        private static FilmGraph CreateStory()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建影游剧情图", "新剧情", "asset",
                "选择保存位置。新图会带一个可运行的分支模板。");
            if (string.IsNullOrEmpty(path)) return null;
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("文件已存在", "请选择不同的文件名。", "确定");
                return null;
            }

            var story = CreateInstance<FilmGraph>();
            BuildStarter(story);
            AssetDatabase.CreateAsset(story, path);
            AssetDatabase.SaveAssetIfDirty(story);
            return story;
        }

        private static void BuildStarter(FilmGraph story)
        {
            var start = Add<FilmStartNode>(story, 20, 115);
            var intro = Add<FilmSegmentNode>(story, 230, 105);
            intro.title = "雾港来电";
            intro.caption = "停电后的雾港里，失声多年的电台忽然传来讯息。";
            var choice = Add<FilmChoiceNode>(story, 560, 105);
            choice.title = "最后一条讯息";
            choice.caption = "有人被困在灯塔下。电池只够发送一条消息，你会发给谁？";
            string rescueOption = choice.AddOption("呼叫救援队");
            string lightOption = choice.AddOption("联系灯塔管理员");
            var rescue = Add<FilmEndingNode>(story, 925, 15);
            rescue.title = "结局 · 破晓";
            rescue.caption = "救援艇在天亮前抵达。困在灯塔下的人平安撤离。";
            var light = Add<FilmEndingNode>(story, 925, 180);
            light.title = "结局 · 归航";
            light.caption = "灯塔重新亮起。管理员找到海堤下的旧通道，领着大家回到港口。";

            Connect(story, start, nameof(start.next), intro);
            Connect(story, intro, nameof(intro.next), choice);
            Connect(story, choice, nameof(choice.optionsOutput), rescue, rescueOption);
            Connect(story, choice, nameof(choice.optionsOutput), light, lightOption);
        }

        private static T Add<T>(FilmGraph graph, float x, float y) where T : FilmNode
        {
            var node = BaseNode.CreateFromType<T>(new Vector2(x, y));
            graph.AddNode(node);
            return node;
        }

        private static void Connect(FilmGraph graph, FilmNode source, string port, FilmContentNode target,
            string optionId = null)
        {
            graph.Connect(target.GetPort(nameof(target.previous), null), source.GetPort(port, optionId));
        }

        private sealed class FilmToolbar : ToolbarView
        {
            private string status = "按 Space 添加节点，再从输出连到下一段。";

            public FilmToolbar(BaseGraphView view) : base(view) {}

            protected override void AddButtons()
            {
                AddButton("新建剧情", () => EditorApplication.delayCall += NewStory);
                AddButton("重置视图", graphView.ResetPositionAndZoom);
                AddButton("检查连线", Validate);
                AddButton("保存", Save);
                AddCustom(() => GUILayout.Label(status, EditorStyles.miniLabel));
            }

            private void Validate() => status = ((FilmGraph)graphView.graph).ValidateGraph() ?? "剧情连线有效";

            private void Save()
            {
                Validate();
                graphView.SaveGraphToDisk();
                AssetDatabase.SaveAssetIfDirty(graphView.graph);
            }
        }
    }
}
