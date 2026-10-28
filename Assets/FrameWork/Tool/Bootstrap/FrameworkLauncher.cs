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

        public UniTask LaunchAsync(bool forceLoadDllInEditor = false)
        {
            if (_launch != null) return _launch.Task;
            var completion = _launch = new UniTaskCompletionSource();
            LaunchCore(completion, forceLoadDllInEditor).Forget();
            return completion.Task;
        }

        private async UniTask LaunchCore(UniTaskCompletionSource completion, bool forceLoadDll)
        {
            try
            {
                var settings = Resources.Load<FrameworkBootSettings>("FrameworkBootSettings");
                if (settings == null) throw new InvalidOperationException("缺少 FrameworkBootSettings，请执行初始化菜单。");
                ProgressChanged?.Invoke(0, "初始化资源与版本");
                await ABMrg.Init();
                if (settings.downloadBeforeStart)
                    await ABMrg.DownloadByTagAsync("startup", p => ProgressChanged?.Invoke(p * 0.7f, "下载基础资源更新"));
                ProgressChanged?.Invoke(0.7f, "补充 AOT 元数据");
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
                ProgressChanged?.Invoke(0.8f, "加载热更新程序集");
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
                ProgressChanged?.Invoke(0.95f, "启动游戏");
                object result = method.Invoke(null, null);
                if (result is UniTask task) await task;
                else if (result is System.Threading.Tasks.Task standardTask) await standardTask;
                else if (method.ReturnType != typeof(void))
                    throw new InvalidOperationException("热更新入口必须返回 void、UniTask 或 Task。");
                HotUpdateStarted = true;
                ProgressChanged?.Invoke(1f, "启动完成");
                completion.TrySetResult();
            }
            catch (Exception error)
            {
                var actual = error is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException : error;
                _launch = null;
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
