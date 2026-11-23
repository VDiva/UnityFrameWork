using System;
using System.Collections.Generic;
using System.Linq;
using FrameWork.FPS;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FrameWork.RPG.Editor
{
    public sealed class RpgStructureWindow : EditorWindow
    {
        private enum Tab { Overview, Scene, Assets }

        private sealed class Entry
        {
            public Object Target;
            public string Name;
            public string Type;
            public string Location;
        }

        private static readonly Color BattleColor = new Color(0.26f, 0.63f, 0.9f);
        private static readonly Color ItemColor = new Color(0.32f, 0.72f, 0.54f);
        private static readonly Color GrowthColor = new Color(0.86f, 0.62f, 0.31f);
        private static readonly string[] Tabs = { "架构总览", "场景组件", "SO 配置" };

        [SerializeField] private Tab tab;
        [SerializeField] private Object selected;
        [SerializeField] private FpsLoadout targetLoadout;
        private readonly List<Entry> sceneEntries = new List<Entry>();
        private readonly List<Entry> assetEntries = new List<Entry>();
        private bool sceneDirty = true;
        private bool assetsDirty = true;
        private Vector2 listScroll;
        private Vector2 inspectorScroll;
        private Vector2 overviewScroll;
        private string query = "";
        private UnityEditor.Editor inspector;
        private GUIStyle nodeStyle;

        [MenuItem("FrameWork/RPG/架构编辑面板")]
        public static void Open()
        {
            var window = GetWindow<RpgStructureWindow>();
            window.titleContent = new GUIContent("RPG 架构");
            window.minSize = new Vector2(600f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += InvalidateScene;
            EditorApplication.projectChanged += InvalidateAssets;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= InvalidateScene;
            EditorApplication.projectChanged -= InvalidateAssets;
            if (inspector != null) DestroyImmediate(inspector);
        }

        private void InvalidateScene()
        {
            sceneDirty = true;
            Repaint();
        }

        private void InvalidateAssets()
        {
            assetsDirty = true;
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var next = (Tab)GUILayout.Toolbar((int)tab, Tabs, EditorStyles.toolbarButton);
                if (next != tab) SwitchTab(next);
                if (GUILayout.Button("新建武器", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                {
                    var weapon = RpgWeaponEditorActions.CreateWeapon();
                    if (weapon != null)
                    {
                        SwitchTab(Tab.Assets);
                        selected = weapon;
                        InvalidateAssets();
                    }
                }
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                {
                    InvalidateScene();
                    InvalidateAssets();
                }
            }

            if (tab == Tab.Overview)
            {
                DrawOverview();
                return;
            }

            if (tab == Tab.Scene && sceneDirty) RefreshScene();
            if (tab == Tab.Assets && assetsDirty) RefreshAssets();
            DrawBrowser(tab == Tab.Scene ? sceneEntries : assetEntries);
        }

        private void SwitchTab(Tab next)
        {
            tab = next;
            selected = null;
            query = "";
            listScroll = Vector2.zero;
            inspectorScroll = Vector2.zero;
            GUI.FocusControl(null);
        }

        private void DrawOverview()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(overviewScroll))
            {
                overviewScroll = scroll.scrollPosition;
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("RPG 模块关系", EditorStyles.largeLabel);
                DrawFlow("战斗", BattleColor, "输入与视角\nFpsPlayer", "装备切换\nFpsLoadout",
                    "枪械或近战\nFpsWeapon", "命中与生命\nIFpsDamageable / FpsHealth");
                DrawFlow("背包与道具", ItemColor, "场景拾取\nFpsItemPickup", "背包与使用\nFpsInventory",
                    "道具效果\nFpsItemDefinition", "补给 / 治疗 / 装备");
                DrawFlow("角色成长", GrowthColor, "目标击败\nFpsHealth.Defeated", "经验归属\nRpgExperienceReward",
                    "等级进度\nRpgProgression", "经验曲线\nRpgLevelCurve");
                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("查看场景组件")) SwitchTab(Tab.Scene);
                    if (GUILayout.Button("查看 SO 配置")) SwitchTab(Tab.Assets);
                }
            }
        }

        private void DrawFlow(string title, Color accent, params string[] nodes)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                nodeStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(5, 5, 4, 4)
                };
                float nodeWidth = (position.width - 82f) / nodes.Length;
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (i > 0) GUILayout.Label("→", GUILayout.Width(16f), GUILayout.Height(48f));
                    Rect rect = GUILayoutUtility.GetRect(nodeWidth, 48f, GUILayout.Width(nodeWidth));
                    EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                        ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.86f, 0.86f, 0.86f));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), accent);
                    GUI.Label(rect, nodes[i], nodeStyle);
                }
            }
        }

        private void DrawBrowser(List<Entry> entries)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(position.width * 0.4f, 232f, 420f))))
                {
                    query = EditorGUILayout.TextField("搜索", query);
                    EditorGUILayout.LabelField(tab == Tab.Scene ? "已加载场景中的组件" : "项目中的配置资产",
                        EditorStyles.miniLabel);
                    using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll))
                    {
                        listScroll = scroll.scrollPosition;
                        bool found = false;
                        foreach (var entry in entries)
                        {
                            if (entry.Target == null || !Matches(entry)) continue;
                            found = true;
                            bool current = selected == entry.Target;
                            if (GUILayout.Toggle(current, entry.Name + "  ·  " + entry.Type, "Button") && !current)
                            {
                                selected = entry.Target;
                                inspectorScroll = Vector2.zero;
                            }
                            GUILayout.Label(entry.Location, EditorStyles.wordWrappedMiniLabel);
                            EditorGUILayout.Space(3f);
                        }
                        if (!found) EditorGUILayout.HelpBox("没有匹配的组件或配置。", MessageType.Info);
                    }
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll))
                    {
                        inspectorScroll = scroll.scrollPosition;
                        DrawInspector();
                    }
                }
            }
        }

        private void DrawInspector()
        {
            if (selected == null)
            {
                EditorGUILayout.HelpBox("从左侧选择一个组件或 SO 配置。", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(selected.name, EditorStyles.largeLabel);
            EditorGUILayout.LabelField(selected.GetType().Name, EditorStyles.miniLabel);
            if (GUILayout.Button("在 Unity 中定位", GUILayout.ExpandWidth(false)))
            {
                Selection.activeObject = selected is Component component ? component.gameObject : selected;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            EditorGUILayout.Space(6f);
            UnityEditor.Editor.CreateCachedEditor(selected, null, ref inspector);
            if (inspector != null) inspector.OnInspectorGUI();
            if (selected is FpsWeaponDefinition weapon)
                RpgWeaponEditorActions.DrawActions(weapon, ref targetLoadout);
        }

        private bool Matches(Entry entry) => string.IsNullOrWhiteSpace(query) ||
            entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
            entry.Type.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
            entry.Location.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshScene()
        {
            sceneDirty = false;
            sceneEntries.Clear();
            var assembly = typeof(FpsPlayer).Assembly;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null || component.GetType().Assembly != assembly) continue;
                    sceneEntries.Add(new Entry
                    {
                        Target = component,
                        Name = component.name,
                        Type = component.GetType().Name,
                        Location = scene.name + " / " + HierarchyPath(component.transform)
                    });
                }
            }
            sceneEntries.Sort((a, b) => string.CompareOrdinal(a.Location, b.Location));
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        private void RefreshAssets()
        {
            assetsDirty = false;
            assetEntries.Clear();
            var types = TypeCache.GetTypesDerivedFrom<FpsItemDefinition>().Where(type => !type.IsAbstract)
                .Concat(new[] { typeof(FpsWeaponDefinition), typeof(FpsAmmoType), typeof(RpgLevelCurve) });
            var paths = new HashSet<string>();
            foreach (var type in types)
            foreach (var guid in AssetDatabase.FindAssets("t:" + type.Name))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            foreach (var path in paths.OrderBy(path => path, StringComparer.Ordinal))
            foreach (var target in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(target is FpsItemDefinition || target is FpsWeaponDefinition ||
                      target is FpsAmmoType || target is RpgLevelCurve)) continue;
                assetEntries.Add(new Entry
                {
                    Target = target,
                    Name = target.name,
                    Type = target.GetType().Name,
                    Location = path
                });
            }
        }
    }
}
