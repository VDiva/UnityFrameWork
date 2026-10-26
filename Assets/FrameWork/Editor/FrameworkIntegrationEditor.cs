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
        private static double _nextPrepare;
        static FrameworkIntegrationEditor() => EditorApplication.update += AdvancePreparation;
        public const string BootSettingsPath = "Assets/FrameWork/Resources/FrameworkBootSettings.asset";
        public const string BootScenePath = "Assets/FrameWork/Scenes/Boot.unity";
        public const string DllDirectory = "Assets/FrameWork/Asset/Dll";

        [MenuItem("FrameWork/HybridCLR/配置热更新程序集和启动场景")]
        public static void Configure()
        {
            var hot = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>("Assets/FrameWork/Scripts/HotUpdate.asmdef");
            if (hot == null) throw new InvalidOperationException("HotUpdate.asmdef 尚未导入，请等 Unity 编译完成。");
            var settings = HybridCLRSettings.instance;
            settings.enable = true;
            settings.useGlobalIl2cpp = false;
            settings.hotUpdateAssemblyDefinitions = (settings.hotUpdateAssemblyDefinitions ?? Array.Empty<AssemblyDefinitionAsset>())
                .Where(a => a != null).Concat(new[] { hot }).Distinct().ToArray();
            settings.hotUpdateAssemblies = (settings.hotUpdateAssemblies ?? Array.Empty<string>()).Where(n => n != "HotUpdate").ToArray();
            settings.outputLinkFile = "FrameWork/Generated/HybridCLR/link.xml";
            settings.outputAOTGenericReferenceFile = "FrameWork/Generated/HybridCLR/AOTGenericReferences.cs";
            settings.hybridclrRepoURL = "https://github.com/focus-creative-games/hybridclr";
            settings.il2cppPlusRepoURL = "https://github.com/focus-creative-games/il2cpp_plus";
            HybridCLRSettings.Save();
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
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != BootScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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
            boot.hotUpdateDlls = SettingsUtil.HotUpdateAssemblyFilesExcludePreserved.ToArray();
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
                .Concat(new[] { "mscorlib.dll", "System.dll", "System.Core.dll", "UniTask.dll", "FrameWork.Runtime.dll" })
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

        private static void QueuePreparation(bool batch)
        {
            FrameworkSetup.Setup();
            SessionState.SetBool(PrepareBatch, batch);
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
                    InstallNative();
                    PrebuildCommand.GenerateAll();
                    CopyAotMetadata();
                    // 生成的 C# 要先完成 Unity 域重载，不能嵌套调用 Player 脚本编译。
                    SessionState.SetString(PrepareStage, "finalize");
                    _nextPrepare = EditorApplication.timeSinceStartup + 2;
                }
                else
                {
                    CompileHotUpdate();
                    YooResourceEditor.BuildResources();
                    FrameworkReleaseEditor.SaveClientBaseline();
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

        [MenuItem("FrameWork/打开启动场景")]
        public static void OpenBootScene() => EditorSceneManager.OpenScene(BootScenePath);
    }
}
