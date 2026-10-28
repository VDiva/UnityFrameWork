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
