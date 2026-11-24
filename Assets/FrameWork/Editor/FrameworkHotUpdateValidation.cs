using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    internal static class FrameworkHotUpdateValidation
    {
        private const string Root = "Library/FrameWork/Validation/";

        [MenuItem("FrameWork/验证/检查项目资源")]
        public static void ValidateResources()
        {
            int issues = 0;
            void Report(string message, UnityEngine.Object asset = null)
            {
                issues++;
                Debug.LogError(message, asset);
            }

            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(FrameworkIntegrationEditor.BootSettingsPath);
            var resource = AssetDatabase.LoadAssetAtPath<YooResourceSettings>(YooResourceEditor.SettingsPath);
            var bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FrameworkIntegrationEditor.BootScenePath);
            if (boot == null || string.IsNullOrWhiteSpace(boot.entryAssembly) || string.IsNullOrWhiteSpace(boot.entryType) ||
                string.IsNullOrWhiteSpace(boot.entryMethod) || boot.hotUpdateDlls == null || !boot.hotUpdateDlls.Contains(boot.entryAssembly + ".dll"))
                Report("启动配置缺失或入口程序集不在热更新 DLL 列表中。", boot);
            if (bootScene == null || EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled)?.path != FrameworkIntegrationEditor.BootScenePath)
                Report("Boot 场景缺失，或未设为首个启用的构建场景。", bootScene);
            else
            {
                try { FrameworkHotUpdateLayout.ValidateBootScene(); }
                catch (Exception error) { Report(error.Message, bootScene); }
            }

            string root = ABEditor.abAssetPath;
            if (!AssetDatabase.IsValidFolder(root)) Report("资源目录不存在：" + root);
            else
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                            Report($"预制体有缺失脚本：{path}（节点 {child.name}）", prefab);
                }
            }

            if (resource == null || string.IsNullOrWhiteSpace(resource.packageName))
                Report("YooAsset 配置或资源包名称缺失。", resource);
            else if (AssetDatabase.IsValidFolder(root))
            {
                try
                {
                    var collected = YooAsset.Editor.BundleCollectorSettingData.Setting.BeginCollect(resource.packageName, true, false, true);
                    var mainAssets = collected.CollectAssets.Where(asset => asset.CollectorType == YooAsset.Editor.ECollectorType.MainAssetCollector).ToArray();
                    foreach (var group in mainAssets.Where(asset => !string.IsNullOrEmpty(asset.Address)).GroupBy(asset => asset.Address).Where(group => group.Count() > 1))
                        Report($"重复资源地址 {group.Key}：\n" + string.Join("\n", group.Select(asset => asset.AssetInfo.AssetPath)), resource);
                    var paths = new HashSet<string>(mainAssets.Select(asset => asset.AssetInfo.AssetPath), StringComparer.Ordinal);
                    foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { root }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!paths.Contains(path)) Report("场景未被收集为 YooAsset 主资源：" + path, AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
                    }
                }
                catch (Exception error) { Report("YooAsset 收集检查失败：" + error.Message, resource); }
            }

            if (issues == 0) Debug.Log("FRAMEWORK_RESOURCE_CHECK_OK：项目资源检查通过。");
            else Debug.LogWarning($"项目资源检查发现 {issues} 项问题，详见上方 Console 日志。");
            if (Application.isBatchMode) EditorApplication.Exit(issues == 0 ? 0 : 1);
        }
        [Serializable] private sealed class Result
        {
            public bool success;
            public string error;
            public List<string> passed = new();
        }

        [InitializeOnLoadMethod]
        private static void Register() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(Root + "HotUpdateLayout.request")) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunRequested;
                return;
            }
            File.Delete(Root + "HotUpdateLayout.request");
            Run();
        }

        [MenuItem("FrameWork/验证/检查热更新边界")]
        internal static void Run()
        {
            var result = new Result();
            try
            {
                FrameworkIntegrationEditor.Configure();
                void Check(bool condition, string label)
                {
                    if (!condition) throw new InvalidOperationException(label);
                    result.passed.Add(label);
                }
                Check(typeof(FrameworkLauncher).Assembly.GetName().Name == "FrameWork.Bootstrap" &&
                    typeof(ABMrg).Assembly == typeof(FrameworkLauncher).Assembly, "启动器和资源下载处于独立 AOT 启动壳");
                Check(!PlayerSettings.stripEngineCode, "动态资源使用的原生引擎组件未被裁剪");
                Check(typeof(Config).Assembly.GetName().Name == "HotUpdate" &&
                    SettingsUtil.HotUpdateAssemblyNamesExcludePreserved.Contains("HotUpdate") &&
                    !SettingsUtil.HotUpdateAssemblyNamesExcludePreserved.Contains("FrameWork.Runtime"), "框架业务和游戏逻辑已合并到 HotUpdate");
                var dlls = FrameworkHotUpdateLayout.OrderedDlls();
                Check(dlls.Contains("HotUpdate.dll") && !dlls.Contains("FrameWork.Runtime.dll"), "不再发布独立的 FrameWork.Runtime.dll，AOT 无反向依赖");
                FrameworkHotUpdateLayout.ValidateBootScene();
                result.passed.Add("启动场景及其预制体未提前引用业务脚本");
                var inputs = new List<ReleaseFile>();
                string fingerprint = FrameworkReleaseEditor.Fingerprint(inputs);
                var paths = new HashSet<string>(inputs.Select(x => x.path));
                Check(paths.Contains("Assets/FrameWork/Tool/Bootstrap/FrameworkLauncher.cs"), "启动壳修改仍受客户端基线保护");
                Check(!paths.Contains("Assets/FrameWork/Tool/Config.cs") &&
                    !paths.Contains("Assets/FrameWork/Tool/UiTool/UiManager.cs") &&
                    !paths.Contains("Assets/FrameWork/Scripts/GameEntry.cs"), "框架业务和游戏代码不参与客户端基线");
                Check(!File.Exists(FrameworkIntegrationEditor.DllDirectory + "/FrameWork.Runtime.dll.bytes") &&
                    !paths.Contains(FrameworkIntegrationEditor.DllDirectory + "/HotUpdate.dll.bytes"), "旧框架 DLL 已移除，统一热更新 DLL 不参与 AOT 指纹");
                Check(!paths.Contains(Config.AssetPath) && AssetDatabase.LoadAssetAtPath<ConfigData>(Config.AssetPath) != null &&
                    !File.Exists("Assets/FrameWork/Resources/ConfigData.asset"), "业务配置已迁入 YooAsset，不再嵌入 Resources");
                Check(fingerprint == FrameworkReleaseEditor.Fingerprint(), "基线指纹重复计算一致");
                result.success = true;
            }
            catch (Exception error) { result.error = error.ToString(); }
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root + "HotUpdateLayout.json", JsonUtility.ToJson(result, true));
            if (result.success) Debug.Log("FRAMEWORK_HOTUPDATE_LAYOUT_OK");
            else Debug.LogError("FRAMEWORK_HOTUPDATE_LAYOUT_FAILED: " + result.error);
        }
    }
}
