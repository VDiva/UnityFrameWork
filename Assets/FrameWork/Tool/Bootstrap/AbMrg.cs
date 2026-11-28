using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

namespace FrameWork
{
    /// <summary>保留框架原调用入口，资源后端使用 YooAsset。</summary>
    public static class ABMrg
    {
        internal sealed class ResourceOperationException : InvalidOperationException
        {
            public ResourceOperationException(string message) : base(message) { }
        }
        /// <summary>WebGL 资源加载与下载的并发上限，在初始化资源系统时生效。</summary>
        public static int WebGLMaxConcurrentRequests = 4;

        public static Func<string, UniTask> SceneLoading;
        public static Action BeforeSceneLoading;
        public static Action<string> SceneLoaded;
        public static Action<string> SceneLoadingFinished;
        /// <summary>可接入微信等定制文件系统；必须在 Init 前设置。</summary>
        public static Func<YooResourceSettings, InitializePackageOptions> InitializeOptionsFactory;

        /// <summary>切换场景释放旧场景句柄后是否自动卸载未被引用的 YooAsset 资源包。</summary>
        public static bool AutoUnloadUnusedOnSceneLoaded = true;

        private static readonly Dictionary<(string, Type), AssetHandle> Assets = new();
        private static readonly Dictionary<AssetHandle, (string, Type)> HandleKeys = new();
        private static readonly Dictionary<(string, Type), int> RefCounts = new();
        private static readonly HashSet<(string, Type)> PendingReleases = new();
        private static UniTaskCompletionSource _initialization;
        private static ResourcePackage _package;
        private static SceneHandle _scene;
        private static int _sceneLoadVersion;
        private static YooResourceSettings _cachedResourceSettings;
        private static FrameworkBootSettings _cachedBootSettings;
        public static bool IsInitialized { get; private set; }
        public static ResourcePackage Package => IsInitialized ? _package :
            throw new InvalidOperationException("请先 await ABMrg.Init()。");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Assets.Clear();
            HandleKeys.Clear();
            RefCounts.Clear();
            PendingReleases.Clear();
            AutoUnloadUnusedOnSceneLoaded = true;
            _initialization = null;
            _package = null;
            _scene = null;
            _sceneLoadVersion = 0;
            _cachedResourceSettings = null;
            _cachedBootSettings = null;
            IsInitialized = false;
            InitializeOptionsFactory = null;
            SceneLoading = null;
            BeforeSceneLoading = null;
            SceneLoaded = null;
            SceneLoadingFinished = null;
        }

        private static YooResourceSettings GetResourceSettings()
        {
            if (_cachedResourceSettings == null)
                _cachedResourceSettings = Resources.Load<YooResourceSettings>("YooResourceSettings");
            return _cachedResourceSettings;
        }

        private static FrameworkBootSettings GetBootSettings()
        {
            if (_cachedBootSettings == null)
                _cachedBootSettings = Resources.Load<FrameworkBootSettings>("FrameworkBootSettings");
            return _cachedBootSettings;
        }

        private static void RegisterHandle((string, Type) key, AssetHandle handle)
        {
            Assets[key] = handle;
            HandleKeys[handle] = key;
        }

        private static void UnregisterHandle((string, Type) key, AssetHandle handle)
        {
            Assets.Remove(key);
            RefCounts.Remove(key);
            PendingReleases.Remove(key);
            if (handle != null) HandleKeys.Remove(handle);
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
                var settings = GetResourceSettings();
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
                        await WaitOperation(destroy);
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
            // Only the shared queue limits WeChat; other WebGL runtimes keep their defaults.
            WebGLRequestQueue.MaxConcurrency = Mathf.Clamp(WebGLMaxConcurrentRequests, 1, 4);
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
            var boot = GetBootSettings();
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

        private static UniTask WaitOperation(AsyncOperationBase operation, CancellationToken cancellationToken = default)
        {
            if (operation == null || operation.IsDone)
                return UniTask.CompletedTask;

            cancellationToken.ThrowIfCancellationRequested();
            var source = AutoResetUniTaskCompletionSource.Create();
            CancellationTokenRegistration registration = default;
            Action<AsyncOperationBase> onCompleted = null;
            onCompleted = op =>
            {
                op.Completed -= onCompleted;
                registration.Dispose();
                source.TrySetResult();
            };
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    operation.Completed -= onCompleted;
                    source.TrySetCanceled(cancellationToken);
                });
            }
            operation.Completed += onCompleted;
            return source.Task;
        }

        private static UniTask WaitAssetHandle(AssetHandle handle, CancellationToken cancellationToken = default)
        {
            if (handle == null || !handle.IsValid || handle.IsDone)
                return UniTask.CompletedTask;

            cancellationToken.ThrowIfCancellationRequested();
            var source = AutoResetUniTaskCompletionSource.Create();
            CancellationTokenRegistration registration = default;
            Action<AssetHandle> onCompleted = null;
            onCompleted = h =>
            {
                if (h != null && h.IsValid)
                    h.Completed -= onCompleted;
                registration.Dispose();
                source.TrySetResult();
            };
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    if (handle.IsValid)
                        handle.Completed -= onCompleted;
                    source.TrySetCanceled(cancellationToken);
                });
            }
            handle.Completed += onCompleted;
            return source.Task;
        }

        private static async UniTask Wait(AsyncOperationBase operation, CancellationToken cancellationToken = default)
        {
            if (!operation.IsDone)
                await WaitOperation(operation, cancellationToken);
            if (operation.Status != EOperationStatus.Succeeded)
                throw new ResourceOperationException(operation.Error);
        }

        private static bool TryGetCachedHandle((string, Type) key, out AssetHandle handle)
        {
            if (!Assets.TryGetValue(key, out handle)) return false;
            if (handle.IsValid) return true;
            UnregisterHandle(key, handle);
            handle = null;
            return false;
        }

        /// <summary>获取指定缓存资源的当前引用计数（用于诊断与内存审计）。</summary>
        public static int GetRefCount<T>(string name) =>
            RefCounts.TryGetValue((name, typeof(T)), out int count) ? count : 0;

        public static bool TryGetLoaded<T>(string name, out T asset)
        {
            if (TryGetCachedHandle((name, typeof(T)), out var handle) &&
                handle.IsDone && handle.Status == EOperationStatus.Succeeded && handle.AssetObject is T result)
            {
                asset = result;
                return true;
            }
            asset = default;
            return false;
        }

        public static UniTask<T> LoadAsync<T>(string name, bool isRelease = true) =>
            LoadAsync<T>(name, isRelease, default);

        public static async UniTask<T> LoadAsync<T>(string name, bool isRelease, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsInitialized)
                await Init();
            cancellationToken.ThrowIfCancellationRequested();

            var key = (name, typeof(T));
            if (!TryGetCachedHandle(key, out var handle))
            {
                handle = Package.LoadAssetAsync(name, typeof(T));
                RegisterHandle(key, handle);
            }

            PendingReleases.Remove(key);
            RefCounts[key] = RefCounts.TryGetValue(key, out int refCount) ? refCount + 1 : 1;

            try
            {
                if (handle.IsValid && !handle.IsDone)
                    await WaitAssetHandle(handle, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                DecrementRefOnAbort(key, handle);
                throw;
            }

            if (PendingReleases.Remove(key) && (!RefCounts.TryGetValue(key, out int remaining) || remaining <= 0))
            {
                if (Assets.TryGetValue(key, out var current) && ReferenceEquals(current, handle))
                    UnregisterHandle(key, handle);
                if (handle.IsValid) handle.Release();
                throw new OperationCanceledException("资源加载完成前已被释放：" + name);
            }

            if (!handle.IsValid)
            {
                DecrementRefOnAbort(key, handle);
                throw new InvalidOperationException("资源加载失败或句柄已释放：" + name);
            }
            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                if (Assets.TryGetValue(key, out var current) && ReferenceEquals(current, handle))
                    UnregisterHandle(key, handle);
                if (handle.IsValid) handle.Release();
                throw new InvalidOperationException($"资源加载失败：{name}，{error}");
            }
            return (T)(object)handle.AssetObject;
        }

        private static void DecrementRefOnAbort((string, Type) key, AssetHandle handle)
        {
            if (RefCounts.TryGetValue(key, out int count))
            {
                count--;
                if (count > 0)
                {
                    RefCounts[key] = count;
                    return;
                }
                RefCounts.Remove(key);
            }
            if (handle != null && handle.IsValid && handle.IsDone)
            {
                if (Assets.TryGetValue(key, out var current) && ReferenceEquals(current, handle))
                    UnregisterHandle(key, handle);
                handle.Release();
            }
            else if (Assets.TryGetValue(key, out var current) && ReferenceEquals(current, handle))
            {
                PendingReleases.Add(key);
            }
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
            var key = (name, typeof(T));
            if (Assets.ContainsKey(key))
                throw new InvalidOperationException("该资源正在异步加载，请等待 LoadAsync 完成：" + name);
            var handle = Package.LoadAssetSync(name, typeof(T));
            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error = handle.Error;
                handle.Release();
                throw new InvalidOperationException($"资源加载失败：{name}，{error}");
            }
            RegisterHandle(key, handle);
            RefCounts[key] = 1;
            return (T)(object)handle.AssetObject;
#endif
        }

        public static UniTask PreloadAsync<T>(IEnumerable<string> names) =>
            PreloadAsync<T>(names, default);

        public static async UniTask PreloadAsync<T>(IEnumerable<string> names, CancellationToken cancellationToken)
        {
            if (names == null) return;
            cancellationToken.ThrowIfCancellationRequested();
            await Init();
            cancellationToken.ThrowIfCancellationRequested();
            int maxConcurrency = Math.Max(1,
#if UNITY_WEBGL && !UNITY_EDITOR
                Mathf.Clamp(WebGLMaxConcurrentRequests, 1, 4)
#else
                8
#endif
            );

            var batch = new List<UniTask>(maxConcurrency);
            foreach (string name in names)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(name)) continue;
                batch.Add(LoadAsync<T>(name, true, cancellationToken));
                if (batch.Count >= maxConcurrency)
                {
                    await UniTask.WhenAll(batch);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
                await UniTask.WhenAll(batch);
        }

        /// <summary>递减资源引用计数，归零时释放底层句柄；若资源仍在加载中则标记为加载完成后自动释放。</summary>
        public static bool ReleaseAsset<T>(string name) => ReleaseAsset<T>(name, false);

        /// <summary>释放缓存资源句柄；force=true 时忽略引用计数直接释放。</summary>
        public static bool ReleaseAsset<T>(string name, bool force)
        {
            var key = (name, typeof(T));
            if (!TryGetCachedHandle(key, out var handle)) return false;

            if (!force && RefCounts.TryGetValue(key, out int count) && count > 1)
            {
                RefCounts[key] = count - 1;
                return false;
            }

            RefCounts.Remove(key);
            if (!handle.IsDone)
            {
                PendingReleases.Add(key);
                return true;
            }

            UnregisterHandle(key, handle);
            handle.Release();
            return true;
        }

        public static void Release(AssetHandle handle)
        {
            if (handle == null) return;
            if (HandleKeys.TryGetValue(handle, out var key))
                UnregisterHandle(key, handle);
            if (handle.IsValid) handle.Release();
        }

        /// <summary>卸载 YooAsset 中引用计数已归零的资源包，可选同步触发 Unity 未引用资源卸载与 GC。</summary>
        public static async UniTask UnloadUnusedAssetsAsync(bool unloadUnityResources = false, bool forceGC = false,
            CancellationToken cancellationToken = default)
        {
            if (!IsInitialized || _package == null) return;
            cancellationToken.ThrowIfCancellationRequested();
            var op = _package.UnloadUnusedAssetsAsync();
            await WaitOperation(op, cancellationToken);
            if (unloadUnityResources)
            {
                var unityOp = Resources.UnloadUnusedAssets();
                if (unityOp != null)
                    await unityOp.ToUniTask(cancellationToken: cancellationToken);
            }
            if (forceGC)
                GC.Collect();
        }

        public static UniTask<GameObject> InstantiateAsync(string name, Transform parent = null) =>
            InstantiateAsync(name, parent, default);

        public static async UniTask<GameObject> InstantiateAsync(string name, Transform parent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsInitialized)
                await Init();
            cancellationToken.ThrowIfCancellationRequested();
            var handle = Package.LoadAssetAsync<GameObject>(name);
            try
            {
                if (handle.IsValid && !handle.IsDone)
                    await WaitAssetHandle(handle, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!handle.IsValid) throw new InvalidOperationException("预制体句柄已失效：" + name);
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

        public static UniTask DownloadAllAsync(Action<float> progress = null)
            => DownloadResourcesAsync(null, progress, default);

        public static UniTask DownloadAllAsync(Action<float> progress, CancellationToken cancellationToken)
            => DownloadResourcesAsync(null, progress, cancellationToken);

        /// <summary>按标签预下载，例如 startup、Level1；已有资源会复用。</summary>
        public static UniTask DownloadByTagAsync(string tag, Action<float> progress = null)
            => DownloadByTagAsync(tag, progress, default);

        public static async UniTask DownloadByTagAsync(string tag, Action<float> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("下载标签不能为空。", nameof(tag));
            await DownloadResourcesAsync(new[] { tag }, progress, cancellationToken);
        }

        private static async UniTask DownloadResourcesAsync(string[] tags, Action<float> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Init();
            cancellationToken.ThrowIfCancellationRequested();
            var settings = GetResourceSettings();
            var download = Package.CreateResourceDownloader(new ResourceDownloaderOptions(
                tags, settings.downloadConcurrency, settings.downloadRetryCount));
            download.StartDownload();
            while (!download.IsDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    download.CancelDownload();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                progress?.Invoke(download.TotalDownloadBytes == 0 ? 1f :
                    (float)download.CurrentDownloadBytes / download.TotalDownloadBytes);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            if (download.Status != EOperationStatus.Succeeded) throw new ResourceOperationException(download.Error);
            progress?.Invoke(1f);
        }

        public static void LoadSceneAsync(string name, Action<float> progress = null,
            Action call = null, Action<Exception> failure = null)
        {
            int version = ++_sceneLoadVersion;
            LoadSceneWithCallbacks(name, version, progress, call, failure).Forget(Debug.LogException);
        }
        public static void LoadScene(string name) => LoadSceneAsync(name);

        private static async UniTask LoadSceneWithCallbacks(string name, int version, Action<float> progress,
            Action call, Action<Exception> failure)
        {
            SceneHandle loading = null;
            bool loaded = false;
            try
            {
                await Init();
                if (version != _sceneLoadVersion) return;
                BeforeSceneLoading?.Invoke();
                if (SceneLoading != null) await SceneLoading(name);
                if (version != _sceneLoadVersion) return;
                loading = Package.LoadSceneAsync(name);
                while (!loading.IsDone)
                {
                    if (version == _sceneLoadVersion)
                        ReportProgress(progress, loading.Progress);
                    await UniTask.Yield();
                }
                if (version != _sceneLoadVersion)
                {
                    if (loading.IsValid) loading.Release();
                    return;
                }
                if (loading.Status != EOperationStatus.Succeeded)
                    throw new InvalidOperationException($"场景加载失败：{name}，{loading.Error}");
                if (_scene != null && _scene.IsValid) _scene.Release();
                _scene = loading;
                loaded = true;
                if (AutoUnloadUnusedOnSceneLoaded)
                    UnloadUnusedAssetsAsync().Forget(Debug.LogException);
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
                if (version == _sceneLoadVersion)
                {
                    try { SceneLoadingFinished?.Invoke(name); }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
        }

        private static void ReportProgress(Action<float> progress, float value)
        {
            try { progress?.Invoke(value); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
