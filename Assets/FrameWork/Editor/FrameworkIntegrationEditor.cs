using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HybridCLR;
using HybridCLR.Commands;
using HybridCLR.Installer;
using HybridCLR.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditorInternal;

namespace FrameWork.Editor
{
    [InitializeOnLoad]
    public static class FrameworkIntegrationEditor
    {
        private const string PrepareStage = "FrameWork.PrepareClient.Stage";
        private const string PrepareBatch = "FrameWork.PrepareClient.Batch";
        private const string PrepareForce = "FrameWork.PrepareClient.Force";
        private static double _nextPrepare;
        static FrameworkIntegrationEditor() => EditorApplication.update += AdvancePreparation;
        public const string BootSettingsPath = "Assets/FrameWork/Resources/FrameworkBootSettings.asset";
        public const string BootScenePath = "Assets/FrameWork/Scenes/Boot.unity";
        public const string DllDirectory = "Assets/FrameWork/Asset/Dll";

        [MenuItem("FrameWork/HybridCLR/配置热更新程序集和启动场景")]
        public static void Configure()
        {
            var hot = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>("Assets/FrameWork/HotUpdate.asmdef");
            if (hot == null) throw new InvalidOperationException("HotUpdate.asmdef 尚未导入，请等 Unity 编译完成。");
            var settings = HybridCLRSettings.instance;
            settings.enable = true;
            settings.useGlobalIl2cpp = false;
            settings.hotUpdateAssemblyDefinitions = (settings.hotUpdateAssemblyDefinitions ?? Array.Empty<AssemblyDefinitionAsset>())
                .Where(a => a != null && a.name != "FrameWork.Runtime")
                .Concat(new[] { hot }).Distinct().ToArray();
            settings.hotUpdateAssemblies = (settings.hotUpdateAssemblies ?? Array.Empty<string>())
                .Where(n => n != "HotUpdate" && n != "FrameWork.Runtime").ToArray();
            settings.outputLinkFile = "FrameWork/Generated/HybridCLR/link.xml";
            settings.outputAOTGenericReferenceFile = "FrameWork/Generated/HybridCLR/AOTGenericReferences.cs";
            settings.hybridclrRepoURL = "https://github.com/focus-creative-games/hybridclr";
            settings.il2cppPlusRepoURL = "https://github.com/focus-creative-games/il2cpp_plus";
            HybridCLRSettings.Save();
            // 动态资源中的原生引擎组件必须保留。
            PlayerSettings.stripEngineCode = false;
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (PlayerSettings.GetScriptingBackend(group) != ScriptingImplementation.IL2CPP)
                PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
            if (PlayerSettings.GetApiCompatibilityLevel(group) != ApiCompatibilityLevel.NET_Unity_4_8)
                PlayerSettings.SetApiCompatibilityLevel(group, ApiCompatibilityLevel.NET_Unity_4_8);
            Directory.CreateDirectory("Assets/FrameWork/Resources");
            Directory.CreateDirectory("Assets/FrameWork/Scenes");
            Directory.CreateDirectory(DllDirectory);
            Directory.CreateDirectory("Assets/FrameWork/Generated/HybridCLR");
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(BootSettingsPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FrameworkBootSettings>(), BootSettingsPath);
            if (!File.Exists(BootScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var launcher = new GameObject("FrameworkLauncher");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(launcher, scene);
                launcher.AddComponent<FrameworkLauncher>();
                EditorSceneManager.SaveScene(scene, BootScenePath);
                EditorSceneManager.CloseScene(scene, true);
            }
            // 安装包只包含启动场景；业务场景必须在 YooAsset 中加载，避免提前引用热更新脚本。
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
            FrameworkHotUpdateLayout.ValidateBootScene();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("FrameWork/HybridCLR/安装项目本地解释器")]
        public static void InstallNative()
        {
            var installer = new InstallerController();
            if (installer.HasInstalledHybridCLR() && installer.InstalledLibil2cppVersion == installer.PackageVersion) return;
            string local = Path.GetFullPath("Library/FrameWork/HybridCLRSource/libil2cpp");
            if (Directory.Exists(Path.Combine(local, "hybridclr"))) installer.InstallFromLocal(local);
            else installer.InstallDefaultHybridCLR();
            if (!installer.HasInstalledHybridCLR()) throw new InvalidOperationException("HybridCLR 本地解释器安装失败。");
        }

        [MenuItem("FrameWork/HybridCLR/编译并复制热更新 DLL")]
        public static void CompileHotUpdate()
        {
            Configure();
            var target = EditorUserBuildSettings.activeBuildTarget;
            string output = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            Directory.CreateDirectory(output);
            var compilation = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(
                new UnityEditor.Build.Player.ScriptCompilationSettings {
                    group = BuildPipeline.GetBuildTargetGroup(target), target = target,
                    options = EditorUserBuildSettings.development ?
                        UnityEditor.Build.Player.ScriptCompilationOptions.DevelopmentBuild : UnityEditor.Build.Player.ScriptCompilationOptions.None
                }, output);
            if (compilation.assemblies == null || compilation.assemblies.Count == 0)
                throw new InvalidOperationException("热更新 Player 程序集编译失败，未使用上一次的旧 DLL。");
            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(BootSettingsPath);
            boot.hotUpdateDlls = FrameworkHotUpdateLayout.OrderedDlls();
            foreach (string dll in boot.hotUpdateDlls)
            {
                string source = Path.Combine(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target), dll);
                if (!File.Exists(source)) throw new FileNotFoundException("热更新编译未产生 DLL", source);
                File.Copy(source, Path.Combine(DllDirectory, dll + ".bytes"), true);
            }
            EditorUtility.SetDirty(boot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static void CopyAotMetadata()
        {
            string generated = "Assets/FrameWork/Generated/HybridCLR/AOTGenericReferences.cs";
            string source = File.ReadAllText(generated);
            var section = Regex.Match(source, @"PatchedAOTAssemblyList\s*=[^{]*\{([\s\S]*?)\}");
            if (!section.Success) throw new InvalidOperationException("未找到 HybridCLR 生成的 PatchedAOTAssemblyList。");
            var dlls = Regex.Matches(section.Groups[1].Value, "\"([^\"]+\\.dll)\"")
                .Cast<Match>().Select(m => m.Groups[1].Value)
                .Concat(new[] { "mscorlib.dll", "System.dll", "System.Core.dll", "UniTask.dll", "FrameWork.Bootstrap.dll" })
                .Where(dll => !FrameworkHotUpdateLayout.AssemblyNames().Contains(Path.GetFileNameWithoutExtension(dll)))
                .Distinct().ToArray();
            string root = SettingsUtil.GetAssembliesPostIl2CppStripDir(EditorUserBuildSettings.activeBuildTarget);
            foreach (string dll in dlls) File.Copy(Path.Combine(root, dll), Path.Combine(DllDirectory, dll + ".bytes"), true);
            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(BootSettingsPath);
            boot.aotMetadataDlls = dlls;
            boot.clientBuildId = Guid.NewGuid().ToString("N");
            EditorUtility.SetDirty(boot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("FrameWork/发布/准备新的客户端基线（含 AOT）")]
        public static void PrepareClient() => QueuePreparation(false);

        public static void PrepareClientBatch() => QueuePreparation(true);

        [MenuItem("FrameWork/发布/强制重建客户端基线（含 AOT）")]
        public static void ForcePrepareClient() => QueuePreparation(false, true);

        private static void QueuePreparation(bool batch, bool force = false)
        {
            if (SessionState.GetString(PrepareStage, "") != "")
                throw new InvalidOperationException("客户端基线正在准备中，请等待完成。");
            FrameworkSetup.Setup();
            SessionState.SetBool(PrepareBatch, batch);
            SessionState.SetBool(PrepareForce, force);
            SessionState.SetString(PrepareStage, "generate");
            _nextPrepare = EditorApplication.timeSinceStartup + 2;
        }

        private static void AdvancePreparation()
        {
            string stage = SessionState.GetString(PrepareStage, "");
            if (stage == "" || EditorApplication.timeSinceStartup < _nextPrepare || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            SessionState.SetString(PrepareStage, "");
            try
            {
                if (stage == "generate")
                {
                    bool reuse = FrameworkReleaseEditor.CanReuseClientBaseline(out string reason);
                    if (SessionState.GetBool(PrepareForce, false)) reuse = false;
                    Debug.Log("[客户端基线] " + (SessionState.GetBool(PrepareForce, false) ? "用户强制重建 AOT" : reason));
                    if (!reuse)
                    {
                        RunPreparationStep("安装本地解释器", InstallNative);
                        RunPreparationStep("生成 AOT（包含 Unity 裁剪构建）", PrebuildCommand.GenerateAll);
                        RunPreparationStep("复制 AOT 元数据", CopyAotMetadata);
                    }
                    // 生成的 C# 要先完成 Unity 域重载，不能嵌套调用 Player 脚本编译。
                    SessionState.SetString(PrepareStage, reuse ? "finalize-reuse" : "finalize");
                    _nextPrepare = EditorApplication.timeSinceStartup + 2;
                }
                else
                {
                    if (stage == "finalize-reuse") AssertReusableBaseline();
                    RunPreparationStep("编译热更新 DLL", CompileHotUpdate);
                    if (stage == "finalize-reuse") AssertReusableBaseline();
                    RunPreparationStep("构建 YooAsset 资源", YooResourceEditor.BuildResources);
                    if (stage == "finalize-reuse") AssertReusableBaseline();
                    RunPreparationStep("保存客户端基线", FrameworkReleaseEditor.SaveClientBaseline);
                    FrameworkSetup.AssertLayout();
                    Debug.Log("FRAMEWORK_CLIENT_BASELINE_OK");
                    if (SessionState.GetBool(PrepareBatch, false)) EditorApplication.Exit(0);
                }
            }
            catch (Exception error)
            {
                YooBuiltinBuildProcessor.Cleanup(); Debug.LogException(error);
                if (SessionState.GetBool(PrepareBatch, false)) EditorApplication.Exit(1);
            }
        }

        private static void AssertReusableBaseline()
        {
            if (!FrameworkReleaseEditor.CanReuseClientBaseline(out string reason))
                throw new InvalidOperationException("准备期间客户端输入发生变化，请重新准备基线。" + reason);
        }

        private static void RunPreparationStep(string label, Action action)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("[客户端基线] 开始：" + label);
            try { action(); }
            finally { Debug.Log($"[客户端基线] {label}，耗时 {timer.Elapsed.TotalSeconds:F1} 秒"); }
        }

        [MenuItem("FrameWork/打开启动场景")]
        public static void OpenBootScene() => EditorSceneManager.OpenScene(BootScenePath);
    }
}
