using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HybridCLR;
using UnityEngine;
using UnityEngine.Scripting;

namespace FrameWork
{
    /// <summary>启动场景唯一入口：资源初始化 → 更新下载 → AOT 元数据 → DLL → 游戏。</summary>
    [DefaultExecutionOrder(-10000)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "FrameWork.Runtime", null)]
    public sealed class FrameworkLauncher : MonoBehaviour
    {
        private static FrameworkLauncher _instance;
        private UniTaskCompletionSource _launch;
        private float _progress;
        private string _stage;
        private bool _failed;
        private bool _canRetry;
        private bool _showPanel;
        public static bool HotUpdateStarted { get; private set; }
        public static event Action<float, string> ProgressChanged;
        public static event Action<Exception> LaunchFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            HotUpdateStarted = false;
            ProgressChanged = null;
            LaunchFailed = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_instance == this) LaunchAsync().Forget(Debug.LogException);
        }

        private void OnGUI()
        {
            if (!_showPanel) return;

            var screen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.Box(screen, GUIContent.none);
            float width = Mathf.Min(420, screen.width * 0.9f);
            GUILayout.BeginArea(new Rect((screen.width - width) / 2, screen.height * 0.32f, width, 200), GUI.skin.box);
            GUILayout.Label("Game startup", GUI.skin.label);
            GUILayout.Space(8);
            GUILayout.Label(_stage);
            GUILayout.Box($"{Mathf.RoundToInt(_progress * 100)}%");
            if (_failed)
            {
                GUILayout.Label(_canRetry ? "Check your connection and try again." : "Please restart the game.");
                if (_canRetry && GUILayout.Button("Retry")) LaunchAsync().Forget(Debug.LogException);
            }
            GUILayout.EndArea();
        }

        private void ReportProgress(float progress, string panelStage, string eventStage)
        {
            _progress = Mathf.Clamp01(progress);
            _stage = panelStage;
            try { ProgressChanged?.Invoke(_progress, eventStage); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public UniTask LaunchAsync(bool forceLoadDllInEditor = false)
        {
            if (_launch != null) return _launch.Task;
            var completion = _launch = new UniTaskCompletionSource();
            _showPanel = true;
            _failed = false;
            _canRetry = false;
            LaunchCore(completion, forceLoadDllInEditor).Forget();
            return completion.Task;
        }

        private async UniTask LaunchCore(UniTaskCompletionSource completion, bool forceLoadDll)
        {
            try
            {
                var settings = Resources.Load<FrameworkBootSettings>("FrameworkBootSettings");
                if (settings == null) throw new InvalidOperationException("缺少 FrameworkBootSettings，请执行初始化菜单。");
                ReportProgress(0, "Checking resource version", "初始化资源与版本");
                await ABMrg.Init();
                if (settings.downloadBeforeStart)
                    await ABMrg.DownloadByTagAsync("startup", p => ReportProgress(p * 0.7f, "Downloading resources", "下载基础资源更新"));
                ReportProgress(0.7f, "Preparing game runtime", "补充 AOT 元数据");
#if ENABLE_IL2CPP && !UNITY_EDITOR
                foreach (string dll in settings.aotMetadataDlls)
                {
                    var asset = await ABMrg.LoadAsync<TextAsset>(dll);
                    try
                    {
                        var code = RuntimeApi.LoadMetadataForAOTAssembly(asset.bytes, HomologousImageMode.SuperSet);
                        if (code != LoadImageErrorCode.OK && code != LoadImageErrorCode.HOMOLOGOUS_ASSEMBLY_HAS_LOADED)
                            throw new InvalidOperationException($"AOT 元数据加载失败：{dll} / {code}");
                    }
                    finally { ABMrg.ReleaseAsset<TextAsset>(dll); }
                }
#endif
                ReportProgress(0.8f, "Loading game code", "加载热更新程序集");
                Assembly entryAssembly = null;
                var loadedAssemblies = new List<Assembly>();
                foreach (string dll in settings.hotUpdateDlls)
                {
                    Assembly assembly = null;
#if UNITY_EDITOR
                    if (settings.useEditorAssemblies && !forceLoadDll)
                        assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
                            a => new AssemblyName(a.FullName).Name == System.IO.Path.GetFileNameWithoutExtension(dll));
#endif
                    if (assembly == null)
                    {
                        var asset = await ABMrg.LoadAsync<TextAsset>(dll);
                        try { assembly = Assembly.Load(asset.bytes); }
                        finally { ABMrg.ReleaseAsset<TextAsset>(dll); }
                    }
                    if (new AssemblyName(assembly.FullName).Name == settings.entryAssembly) entryAssembly = assembly;
                    loadedAssemblies.Add(assembly);
                }
#if !UNITY_EDITOR
                // 动态加载 DLL 后 Unity 不会自动执行这些启动回调，需在业务入口前补齐。
                InitializeHotUpdateAssemblies(loadedAssemblies);
#endif
                var type = entryAssembly?.GetType(settings.entryType, true);
                var method = type?.GetMethod(settings.entryMethod, BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                if (method == null) throw new MissingMethodException($"找不到热更新入口 {settings.entryType}.{settings.entryMethod}()");
                ReportProgress(0.95f, "Starting game", "启动游戏");
                object result = method.Invoke(null, null);
                if (result is UniTask task) await task;
                else if (result is System.Threading.Tasks.Task standardTask) await standardTask;
                else if (method.ReturnType != typeof(void))
                    throw new InvalidOperationException("热更新入口必须返回 void、UniTask 或 Task。");
                HotUpdateStarted = true;
                _showPanel = false;
                ReportProgress(1f, "Ready", "启动完成");
                completion.TrySetResult();
            }
            catch (Exception error)
            {
                var actual = error is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException : error;
                _launch = null;
                _failed = true;
                _canRetry = _progress < 0.7f && actual is ABMrg.ResourceOperationException;
                _stage = _canRetry ? "Could not check or download resources." : "Game startup failed.";
                try { LaunchFailed?.Invoke(actual); } catch (Exception callbackError) { Debug.LogException(callbackError); }
                completion.TrySetException(actual);
            }
        }

        [Preserve]
        public static void MarkHotUpdateStarted() => HotUpdateStarted = true;

        private static void InitializeHotUpdateAssemblies(IEnumerable<Assembly> assemblies)
        {
            var callbacks = assemblies.SelectMany(a => a.GetTypes()).SelectMany(t =>
                t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Select(method => new { Method = method, Attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() })
                .Where(callback => callback.Attribute != null).ToArray();
            var phases = new[] { RuntimeInitializeLoadType.SubsystemRegistration, RuntimeInitializeLoadType.AfterAssembliesLoaded,
                RuntimeInitializeLoadType.BeforeSplashScreen, RuntimeInitializeLoadType.BeforeSceneLoad, RuntimeInitializeLoadType.AfterSceneLoad };
            foreach (var phase in phases)
                foreach (var callback in callbacks.Where(c => c.Attribute.loadType == phase))
                {
                    if (callback.Method.GetParameters().Length != 0 || callback.Method.ReturnType != typeof(void) || callback.Method.ContainsGenericParameters)
                        throw new InvalidOperationException("热更新启动回调必须是无参数的静态 void 方法：" + callback.Method);
                    callback.Method.Invoke(null, null);
                }
        }
    }
}
