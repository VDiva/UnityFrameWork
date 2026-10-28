using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

namespace FrameWork
{
    /// <summary>保留框架原调用入口，资源后端使用 YooAsset。</summary>
    public static class ABMrg
    {
        public static Func<string, UniTask> SceneLoading;
        public static Action BeforeSceneLoading;
        public static Action<string> SceneLoaded;
        public static Action<string> SceneLoadingFinished;
        /// <summary>可接入微信等定制文件系统；必须在 Init 前设置。</summary>
        public static Func<YooResourceSettings, InitializePackageOptions> InitializeOptionsFactory;

        private static readonly Dictionary<(string, Type), AssetHandle> Assets = new();
        private static UniTaskCompletionSource _initialization;
        private static ResourcePackage _package;
        private static SceneHandle _scene;
        public static bool IsInitialized { get; private set; }
        public static ResourcePackage Package => IsInitialized ? _package :
            throw new InvalidOperationException("请先 await ABMrg.Init()。");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Assets.Clear();
            _initialization = null;
            _package = null;
            _scene = null;
            IsInitialized = false;
            InitializeOptionsFactory = null;
            SceneLoading = null;
            BeforeSceneLoading = null;
            SceneLoaded = null;
            SceneLoadingFinished = null;
        }

        public static UniTask Init()
        {
            if (IsInitialized) return UniTask.CompletedTask;
            if (_initialization != null) return _initialization.Task;
            var completion = _initialization = new UniTaskCompletionSource();
            InitializeCore(completion).Forget();
            return completion.Task;
        }

        private static async UniTask InitializeCore(UniTaskCompletionSource completion)
        {
            try
            {
                var settings = Resources.Load<YooResourceSettings>("YooResourceSettings");
                if (settings == null || string.IsNullOrWhiteSpace(settings.packageName))
                    throw new InvalidOperationException("缺少 YooResourceSettings，请执行 FrameWork/初始化抽离框架。");
                if (!YooAssets.IsInitialized) YooAssets.Initialize();
                if (!YooAssets.TryGetPackage(settings.packageName, out _package))
                    _package = YooAssets.CreatePackage(settings.packageName);
                var options = InitializeOptionsFactory != null
                    ? InitializeOptionsFactory(settings) : CreateOptions(settings);
                var initialize = _package.InitializePackageAsync(options);
                await Wait(initialize);
                string version = settings.packageVersion;
                if (string.IsNullOrWhiteSpace(version))
                {
                    var request = _package.RequestPackageVersionAsync(
                        new RequestPackageVersionOptions(true, settings.timeoutSeconds));
                    await Wait(request);
                    version = request.PackageVersion;
                }
                var manifest = _package.LoadPackageManifestAsync(
                    new LoadPackageManifestOptions(version, settings.timeoutSeconds));
                await Wait(manifest);
                IsInitialized = true;
                completion.TrySetResult();
                Debug.Log($"YooAsset 初始化完成：{settings.packageName} / {version}");
            }
            catch (Exception error)
            {
                if (_package != null)
                {
                    string name = _package.PackageName;
                    try
                    {
                        var destroy = _package.DestroyPackageAsync();
                        while (!destroy.IsDone) await UniTask.Yield();
                        YooAssets.RemovePackage(name);
                    }
                    catch (Exception cleanupError) { Debug.LogException(cleanupError); }
                }
                _package = null;
                _initialization = null; // 失败可重试，不把失败的 Task 永久缓存。
                completion.TrySetException(error);
            }
        }

        private static InitializePackageOptions CreateOptions(YooResourceSettings settings)
        {
#if UNITY_EDITOR
            if (settings.editorSimulate)
            {
                var build = EditorSimulateBuildInvoker.Build(settings.packageName, (int)EBundleType.VirtualAssetBundle);
                return new EditorSimulateModeOptions {
                    EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(build.PackageRootDirectory)
                };
            }
#endif
            bool host = settings.playerMode == YooPlayerMode.Host;
#if UNITY_WEBGL && !UNITY_EDITOR
            var web = new WebPlayModeOptions();
            if (host) web.WebNetworkFileSystemParameters =
                FileSystemParameters.CreateDefaultWebNetworkFileSystemParameters(CreateRemote(settings));
            else web.WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
            return web;
#else
            if (host)
                return new HostPlayModeOptions {
                    CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(CreateRemote(settings))
                };
            return new OfflinePlayModeOptions {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(
#if UNITY_EDITOR
                        System.IO.Path.GetFullPath(System.IO.Path.Combine("Library/FrameWork/YooAssetBuiltin",
                            UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(), "yoo", settings.packageName))
#endif
                    )
            };
#endif
        }

        private static IRemoteService CreateRemote(YooResourceSettings settings)
        {
            string remoteUrl = settings.ActiveRemoteUrl;
            string fallbackRemoteUrl = settings.ActiveFallbackRemoteUrl;
            if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var primary) ||
                (primary.Scheme != "http" && primary.Scheme != "https"))
                throw new InvalidOperationException($"Host 模式需要配置 {YooResourceSettings.EnvironmentName}环境资源地址（YooResourceSettings），必须是完整 HTTP(S) URL。");
            if (!string.IsNullOrWhiteSpace(fallbackRemoteUrl) &&
                (!Uri.TryCreate(fallbackRemoteUrl, UriKind.Absolute, out var fallback) ||
                 (fallback.Scheme != "http" && fallback.Scheme != "https")))
                throw new InvalidOperationException($"{YooResourceSettings.EnvironmentName}环境备用资源地址必须是 HTTP(S) URL。");
            return new RemoteService(ExpandRemote(remoteUrl, settings.packageName),
                ExpandRemote(fallbackRemoteUrl, settings.packageName));
        }

        private static string ExpandRemote(string url, string package)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            var boot = Resources.Load<FrameworkBootSettings>("FrameworkBootSettings");
            string platform;
#if UNITY_EDITOR
            platform = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
#else
            switch (Application.platform)
            {
                case RuntimePlatform.Android: platform = "Android"; break;
                case RuntimePlatform.IPhonePlayer: platform = "iOS"; break;
                case RuntimePlatform.WebGLPlayer: platform = "WebGL"; break;
                case RuntimePlatform.OSXPlayer: platform = "StandaloneOSX"; break;
                case RuntimePlatform.LinuxPlayer: platform = "StandaloneLinux64"; break;
                default: platform = "StandaloneWindows64"; break;
            }
#endif
            return url.Replace("{platform}", platform).Replace("{package}", package)
                .Replace("{client}", boot != null ? boot.clientBuildId : "");
        }

        private sealed class RemoteService : IRemoteService
        {
            private readonly string _primary, _fallback;
            public RemoteService(string primary, string fallback)
            {
                _primary = primary.TrimEnd('/');
                _fallback = string.IsNullOrWhiteSpace(fallback) ? _primary : fallback.TrimEnd('/');
            }
            public IReadOnlyList<string> GetRemoteUrls(string fileName) =>
                _primary == _fallback ? new[] { _primary + "/" + fileName } :
                    new[] { _primary + "/" + fileName, _fallback + "/" + fileName };
        }

        private static async UniTask Wait(AsyncOperationBase operation)
        {
            while (!operation.IsDone) await UniTask.Yield(PlayerLoopTiming.Update);
            if (operation.Status != EOperationStatus.Succeeded)
                throw new InvalidOperationException(operation.Error);
        }

        public static bool TryGetLoaded<T>(string name, out T asset)
        {
            if (Assets.TryGetValue((name, typeof(T)), out var handle) && handle.IsValid &&
                handle.IsDone && handle.Status == EOperationStatus.Succeeded && handle.AssetObject is T result)
            {
                asset = result;
                return true;
            }
            asset = default;
            return false;
        }

        public static async UniTask<T> LoadAsync<T>(string name, bool isRelease = true)
        {
            await Init();
            var key = (name, typeof(T));
            if (!Assets.TryGetValue(key, out var handle))
            {
                handle = Package.LoadAssetAsync(name, typeof(T));
                Assets.Add(key, handle);
            }
            while (handle.IsValid && !handle.IsDone) await UniTask.Yield(PlayerLoopTiming.Update);
            if (!handle.IsValid) throw new InvalidOperationException("资源加载失败或句柄已释放：" + name);
            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                if (Assets.TryGetValue(key, out var current) && ReferenceEquals(current, handle))
                    Assets.Remove(key);
                if (handle.IsValid) handle.Release();
                throw new InvalidOperationException($"资源加载失败：{name}，{error}");
            }
            return (T)(object)handle.AssetObject;
        }

        /// <summary>isRelease 保留旧签名；缓存由 ReleaseAsset 显式释放。</summary>
        public static T Load<T>(string name, bool isRelease = true, float timeoutSeconds = 5f)
        {
            if (TryGetLoaded<T>(name, out var asset)) return asset;
            if (timeoutSeconds <= 0 || float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new InvalidOperationException("WebGL 不阻塞主线程，请先 await ABMrg.LoadAsync 或 PreloadAsync：" + name);
#else
            if (Assets.ContainsKey((name, typeof(T))))
                throw new InvalidOperationException("该资源正在异步加载，请等待 LoadAsync 完成：" + name);
            var handle = Package.LoadAssetSync(name, typeof(T));
            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                handle.Release();
                throw new InvalidOperationException($"资源加载失败：{name}，{error}");
            }
            Assets.Add((name, typeof(T)), handle);
            return (T)(object)handle.AssetObject;
#endif
        }

        public static async UniTask PreloadAsync<T>(IEnumerable<string> names)
        {
            foreach (string name in names) await LoadAsync<T>(name);
        }

        public static bool ReleaseAsset<T>(string name)
        {
            var key = (name, typeof(T));
            if (!Assets.TryGetValue(key, out var handle)) return false;
            if (!handle.IsDone) throw new InvalidOperationException("资源仍在加载：" + name);
            Assets.Remove(key);
            if (handle.IsValid) handle.Release();
            return true;
        }

        public static void Release(AssetHandle handle)
        {
            if (handle == null) return;
            foreach (var entry in new List<KeyValuePair<(string, Type), AssetHandle>>(Assets))
                if (ReferenceEquals(entry.Value, handle)) Assets.Remove(entry.Key);
            if (handle.IsValid) handle.Release();
        }

        public static async UniTask<GameObject> InstantiateAsync(string name, Transform parent = null)
        {
            await Init();
            var handle = Package.LoadAssetAsync<GameObject>(name);
            try
            {
                while (!handle.IsDone) await UniTask.Yield();
                if (handle.Status != EOperationStatus.Succeeded)
                    throw new InvalidOperationException($"预制体加载失败：{name}，{handle.Error}");
                var instance = Object.Instantiate(handle.GetAssetObject<GameObject>(), parent);
                instance.AddComponent<YooInstanceLease>().Bind(handle);
                return instance;
            }
            catch { if (handle.IsValid) handle.Release(); throw; }
        }

        public static void ReleaseInstantiate(GameObject instance)
        {
            if (instance != null) Object.Destroy(instance); // OnDestroy 释放独立资源句柄。
        }

        public static async UniTask DownloadAllAsync(Action<float> progress = null)
            => await DownloadResourcesAsync(null, progress);

        /// <summary>按标签预下载，例如 startup、Level1；已有资源会复用。</summary>
        public static async UniTask DownloadByTagAsync(string tag, Action<float> progress = null)
        {
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("下载标签不能为空。", nameof(tag));
            await DownloadResourcesAsync(new[] { tag }, progress);
        }

        private static async UniTask DownloadResourcesAsync(string[] tags, Action<float> progress)
        {
            await Init();
            var settings = Resources.Load<YooResourceSettings>("YooResourceSettings");
            var download = Package.CreateResourceDownloader(new ResourceDownloaderOptions(
                tags, settings.downloadConcurrency, settings.downloadRetryCount));
            download.StartDownload();
            while (!download.IsDone)
            {
                progress?.Invoke(download.TotalDownloadBytes == 0 ? 1f :
                    (float)download.CurrentDownloadBytes / download.TotalDownloadBytes);
                await UniTask.Yield();
            }
            if (download.Status != EOperationStatus.Succeeded) throw new InvalidOperationException(download.Error);
            progress?.Invoke(1f);
        }

        public static void LoadSceneAsync(string name, Action<float> progress = null,
            Action call = null, Action<Exception> failure = null)
        {
            LoadSceneWithCallbacks(name, progress, call, failure).Forget(Debug.LogException);
        }
        public static void LoadScene(string name) => LoadSceneAsync(name);

        private static async UniTask LoadSceneWithCallbacks(string name, Action<float> progress,
            Action call, Action<Exception> failure)
        {
            SceneHandle loading = null;
            bool loaded = false;
            try
            {
                await Init();
                BeforeSceneLoading?.Invoke();
                if (SceneLoading != null) await SceneLoading(name);
                loading = Package.LoadSceneAsync(name);
                while (!loading.IsDone)
                {
                    ReportProgress(progress, loading.Progress);
                    await UniTask.Yield();
                }
                if (loading.Status != EOperationStatus.Succeeded)
                    throw new InvalidOperationException($"场景加载失败：{name}，{loading.Error}");
                if (_scene != null && _scene.IsValid) _scene.Release();
                _scene = loading;
                loaded = true;
                ReportProgress(progress, 1f);
                call?.Invoke();
                SceneLoaded?.Invoke(name);
            }
            catch (Exception error)
            {
                if (!loaded && loading != null && loading.IsValid) loading.Release();
                Debug.LogException(error);
                failure?.Invoke(error);
            }
            finally
            {
                try { SceneLoadingFinished?.Invoke(name); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        private static void ReportProgress(Action<float> progress, float value)
        {
            try { progress?.Invoke(value); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
