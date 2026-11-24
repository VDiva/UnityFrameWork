using System;
using System.Collections.Generic;
using System.Linq;
using FrameWork.FPS;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FrameWork.RPG.Editor
{
    public sealed class RpgStructureWindow : EditorWindow
    {
        private enum Tab { Overview, Scene, Assets }
        private enum AssetSection { None, Firearms, Melee, AmmoTypes, Projectiles, AmmoPacks, Items, Growth, Enemies, BehaviorTrees, Skills }

        private sealed class Entry
        {
            public Object Target;
            public string Name;
            public string Type;
            public string Location;
            public AssetSection Section;
        }

        private static readonly Color BattleColor = new Color(0.26f, 0.63f, 0.9f);
        private static readonly Color ItemColor = new Color(0.32f, 0.72f, 0.54f);
        private static readonly Color GrowthColor = new Color(0.86f, 0.62f, 0.31f);
        private static readonly string[] Tabs = { "架构总览", "场景组件", "资产配置" };
        private static readonly string[] AssetFilters = { "全部类型", "武器", "子弹与弹药", "其他道具", "角色成长", "敌人", "技能" };
        private static readonly string[] CreateLabels = { "新建资产", "新建武器", "新建子弹", "新建道具", "新建等级曲线", "新建敌人", "新建技能" };
        private static readonly string[] SectionNames =
        {
            "", "武器 · 枪械", "武器 · 近战", "子弹与弹药 · 弹种", "子弹与弹药 · 弹丸预制体",
            "子弹与弹药 · 弹药包", "其他道具", "角色成长 · 等级曲线", "敌人 · 行为配置", "敌人 · 行为树"
        };

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
        private int assetFilter;
        private Object inspectorTarget;
        private PropertyTree inspector;
        private GUIStyle nodeStyle;
        private GUIStyle headingStyle;
        private GUIStyle entryTitleStyle;
        private GUIStyle entryInfoStyle;
        private readonly RpgAssetPreview assetPreview = new RpgAssetPreview();

        private static Color Surface => EditorGUIUtility.isProSkin ? new Color(0.20f, 0.22f, 0.25f) : new Color(0.89f, 0.92f, 0.96f);
        private static Color SelectedSurface => EditorGUIUtility.isProSkin ? new Color(0.16f, 0.30f, 0.44f) : new Color(0.76f, 0.87f, 0.99f);

        [MenuItem("FrameWork/RPG/架构编辑面板")]
        public static void Open()
        {
            var window = GetWindow<RpgStructureWindow>();
            window.titleContent = new GUIContent("RPG 架构");
            window.minSize = new Vector2(600f, 320f);
            window.Show();
        }

        [MenuItem("FrameWork/RPG/打开 Ember Vault 示例")]
        private static void OpenDungeonExample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/FrameWork/RPG/Example/Dungeon/EmberVault.unity");
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
            ReleaseInspector();
        }

        private void OnInspectorUpdate()
        {
            if (assetPreview.IsLoading) Repaint();
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
            DrawHeader();
            SirenixEditorGUI.BeginHorizontalToolbar(28f);
            try
            {
                var next = (Tab)GUILayout.Toolbar((int)tab, Tabs, EditorStyles.toolbarButton);
                if (next != tab) SwitchTab(next);
                GUILayout.FlexibleSpace();
                if (SirenixEditorGUI.ToolbarButton(EditorIcons.Refresh))
                {
                    InvalidateScene();
                    InvalidateAssets();
                }
            }
            finally { SirenixEditorGUI.EndHorizontalToolbar(); }

            if (tab == Tab.Overview)
            {
                DrawOverview();
                return;
            }

            if (tab == Tab.Scene && sceneDirty) RefreshScene();
            if (tab == Tab.Assets && assetsDirty) RefreshAssets();
            DrawBrowser(tab == Tab.Scene ? sceneEntries : assetEntries);
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 72f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Surface);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), BattleColor);
            headingStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 };
            GUI.Label(new Rect(rect.x + 16f, rect.y + 10f, rect.width - 28f, 28f), "RPG 架构编辑器", headingStyle);
            GUI.Label(new Rect(rect.x + 17f, rect.y + 42f, rect.width - 29f, 18f),
                "战斗、装备和成长系统", EditorStyles.miniLabel);
        }

        private void DrawAssetTypeBar()
        {
            using var row = new EditorGUILayout.HorizontalScope();
            int next = EditorGUILayout.Popup(assetFilter, AssetFilters);
            if (next != assetFilter)
            {
                assetFilter = next;
                listScroll = Vector2.zero;
            }

            if (!GUILayout.Button(CreateLabels[assetFilter] + " ▾", GUILayout.Width(110f))) return;
            var menu = new GenericMenu();
            bool all = assetFilter == 0;
            if (all || assetFilter == 1)
            {
                AddCreateItem(menu, all ? "武器/枪械" : "枪械", () => RpgAssetCreation.CreateWeapon(FpsWeaponKind.Firearm));
                AddCreateItem(menu, all ? "武器/近战武器" : "近战武器", () => RpgAssetCreation.CreateWeapon(FpsWeaponKind.Melee));
            }
            if (all || assetFilter == 2)
            {
                AddCreateItem(menu, all ? "子弹与弹药/弹丸预制体" : "弹丸预制体", RpgAssetCreation.CreateProjectile);
                AddCreateItem(menu, all ? "子弹与弹药/弹种" : "弹种", () => RpgAssetCreation.CreateAsset<FpsAmmoType>("新弹种"));
                AddCreateItem(menu, all ? "子弹与弹药/弹药包" : "弹药包", () => RpgAssetCreation.CreateAsset<FpsAmmoItem>("新弹药包"));
            }
            if (all || assetFilter == 3)
            {
                AddCreateItem(menu, all ? "其他道具/治疗包" : "治疗包", () => RpgAssetCreation.CreateAsset<FpsHealthItem>("新治疗包"));
                AddCreateItem(menu, all ? "其他道具/状态效果" : "状态效果", () => RpgAssetCreation.CreateAsset<RpgStatusEffect>("新状态效果"));
                AddCreateItem(menu, all ? "其他道具/武器拾取" : "武器拾取", () => RpgAssetCreation.CreateAsset<FpsWeaponItem>("新武器拾取道具"));
            }
            if (all || assetFilter == 4)
                AddCreateItem(menu, "等级曲线", () => RpgAssetCreation.CreateAsset<RpgLevelCurve>("新等级曲线"));
            if (all || assetFilter == 5)
            {
                AddCreateItem(menu, all ? "敌人/行为配置" : "行为配置", () => RpgAssetCreation.CreateAsset<RpgEnemyDefinition>("新敌人"));
                AddCreateItem(menu, all ? "敌人/行为树" : "行为树", RpgBehaviorTreeWindow.CreateAsset);
            }
            if (all || assetFilter == 6)
                AddCreateItem(menu, "技能配置", () => RpgAssetCreation.CreateAsset<RpgSkillDefinition>("新技能"));
            menu.DropDown(GUILayoutUtility.GetLastRect());
        }

        private void AddCreateItem(GenericMenu menu, string label, Func<Object> create)
        {
            menu.AddItem(new GUIContent(label), false, () => EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                var created = create();
                if (created == null) return;
                ReleaseInspector();
                selected = created;
                query = "";
                listScroll = Vector2.zero;
                inspectorScroll = Vector2.zero;
                InvalidateAssets();
            });
        }

        private void SwitchTab(Tab next)
        {
            ReleaseInspector();
            tab = next;
            selected = null;
            query = "";
            assetFilter = 0;
            listScroll = Vector2.zero;
            inspectorScroll = Vector2.zero;
            if (Event.current != null) GUI.FocusControl(null);
        }

        private void DrawOverview()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(overviewScroll))
            {
                overviewScroll = scroll.scrollPosition;
                EditorGUILayout.Space(8f);
                DrawFlow("战斗", BattleColor, "输入与视角\nFpsPlayer", "装备切换\nFpsLoadout",
                    "枪械或近战\nFpsWeapon", "命中与生命\nIFpsDamageable / FpsHealth");
                DrawFlow("背包与道具", ItemColor, "场景拾取\nFpsItemPickup", "背包与使用\nFpsInventory",
                    "道具效果\nFpsItemDefinition", "补给 / 治疗 / 装备");
                DrawFlow("角色成长", GrowthColor, "目标击败\nFpsHealth.Defeated", "经验归属\nRpgExperienceReward",
                    "等级进度\nRpgProgression", "经验曲线\nRpgLevelCurve");
                DrawFlow("敌人", BattleColor, "行为配置\nRpgEnemyDefinition", "发现与追击\nRpgEnemyAgent",
                    "攻击\nFpsWeapon", "击败与奖励\nRpgExperienceReward");
                DrawFlow("武器动画", BattleColor, "武器配置\nFpsWeaponDefinition", "装备变更\nFpsLoadout",
                    "动画驱动\nFpsWeaponAnimator", "状态机\nAnimator Controller");
                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("查看场景组件")) SwitchTab(Tab.Scene);
                    if (GUILayout.Button("查看资产配置")) SwitchTab(Tab.Assets);
                }
            }
        }

        private void DrawFlow(string title, Color accent, params string[] nodes)
        {
            EditorGUILayout.Space(8f);
            SirenixEditorGUI.BeginBox(title);
            try
            {
                using var row = new EditorGUILayout.HorizontalScope();
                nodeStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(5, 5, 4, 4)
                };
                float nodeWidth = (position.width - 110f) / nodes.Length;
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (i > 0) GUILayout.Label("→", GUILayout.Width(16f), GUILayout.Height(48f));
                    Rect rect = GUILayoutUtility.GetRect(nodeWidth, 48f, GUILayout.Width(nodeWidth));
                    EditorGUI.DrawRect(rect, Surface);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), accent);
                    GUI.Label(rect, nodes[i], nodeStyle);
                }
            }
            finally { SirenixEditorGUI.EndBox(); }
        }

        private void DrawBrowser(List<Entry> entries)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(position.width * 0.4f, 232f, 420f))))
                {
                    EditorGUILayout.Space(6f);
                    query = SirenixEditorGUI.ToolbarSearchField(query);
                    if (tab == Tab.Assets) DrawAssetTypeBar();
                    EditorGUILayout.LabelField(tab == Tab.Scene ? "已加载场景" : "项目配置", EditorStyles.boldLabel);
                    using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll))
                    {
                        listScroll = scroll.scrollPosition;
                        bool found = false;
                        var section = AssetSection.None;
                        foreach (var entry in entries)
                        {
                            if (entry.Target == null || !Matches(entry)) continue;
                            found = true;
                            if (entry.Section != section)
                            {
                                section = entry.Section;
                                EditorGUILayout.Space(6f);
                                EditorGUILayout.LabelField(SectionNames[(int)section], EditorStyles.boldLabel);
                            }
                            DrawEntry(entry);
                        }
                        if (!found) EditorGUILayout.HelpBox("没有匹配的组件或配置。", MessageType.Info);
                    }
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(inspectorScroll))
                    {
                        inspectorScroll = scroll.scrollPosition;
                        DrawInspector();
                    }
                }
            }
        }

        private void DrawEntry(Entry entry)
        {
            entryTitleStyle ??= new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            entryInfoStyle ??= new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            Rect rect = GUILayoutUtility.GetRect(0f, 54f, GUILayout.ExpandWidth(true));
            bool current = selected == entry.Target;
            EditorGUI.DrawRect(rect, current ? SelectedSurface : Surface);
            if (current) EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), BattleColor);
            var icon = EditorGUIUtility.GetIconForObject(entry.Target);
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 9f, rect.y + 8f, 16f, 16f), icon, ScaleMode.ScaleToFit);
            float width = rect.width - 36f;
            GUI.Label(new Rect(rect.x + 30f, rect.y + 4f, width, 18f), entry.Name, entryTitleStyle);
            GUI.Label(new Rect(rect.x + 30f, rect.y + 21f, width, 15f), entry.Type, entryInfoStyle);
            GUI.Label(new Rect(rect.x + 30f, rect.y + 36f, width, 15f), entry.Location, entryInfoStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && !current)
            {
                ReleaseInspector();
                selected = entry.Target;
                inspectorScroll = Vector2.zero;
            }
            EditorGUILayout.Space(3f);
        }

        private void DrawInspector()
        {
            if (selected == null)
            {
                ReleaseInspector();
                EditorGUILayout.HelpBox("从左侧选择一个组件或配置资产。", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(selected.name, EditorStyles.largeLabel);
                    EditorGUILayout.LabelField(selected.GetType().Name, EditorStyles.miniLabel);
                }
                if (GUILayout.Button("定位", GUILayout.Width(52f)))
                {
                    Selection.activeObject = selected is Component component ? component.gameObject : selected;
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }

            if (Application.isPlaying && selected is FpsHealth health)
            {
                EditorGUILayout.LabelField("运行时属性", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("生命", $"{health.CurrentHealth:0.0} / {health.MaxHealth:0.0}");
                EditorGUILayout.LabelField("护甲", $"{health.Armor:0.##}");
                EditorGUILayout.LabelField("移速倍率", $"{health.MovementMultiplier:0.##}");
                foreach (var effect in health.ActiveEffects)
                {
                    if (effect.Definition == null) continue;
                    string source = effect.Source != null ? effect.Source.name : "—";
                    EditorGUILayout.LabelField(effect.Definition.name, $"{effect.Remaining:0.0}s · {source}");
                }
                Repaint();
            }

            if (selected is FpsWeaponDefinition definition)
            {
                using var settings = new SerializedObject(definition);
                RpgWeaponInspectorLayout.DrawAttack(settings, definition.Kind == FpsWeaponKind.Melee);
                assetPreview.Draw(selected);
                RpgWeaponInspectorLayout.DrawDefinition(settings);
                if (settings.ApplyModifiedProperties()) InvalidateAssets();
                RpgWeaponEditorActions.DrawActions(definition, ref targetLoadout);
                return;
            }

            if (selected is FpsWeapon weapon)
            {
                using var instanceSettings = new SerializedObject(weapon);
                var weaponDefinition = weapon.Definition;
                if (weaponDefinition != null)
                {
                    EditorGUILayout.LabelField("共享武器配置", weaponDefinition.DisplayName, EditorStyles.miniLabel);
                    using var settings = new SerializedObject(weaponDefinition);
                    RpgWeaponInspectorLayout.DrawAttack(settings, weapon.IsMelee);
                    if (settings.ApplyModifiedProperties()) InvalidateAssets();
                }
                else
                {
                    RpgWeaponInspectorLayout.DrawAttack(instanceSettings, false);
                }
                assetPreview.Draw(selected);
                RpgWeaponInspectorLayout.DrawInstance(instanceSettings);
                instanceSettings.ApplyModifiedProperties();
                return;
            }

            if (selected is FpsProjectile projectile && EditorUtility.IsPersistent(projectile))
                DrawProjectileWeapons(projectile);

            if (selected is RpgBehaviorTree tree)
            {
                EditorGUILayout.LabelField("节点", tree.nodes.Count.ToString());
                EditorGUILayout.LabelField("连线", tree.edges.Count.ToString());
                DrawBehaviorTreeActions(tree);
                return;
            }

            if (selected is RpgEnemyAgent enemy && enemy.BehaviorTree != null)
                DrawBehaviorTreeActions(enemy.BehaviorTree, enemy);

            assetPreview.Draw(selected);
            if (inspectorTarget != selected)
            {
                inspector?.Dispose();
                inspector = PropertyTree.Create(new SerializedObject(selected));
                inspectorTarget = selected;
            }
            inspector?.Draw(true);
        }

        private static void DrawBehaviorTreeActions(RpgBehaviorTree tree, RpgEnemyAgent debugTarget = null)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button("打开行为树", GUILayout.Height(28f)))
                EditorApplication.delayCall += () => RpgBehaviorTreeWindow.Open(tree, debugTarget);
            if (GUILayout.Button("新建动作脚本", GUILayout.Height(28f)))
                EditorApplication.delayCall += RpgActionScriptCreator.Create;

            if (!GUILayout.Button("插入已有动作 ▾", GUILayout.Height(28f))) return;
            var menu = new GenericMenu();
            foreach (RpgEnemyAction action in Enum.GetValues(typeof(RpgEnemyAction)))
            {
                string name = action.ToString();
                var field = typeof(RpgEnemyAction).GetField(name);
                var title = Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) as InspectorNameAttribute;
                menu.AddItem(new GUIContent(title?.displayName ?? ObjectNames.NicifyVariableName(name)), false,
                    () => EditorApplication.delayCall += () => RpgBehaviorTreeWindow.AddAction(tree, action));
            }
            foreach (var type in TypeCache.GetTypesDerivedFrom<RpgCustomBehaviorAction>()
                         .Where(type => !type.IsAbstract && !type.ContainsGenericParameters)
                         .OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                string name = type.Name.StartsWith(RpgActionScriptCreator.ClassPrefix, StringComparison.Ordinal)
                    ? type.Name.Substring(RpgActionScriptCreator.ClassPrefix.Length) : type.Name;
                menu.AddItem(new GUIContent("自定义/" + ObjectNames.NicifyVariableName(name)), false,
                    () => EditorApplication.delayCall += () => RpgBehaviorTreeWindow.AddCustomAction(tree, type));
            }
            menu.DropDown(GUILayoutUtility.GetLastRect());
        }

        private void DrawProjectileWeapons(FpsProjectile projectile)
        {
            foreach (var entry in assetEntries)
            {
                if (!(entry.Target is FpsWeaponDefinition definition) || definition.ProjectilePrefab != projectile) continue;
                EditorGUILayout.LabelField(definition.DisplayName, EditorStyles.boldLabel);
                using var settings = new SerializedObject(definition);
                RpgWeaponInspectorLayout.DrawAttack(settings, false);
                if (settings.ApplyModifiedProperties()) InvalidateAssets();
            }
            EditorGUILayout.HelpBox("弹丸速度、射程和伤害保存在使用它的武器配置中。", MessageType.Info);
        }

        private void ReleaseInspector()
        {
            inspector?.Dispose();
            inspector = null;
            inspectorTarget = null;
            assetPreview.Dispose();
        }

        private bool Matches(Entry entry)
        {
            bool included = tab != Tab.Assets || (assetFilter switch
            {
                1 => entry.Section is AssetSection.Firearms or AssetSection.Melee,
                2 => entry.Section is AssetSection.AmmoTypes or AssetSection.Projectiles or AssetSection.AmmoPacks,
                3 => entry.Section == AssetSection.Items,
                4 => entry.Section == AssetSection.Growth,
                5 => entry.Section is AssetSection.Enemies or AssetSection.BehaviorTrees,
                6 => entry.Section == AssetSection.Skills,
                _ => true
            });
            return included && (string.IsNullOrWhiteSpace(query) ||
                entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Type.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                SectionNames[(int)entry.Section].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.Location.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

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
                .Concat(new[] { typeof(FpsWeaponDefinition), typeof(FpsAmmoType), typeof(RpgLevelCurve),
                    typeof(RpgEnemyDefinition), typeof(RpgBehaviorTree), typeof(RpgStatusEffect), typeof(RpgSkillDefinition) });
            var paths = new HashSet<string>();
            foreach (var type in types)
            foreach (var guid in AssetDatabase.FindAssets("t:" + type.Name))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            var projectiles = new HashSet<FpsProjectile>();
            foreach (var path in paths)
            foreach (var target in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var section = target switch
                {
                    FpsWeaponDefinition weapon => weapon.Kind == FpsWeaponKind.Melee ? AssetSection.Melee : AssetSection.Firearms,
                    FpsAmmoType => AssetSection.AmmoTypes,
                    FpsAmmoItem => AssetSection.AmmoPacks,
                    FpsItemDefinition => AssetSection.Items,
                    RpgStatusEffect => AssetSection.Items,
                    RpgLevelCurve => AssetSection.Growth,
                    RpgEnemyDefinition => AssetSection.Enemies,
                    RpgBehaviorTree => AssetSection.BehaviorTrees,
                    RpgSkillDefinition => AssetSection.Skills,
                    _ => AssetSection.None
                };
                if (section == AssetSection.None) continue;
                AddAssetEntry(target, path, section);
                if (target is FpsWeaponDefinition definition && definition.ProjectilePrefab != null &&
                    EditorUtility.IsPersistent(definition.ProjectilePrefab))
                    projectiles.Add(definition.ProjectilePrefab);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (var projectile in prefab.GetComponentsInChildren<FpsProjectile>(true))
                    projectiles.Add(projectile);
            }

            foreach (var projectile in projectiles)
                AddAssetEntry(projectile, AssetDatabase.GetAssetPath(projectile), AssetSection.Projectiles);

            assetEntries.Sort((a, b) =>
            {
                int section = a.Section.CompareTo(b.Section);
                if (section != 0) return section;
                int name = string.CompareOrdinal(a.Name, b.Name);
                return name != 0 ? name : string.CompareOrdinal(a.Location, b.Location);
            });
        }

        private void AddAssetEntry(Object target, string path, AssetSection section)
        {
            assetEntries.Add(new Entry
            {
                Target = target,
                Name = target.name,
                Type = target.GetType().Name,
                Location = path,
                Section = section
            });
        }
    }
}
