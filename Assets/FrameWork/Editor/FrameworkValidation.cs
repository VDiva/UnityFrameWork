using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FrameWork.Script.Mrg;
using FrameWork.Script.Role;

namespace FrameWork.Editor
{
    [Serializable] public sealed class IntegrationCheckResult
    {
        public bool success;
        public string error;
        public List<string> passed = new();
    }

    public static class FrameworkValidation
    {
        private const string Pending = "FrameWork.Validation.Pending";
        private const string Running = "FrameWork.Validation.Running";
        private const string Report = "FrameWork.Validation.Report";
        private const string HostOriginal = "FrameWork.Validation.HostOriginal";
        private const string HostMode = "FrameWork.Validation.HostMode";

        public static void ValidateSyncedProjectBatch()
        {
            try
            {
                FrameworkSetup.Setup();
                FrameworkSetup.AssertLayout();
                var baseline = JsonUtility.FromJson<ClientBaseline>(File.ReadAllText("ProjectSettings/FrameWorkReleaseBaseline.json"));
                if (baseline.aotFingerprint != FrameworkReleaseEditor.Fingerprint())
                    throw new InvalidOperationException("同步后客户端基线指纹不符。");
                FrameworkIntegrationEditor.InstallNative();
                Debug.Log("FRAMEWORK_SYNCED_PROJECT_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        // CI 在本轮 GenerateAll 已成功后，复用裁剪产物验证元数据打包和基线保存。
        public static void FinalizeGeneratedBaselineBatch()
        {
            try
            {
                FrameworkIntegrationEditor.Configure();
                FrameworkIntegrationEditor.CopyAotMetadata();
                FrameworkIntegrationEditor.CompileHotUpdate();
                YooResourceEditor.BuildResources();
                FrameworkReleaseEditor.SaveClientBaseline();
                Debug.Log("FRAMEWORK_METADATA_BASELINE_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void RunHostCold()
        {
            SessionState.SetBool("FrameWork.Validation.ColdHost", true);
            RunHost();
        }

        private sealed class ValidationRemote : YooAsset.IRemoteService
        {
            private readonly string _root;
            public ValidationRemote(string root) { _root = root; }
            public IReadOnlyList<string> GetRemoteUrls(string fileName) => new[] { _root + "/" + fileName };
        }

        public static void RunHost()
        {
            FrameworkSetup.Setup();
            SessionState.SetString(HostOriginal, Convert.ToBase64String(File.ReadAllBytes(YooResourceEditor.SettingsPath)));
            var settings = AssetDatabase.LoadAssetAtPath<YooResourceSettings>(YooResourceEditor.SettingsPath);
            settings.editorSimulate = false;
            settings.playerMode = YooPlayerMode.Host;
            settings.packageVersion = "";
            settings.remoteUrl = "http://127.0.0.1:18765/{platform}/{client}/{package}";
            settings.fallbackRemoteUrl = "";
            settings.testRemoteUrl = settings.remoteUrl;
            settings.testFallbackRemoteUrl = "";
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(HostMode, true);
            Run();
        }

        [InitializeOnLoadMethod]
        private static void Register() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("FrameWork/验证/运行资源与启动检查")]
        public static void Run()
        {
            FrameworkSetup.Setup();
            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(FrameworkIntegrationEditor.BootSettingsPath);
            foreach (string dll in boot.hotUpdateDlls)
                if (!File.Exists(Path.Combine(FrameworkIntegrationEditor.DllDirectory, dll + ".bytes")))
                    throw new InvalidOperationException("请先编译热更新 DLL：" + dll);
            SessionState.SetString(Report, JsonUtility.ToJson(new IntegrationCheckResult()));
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Running, false);
            EditorSceneManager.OpenScene(FrameworkIntegrationEditor.BootScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode && !SessionState.GetBool(Running, false))
            {
                SessionState.SetBool(Running, true);
                CheckRuntime().Forget();
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running, false))
            {
                SessionState.SetBool(Pending, false);
                SessionState.SetBool(Running, false);
                FrameworkSetup.AssertLayout();
                if (SessionState.GetBool(HostMode, false))
                {
                    File.WriteAllBytes(YooResourceEditor.SettingsPath, Convert.FromBase64String(SessionState.GetString(HostOriginal, "")));
                    AssetDatabase.ImportAsset(YooResourceEditor.SettingsPath);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                File.WriteAllText(ResultPath, SessionState.GetString(Report, "{}"));
                Debug.Log("FRAMEWORK_SMOKE_FINISHED：" + ResultPath);
                SessionState.SetBool(HostMode, false);
                SessionState.SetBool("FrameWork.Validation.ColdHost", false);
                if (Application.isBatchMode)
                    EditorApplication.Exit(JsonUtility.FromJson<IntegrationCheckResult>(SessionState.GetString(Report, "{}")).success ? 0 : 1);
            }
        }

        public static string ResultPath => Path.GetFullPath("Library/FrameWork/Validation/" + (SessionState.GetBool("FrameWork.Validation.ColdHost", false) ? "Cold-Host-Smoke.json" : SessionState.GetBool(HostMode, false) ? "Host-Smoke.json" : "Integration-Smoke.json"));

        private static bool _coldFactoryUsed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void ConfigureColdHost()
        {
                if (SessionState.GetBool("FrameWork.Validation.ColdHost", false))
                {
                    var boot = Resources.Load<FrameworkBootSettings>("FrameworkBootSettings");
                    string root = $"http://127.0.0.1:18765/{EditorUserBuildSettings.activeBuildTarget}/{boot.clientBuildId}";
                    string cache = Path.GetFullPath("Library/FrameWork/Validation/ColdCache/" + Guid.NewGuid().ToString("N"));
                    ABMrg.InitializeOptionsFactory = settings => {
                        _coldFactoryUsed = true;
                        return new YooAsset.HostPlayModeOptions {
                        BuiltinFileSystemParameters = null,
                        CacheFileSystemParameters = YooAsset.FileSystemParameters.CreateDefaultSandboxFileSystemParameters(
                            new ValidationRemote(root + "/" + settings.packageName), cache)
                    }; };
                }
        }

        private static async UniTask CheckRuntime()
        {
            var result = JsonUtility.FromJson<IntegrationCheckResult>(SessionState.GetString(Report, "{}"));
            try
            {
                await UniTask.WhenAll(ABMrg.Init(), ABMrg.Init(), ABMrg.Init()).Timeout(TimeSpan.FromSeconds(60));
                Check(ABMrg.IsInitialized, "并发初始化共享一次包初始化", result);
                if (SessionState.GetBool("FrameWork.Validation.ColdHost", false))
                    Check(_coldFactoryUsed, "空缓存测试确实禁用了内置资源并使用独立缓存", result);
                await UniTask.WaitUntil(() => FrameworkLauncher.HotUpdateStarted).Timeout(TimeSpan.FromSeconds(60));
                Check(true, "启动脚本经过 YooAsset 加载 DLL 并调用 HotUpdate.GameEntry", result);
                Check(UiManager.GetCamera() != null, "Scripts 热更新入口完成 UI 根节点注入", result);
                Check(typeof(UiActor).Assembly.GetType("FrameWork.UiRoot") == null,
                    "AOT 程序集不再包含生成的业务 UiRoot", result);
                var pair = await UniTask.WhenAll(ABMrg.LoadAsync<GameObject>("UiRoot"), ABMrg.LoadAsync<GameObject>("UiRoot"));
                Check(pair.Item1 == pair.Item2, "相同资源并发加载共用缓存", result);
                Check(ABMrg.Load<GameObject>("UiRoot") == pair.Item1, "同步读取已加载缓存", result);
                var instance = await ABMrg.InstantiateAsync("UiRoot");
                Check(instance != null && instance.GetComponent<YooInstanceLease>() != null, "实例持有独立资源句柄", result);
                ABMrg.ReleaseAsset<GameObject>("UiRoot");
                Check(!ABMrg.TryGetLoaded<GameObject>("UiRoot", out _) && instance != null,
                    "释放预制体缓存不会销毁克隆", result);
                ABMrg.ReleaseInstantiate(instance);
                await UniTask.WaitUntil(() => instance == null).Timeout(TimeSpan.FromSeconds(10));
                Check(true, "销毁克隆触发生命周期释放", result);
                var pool = GameObjectMrg.Instance;
                var pooled = await pool.Dequeue("UiRoot", RoleType.Other, false);
                Check(pooled != null && !pooled.activeSelf && pooled.GetComponent<YooInstanceLease>() != null,
                    "网络对象池在禁用父节点中实例化并保留资源", result);
                pool.Enqueue(pooled);
                var reused = await pool.Dequeue("UiRoot", RoleType.Other, false);
                Check(reused == pooled, "网络对象回收后可复用", result);
                pool.Enqueue(reused);
                UnityEngine.Object.Destroy(pool.gameObject);
                await UniTask.Yield();
                Check(Directory.GetDirectories("Assets").All(p => Path.GetFileName(p) == "FrameWork"),
                    "运行后 Assets 仍只有 FrameWork", result);
                result.success = true;
            }
            catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
            finally
            {
                SessionState.SetString(Report, JsonUtility.ToJson(result, true));
                EditorApplication.ExitPlaymode();
            }
        }

        private static void Check(bool condition, string name, IntegrationCheckResult result)
        {
            if (!condition) throw new InvalidOperationException("验证失败：" + name);
            result.passed.Add(name);
            Debug.Log("PASS: " + name);
        }
    }

}
