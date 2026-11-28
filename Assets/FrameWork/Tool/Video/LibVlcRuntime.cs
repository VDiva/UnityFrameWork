#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
using System;
using System.IO;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using UnityEngine;

namespace FrameWork.Video
{
    /// <summary>
    /// LibVLC 全局原生运行时管理（Windows / Windows Editor / Android）：
    /// - Windows：自动定位 Native~/win-x64（编辑器）或 &lt;Build&gt;_Data/LibVLC/win-x64（打包后）并设置 VLC_PLUGIN_PATH；
    /// - Android：直接由 APK 内打包的 libvlc-all AAR 加载 libvlc.so。
    /// </summary>
    public static class LibVlcRuntime
    {
        private static readonly object Gate = new object();
        private static LibVLC _sharedInstance;
        private static bool _coreInitialized;
        private static bool _hookedQuit;
        private static string _cachedNativeDir;
        private static Task _prewarmTask;

        public static bool IsSupported => true;
        public static bool IsReady => _sharedInstance != null;

        public static string ResolveNativeDirectory()
        {
            if (!string.IsNullOrEmpty(_cachedNativeDir))
                return _cachedNativeDir;

#if UNITY_ANDROID && !UNITY_EDITOR
            _cachedNativeDir = string.Empty;
            return _cachedNativeDir;
#elif UNITY_EDITOR
            string editorDir = Path.GetFullPath("Assets/FrameWork/Plugins/LibVLC/Native~/win-x64");
            _cachedNativeDir = Directory.Exists(editorDir)
                ? editorDir
                : Path.Combine(Application.dataPath, "LibVLC", "win-x64");
            return _cachedNativeDir;
#else
            string buildDir = Path.Combine(Application.dataPath, "LibVLC", "win-x64");
            _cachedNativeDir = Directory.Exists(buildDir)
                ? buildDir
                : Path.Combine(Application.streamingAssetsPath, "LibVLC", "win-x64");
            return _cachedNativeDir;
#endif
        }

        /// <summary>
        /// 在后台线程异步预热 LibVLC（建议在启动/登录界面调用），彻底消除首次播放视频时的主线程卡顿。
        /// </summary>
        public static Task PrewarmAsync()
        {
            if (_sharedInstance != null)
                return Task.CompletedTask;

            // 先在主线程解析路径并注册退出回调，避免后台线程访问 Unity 主线程 API。
            ResolveNativeDirectory();
            EnsureQuitHook();

            lock (Gate)
            {
                if (_sharedInstance != null)
                    return Task.CompletedTask;
                if (_prewarmTask == null || _prewarmTask.IsCompleted)
                    _prewarmTask = Task.Run(() => GetOrCreate());
                return _prewarmTask;
            }
        }

        /// <summary>触发后台预热（Fire-and-forget）。</summary>
        public static void PrewarmInBackground() => _ = PrewarmAsync();

        private static void EnsureQuitHook()
        {
            if (_hookedQuit) return;
            _hookedQuit = true;
            Application.quitting += DisposeShared;
        }

        public static LibVLC GetOrCreate()
        {
            lock (Gate)
            {
                if (_sharedInstance != null)
                    return _sharedInstance;

#if UNITY_ANDROID && !UNITY_EDITOR
                if (!_coreInitialized)
                {
                    Core.Initialize();
                    _coreInitialized = true;
                }
                Debug.Log("[LibVLC] Android 原生运行时初始化成功。");
#else
                string nativeDir = ResolveNativeDirectory();
                string dllPath = Path.Combine(nativeDir, "libvlc.dll");
                if (!File.Exists(dllPath))
                {
                    Debug.LogError($"[LibVLC] 未找到原生库：{dllPath}");
                    return null;
                }

                string pluginsDir = Path.Combine(nativeDir, "plugins");
                if (Directory.Exists(pluginsDir))
                    Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginsDir);

                if (!_coreInitialized)
                {
                    Core.Initialize(nativeDir);
                    _coreInitialized = true;
                }
                Debug.Log($"[LibVLC] Windows 初始化成功：{nativeDir}");
#endif

                _sharedInstance = new LibVLC(
                    "--no-osd",
                    "--no-video-title-show",
                    "--no-stats",
                    "--quiet");

                if (!_hookedQuit)
                {
                    _hookedQuit = true;
                    Application.quitting += DisposeShared;
                }

                return _sharedInstance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            DisposeShared();
            _hookedQuit = false;
            _cachedNativeDir = null;
            _prewarmTask = null;
        }

        public static void DisposeShared()
        {
            lock (Gate)
            {
                if (_sharedInstance == null) return;
                try { _sharedInstance.Dispose(); }
                catch (Exception ex) { Debug.LogWarning($"[LibVLC] 释放异常：{ex.Message}"); }
                _sharedInstance = null;
            }
        }
    }
}
#endif
