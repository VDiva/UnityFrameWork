using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
using LibVLCSharp.Shared;
#endif

namespace FrameWork.Video
{
    /// <summary>
    /// 跨平台统一视频播放组件：
    /// - Windows / Windows Editor：优先走官方免费 LGPL LibVLC（全格式、RTSP/RTMP/HLS、流畅 Seek、不闪黑屏）；
    /// - WebGL / 微信小游戏 / 其他平台：自动无缝回退到封装好的 Unity 内置 VideoPlayer。
    /// </summary>
    public sealed class FrameworkVideoPlayer : MonoBehaviour
    {
        [Header("显示目标")]
        [Tooltip("UI RawImage 显示目标（可选；与 Renderer 二选一或同时绑定）。")]
        [SerializeField] private RawImage targetRawImage;
        [Tooltip("3D 材质显示目标（可选）。")]
        [SerializeField] private Renderer targetRenderer;

        [Header("播放设置")]
        [SerializeField] private bool playOnAwake;
        [Tooltip("支持绝对路径、StreamingAssets 相对路径、http(s)://、rtsp://、rtmp://。")]
        [SerializeField] private string initialSource = "";
        [SerializeField] private bool loop;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool mute;
        [SerializeField, Range(0.25f, 4f)] private float playbackRate = 1f;
        [Tooltip("Windows / Android 下是否启用 LibVLC 后端（关闭则强制使用 Unity 内置 VideoPlayer）。")]
        [SerializeField] private bool preferLibVlcOnWindows = true;
        [Tooltip("本地视频起播缓冲（毫秒）；默认 300ms（过低容易导致高码率视频丢帧卡顿）。")]
        [SerializeField, Range(80, 2000)] private int fileCachingMs = 300;
        [Tooltip("网络流起播缓冲（毫秒）；默认 600ms，弱网可调大至 1000~1500ms。")]
        [SerializeField, Range(150, 5000)] private int networkCachingMs = 600;

        /// <summary>视频准备完成且首帧就绪时触发。</summary>
        public event Action OnPrepared;
        /// <summary>非循环模式下视频播放完成时触发。</summary>
        public event Action OnCompleted;
        /// <summary>循环模式下每播完一轮并重新开始时触发。</summary>
        public event Action OnLoopPointReached;
        /// <summary>视频跳转（Seek / SetTimeMs）完成时触发，参数为跳转后的当前播放时间（秒）。</summary>
        public event Action<double> OnSeekCompleted;
        /// <summary>播放进度变化时触发（主线程），参数为 (当前时间秒, 总时长秒)。</summary>
        public event Action<double, double> OnTimeChanged;
        /// <summary>网络流缓冲进度变化时触发，参数为缓冲进度 (0 ~ 1)。</summary>
        public event Action<float> OnBuffering;
        public event Action<string> OnError;

        public string BackendName => _usingLibVlc ? "LibVLC" : "UnityVideoPlayer";
        public bool IsPlaying => GetIsPlaying();
        public bool IsLooping => loop;
        public string InitialSource => initialSource;
        public string CurrentSource => _currentSource;
        public Texture CurrentTexture => GetCurrentTexture();
        public bool IsTextureFlippedY => _usingLibVlc;
        public int VideoWidth => _videoWidth;
        public int VideoHeight => _videoHeight;
        public double DurationSeconds => GetDurationSeconds();
        public double CurrentTimeSeconds => GetCurrentTimeSeconds();
        public float Progress => DurationSeconds > 0.001 ? Mathf.Clamp01((float)(CurrentTimeSeconds / DurationSeconds)) : 0f;

        private bool _usingLibVlc;
        private int _videoWidth;
        private int _videoHeight;
        private bool _preparedInvoked;
        private string _currentSource;
        private bool _pendingVlcSeekNotify;
        private double _lastReportedTime = -1;
        private bool _suppressTargetApply;

        // Unity VideoPlayer 回退后端
        private VideoPlayer _unityPlayer;
        private RenderTexture _unityRt;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
        private MediaPlayer _vlcPlayer;
        private Media _vlcMedia;
        private Texture2D _vlcTexture;

        private readonly object _frameLock = new object();
        // 三缓冲零拷贝：write 给解码线程写，ready 存放最新就绪帧，read 给主线程无锁上传 GPU。
        private byte[] _writeBuf;
        private byte[] _readyBuf;
        private byte[] _readBuf;
        private GCHandle _writePin;
        private GCHandle _readyPin;
        private GCHandle _readPin;
        private bool _buffersPinned;
        private int _pendingWidth;
        private int _pendingHeight;
        private bool _frameDirty;
        private bool _vlcEndReachedFlag;
        private string _vlcErrorFlag;
        private float _vlcBufferingFlag = -1f;

        // 保活原生回调委托，防止被 GC 回收
        private MediaPlayer.LibVLCVideoFormatCb _formatCb;
        private MediaPlayer.LibVLCVideoCleanupCb _cleanupCb;
        private MediaPlayer.LibVLCVideoLockCb _lockCb;
        private MediaPlayer.LibVLCVideoUnlockCb _unlockCb;
        private MediaPlayer.LibVLCVideoDisplayCb _displayCb;
#endif

        private void Awake()
        {
            if (targetRawImage == null)
                targetRawImage = GetComponent<RawImage>();
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (playOnAwake && !string.IsNullOrWhiteSpace(initialSource))
                Play(initialSource, loop);
        }

        private void OnDestroy()
        {
            Stop();
            ReleaseUnityBackend();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            ReleaseVlcBackend();
#endif
        }

        /// <summary>
        /// 播放视频：支持本地绝对路径、StreamingAssets 相对路径、以及 http(s) / rtsp / rtmp 等网络流。
        /// </summary>
        public void Play(string source, bool isLoop = false)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                NotifyError("视频路径或 URL 不能为空。");
                return;
            }

            loop = isLoop;
            _currentSource = ResolveSource(source);
            _preparedInvoked = false;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (preferLibVlcOnWindows && TryPlayWithLibVlc(_currentSource))
            {
                _usingLibVlc = true;
                return;
            }
#endif
            _usingLibVlc = false;
            PlayWithUnityVideoPlayer(_currentSource);
        }

        public void Pause()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc) { _vlcPlayer?.SetPause(true); return; }
#endif
            if (_unityPlayer != null && _unityPlayer.isPlaying) _unityPlayer.Pause();
        }

        public void Resume()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc) { _vlcPlayer?.SetPause(false); return; }
#endif
            if (_unityPlayer != null && _unityPlayer.isPrepared) _unityPlayer.Play();
        }

        public void Stop()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_vlcPlayer != null)
            {
                try { _vlcPlayer.Stop(); } catch { }
            }
#endif
            if (_unityPlayer != null)
                _unityPlayer.Stop();
        }

        public void Seek(float normalizedProgress)
        {
            float clamped = Mathf.Clamp01(normalizedProgress);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc && _vlcPlayer != null)
            {
                _pendingVlcSeekNotify = true;
                _vlcPlayer.Position = clamped;
                return;
            }
#endif
            if (_unityPlayer != null && _unityPlayer.length > 0.001)
                _unityPlayer.time = clamped * _unityPlayer.length;
        }

        public void SetTimeMs(long timeMs)
        {
            long clamped = Math.Max(0, timeMs);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc && _vlcPlayer != null)
            {
                _pendingVlcSeekNotify = true;
                _vlcPlayer.Time = clamped;
                return;
            }
#endif
            if (_unityPlayer != null)
                _unityPlayer.time = clamped * 0.001;
        }

        public void SetVolume(float value01)
        {
            volume = Mathf.Clamp01(value01);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_vlcPlayer != null)
                _vlcPlayer.Volume = Mathf.RoundToInt(volume * 100f);
#endif
            if (_unityPlayer != null && _unityPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
                _unityPlayer.SetDirectAudioVolume(0, mute ? 0f : volume);
        }

        public void SetMute(bool isMuted)
        {
            mute = isMuted;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_vlcPlayer != null)
                _vlcPlayer.Mute = mute;
#endif
            if (_unityPlayer != null && _unityPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
                _unityPlayer.SetDirectAudioMute(0, mute);
        }

        public void SetPlaybackRate(float rate)
        {
            playbackRate = Mathf.Clamp(rate, 0.25f, 4f);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_vlcPlayer != null)
                _vlcPlayer.SetRate(playbackRate);
#endif
            if (_unityPlayer != null && _unityPlayer.canSetPlaybackSpeed)
                _unityPlayer.playbackSpeed = playbackRate;
        }

        /// <summary>运行时动态开启或关闭循环播放。</summary>
        public void SetLoop(bool isLoop)
        {
            loop = isLoop;
            if (_unityPlayer != null)
                _unityPlayer.isLooping = loop;
        }

        private void Update()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc)
                PumpLibVlcFrame();
#endif
            if (OnTimeChanged != null && IsPlaying)
            {
                double cur = CurrentTimeSeconds;
                if (Math.Abs(cur - _lastReportedTime) >= 0.03)
                {
                    _lastReportedTime = cur;
                    OnTimeChanged.Invoke(cur, DurationSeconds);
                }
            }
        }

        private Texture GetCurrentTexture()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc) return _vlcTexture;
#endif
            return _unityRt;
        }

#if UNITY_EDITOR
        /// <summary>供自定义 Inspector 在编辑模式下仅于面板内预览播放，不修改场景物体的目标纹理。</summary>
        public void EditorPlayPreview(string source, bool isLoop)
        {
            _suppressTargetApply = true;
            Play(source, isLoop);
        }

        /// <summary>供自定义 Inspector 在非运行模式下驱动帧刷新，返回本帧是否有新画面。</summary>
        public bool EditorTick()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (_usingLibVlc)
                return PumpLibVlcFrame();
#endif
            return false;
        }

        /// <summary>供自定义 Inspector 在退出预览时安全释放临时预览资源。</summary>
        public void EditorReleasePreview()
        {
            _suppressTargetApply = false;
            Stop();
            ReleaseUnityBackend();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            ReleaseVlcBackend();
#endif
        }
#endif

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private bool GetIsPlaying()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc) return _vlcPlayer != null && _vlcPlayer.IsPlaying;
#endif
            return _unityPlayer != null && _unityPlayer.isPlaying;
        }

        private double GetDurationSeconds()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc) return _vlcPlayer != null && _vlcPlayer.Length > 0 ? _vlcPlayer.Length * 0.001 : 0;
#endif
            return _unityPlayer != null ? _unityPlayer.length : 0;
        }

        private double GetCurrentTimeSeconds()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
            if (_usingLibVlc) return _vlcPlayer != null && _vlcPlayer.Time > 0 ? _vlcPlayer.Time * 0.001 : 0;
#endif
            return _unityPlayer != null ? _unityPlayer.time : 0;
        }

        private void ApplyTextureToTargets(Texture tex, bool flipY)
        {
            if (_suppressTargetApply || !Application.isPlaying)
                return;

            if (targetRawImage != null)
            {
                targetRawImage.texture = tex;
                targetRawImage.uvRect = flipY ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
            }
            if (targetRenderer != null && targetRenderer.material != null)
            {
                targetRenderer.material.mainTexture = tex;
                targetRenderer.material.mainTextureScale = flipY ? new Vector2(1f, -1f) : Vector2.one;
                targetRenderer.material.mainTextureOffset = flipY ? new Vector2(0f, 1f) : Vector2.zero;
            }
        }

        private static string ResolveSource(string source)
        {
            string trimmed = source.Trim();
            if (trimmed.Contains("://") || Path.IsPathRooted(trimmed))
                return trimmed;
            return Path.Combine(Application.streamingAssetsPath, trimmed);
        }

        private void NotifyError(string message)
        {
            Debug.LogError($"[FrameworkVideoPlayer] {message}");
            OnError?.Invoke(message);
        }

        #region Unity 内置 VideoPlayer 回退实现

        private void PlayWithUnityVideoPlayer(string source)
        {
            if (_unityPlayer == null)
            {
                _unityPlayer = gameObject.GetComponent<VideoPlayer>() ?? gameObject.AddComponent<VideoPlayer>();
                _unityPlayer.playOnAwake = false;
                _unityPlayer.renderMode = VideoRenderMode.RenderTexture;
                _unityPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                _unityPlayer.prepareCompleted += HandleUnityPrepared;
                _unityPlayer.loopPointReached += HandleUnityCompleted;
                _unityPlayer.seekCompleted += HandleUnitySeekCompleted;
                _unityPlayer.errorReceived += HandleUnityError;
            }

            _unityPlayer.Stop();
            _unityPlayer.source = VideoSource.Url;
            _unityPlayer.url = source;
            _unityPlayer.isLooping = loop;
            _unityPlayer.Prepare();
        }

        private void HandleUnityPrepared(VideoPlayer vp)
        {
            int w = Mathf.Max(16, (int)vp.width);
            int h = Mathf.Max(16, (int)vp.height);
            _videoWidth = w;
            _videoHeight = h;

            if (_unityRt == null || _unityRt.width != w || _unityRt.height != h)
            {
                if (_unityRt != null) { _unityRt.Release(); SafeDestroy(_unityRt); }
                _unityRt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "FrameworkVideoRT" };
                _unityRt.Create();
            }

            vp.targetTexture = _unityRt;
            vp.SetDirectAudioMute(0, mute);
            vp.SetDirectAudioVolume(0, mute ? 0f : volume);
            if (vp.canSetPlaybackSpeed) vp.playbackSpeed = playbackRate;
            ApplyTextureToTargets(_unityRt, flipY: false);
            vp.Play();

            if (!_preparedInvoked)
            {
                _preparedInvoked = true;
                OnPrepared?.Invoke();
            }
        }

        private void HandleUnityCompleted(VideoPlayer vp)
        {
            if (loop) OnLoopPointReached?.Invoke();
            else OnCompleted?.Invoke();
        }

        private void HandleUnitySeekCompleted(VideoPlayer vp)
        {
            OnSeekCompleted?.Invoke(vp != null ? vp.time : 0);
        }

        private void HandleUnityError(VideoPlayer vp, string msg)
        {
            NotifyError($"UnityVideoPlayer 错误：{msg}");
        }

        private void ReleaseUnityBackend()
        {
            if (_unityPlayer != null)
            {
                _unityPlayer.prepareCompleted -= HandleUnityPrepared;
                _unityPlayer.loopPointReached -= HandleUnityCompleted;
                _unityPlayer.seekCompleted -= HandleUnitySeekCompleted;
                _unityPlayer.errorReceived -= HandleUnityError;
            }
            if (_unityRt != null)
            {
                _unityRt.Release();
                SafeDestroy(_unityRt);
                _unityRt = null;
            }
        }

        #endregion

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR)
        #region LibVLC Windows 后端实现

        private bool TryPlayWithLibVlc(string source)
        {
            LibVLC vlc = LibVlcRuntime.GetOrCreate();
            if (vlc == null)
                return false;

            if (_vlcPlayer == null)
            {
                _vlcPlayer = new MediaPlayer(vlc);
                _formatCb = OnVlcVideoFormat;
                _cleanupCb = OnVlcVideoCleanup;
                _lockCb = OnVlcVideoLock;
                _unlockCb = OnVlcVideoUnlock;
                _displayCb = OnVlcVideoDisplay;
                _vlcPlayer.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
                _vlcPlayer.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);
                _vlcPlayer.EndReached += (_, _) => { _vlcEndReachedFlag = true; };
                _vlcPlayer.EncounteredError += (_, _) => { _vlcErrorFlag = "LibVLC 解码或打开媒体失败。"; };
                _vlcPlayer.Buffering += (_, e) => { _vlcBufferingFlag = Mathf.Clamp01(e.Cache / 100f); };
            }

            _vlcEndReachedFlag = false;
            _vlcErrorFlag = null;
            _vlcBufferingFlag = -1f;
            _pendingVlcSeekNotify = false;
            _lastReportedTime = -1;

            _vlcMedia?.Dispose();
            bool isNetwork = source.Contains("://");
            _vlcMedia = isNetwork
                ? new Media(vlc, new Uri(source))
                : new Media(vlc, source, FromType.FromPath);
            _vlcMedia.AddOption(isNetwork
                ? $":network-caching={Mathf.Clamp(networkCachingMs, 150, 5000)}"
                : $":file-caching={Mathf.Clamp(fileCachingMs, 80, 2000)}");

            _vlcPlayer.Volume = Mathf.RoundToInt(volume * 100f);
            _vlcPlayer.Mute = mute;
            _vlcPlayer.SetRate(playbackRate);
            return _vlcPlayer.Play(_vlcMedia);
        }

        private uint OnVlcVideoFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            byte[] rgba = Encoding.ASCII.GetBytes("RGBA");
            Marshal.Copy(rgba, 0, chroma, 4);

            uint w = Math.Max(16u, width);
            uint h = Math.Max(16u, height);
            pitches = w * 4u;
            lines = h;

            int bytes = (int)(pitches * lines);
            lock (_frameLock)
            {
                UnpinBuffersLocked();
                _pendingWidth = (int)w;
                _pendingHeight = (int)h;
                _writeBuf = new byte[bytes];
                _readyBuf = new byte[bytes];
                _readBuf = new byte[bytes];
                _writePin = GCHandle.Alloc(_writeBuf, GCHandleType.Pinned);
                _readyPin = GCHandle.Alloc(_readyBuf, GCHandleType.Pinned);
                _readPin = GCHandle.Alloc(_readBuf, GCHandleType.Pinned);
                _buffersPinned = true;
                _frameDirty = false;
            }
            return 1;
        }

        private void OnVlcVideoCleanup(ref IntPtr opaque)
        {
            lock (_frameLock)
                UnpinBuffersLocked();
        }

        private IntPtr OnVlcVideoLock(IntPtr opaque, IntPtr planes)
        {
            lock (_frameLock)
            {
                if (_buffersPinned)
                    Marshal.WriteIntPtr(planes, _writePin.AddrOfPinnedObject());
            }
            return IntPtr.Zero;
        }

        private void OnVlcVideoUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
            // 三缓冲零拷贝 Swap：仅微秒级交换 write/ready，绝不阻塞主线程正在读取的 read 缓冲。
            lock (_frameLock)
            {
                if (_buffersPinned)
                {
                    (_writeBuf, _readyBuf) = (_readyBuf, _writeBuf);
                    (_writePin, _readyPin) = (_readyPin, _writePin);
                    _frameDirty = true;
                }
            }
        }

        private void OnVlcVideoDisplay(IntPtr opaque, IntPtr picture) { }

        private bool PumpLibVlcFrame()
        {
            if (_vlcErrorFlag != null)
            {
                string err = _vlcErrorFlag;
                _vlcErrorFlag = null;
                NotifyError(err);
            }

            if (_vlcBufferingFlag >= 0f)
            {
                float cache = _vlcBufferingFlag;
                _vlcBufferingFlag = -1f;
                OnBuffering?.Invoke(cache);
            }

            if (_vlcEndReachedFlag)
            {
                _vlcEndReachedFlag = false;
                if (loop && !string.IsNullOrEmpty(_currentSource))
                {
                    OnLoopPointReached?.Invoke();
                    // 回调里可能调用了 SetLoop(false) 或 Stop()，重播前再次确认
                    if (loop && !string.IsNullOrEmpty(_currentSource))
                        TryPlayWithLibVlc(_currentSource);
                }
                else
                {
                    OnCompleted?.Invoke();
                }
            }

            bool hasNewFrame = false;
            int w = 0, h = 0;
            IntPtr readPtr = IntPtr.Zero;
            int readLen = 0;
            lock (_frameLock)
            {
                if (_frameDirty && _buffersPinned && _readyBuf != null && _pendingWidth > 0 && _pendingHeight > 0)
                {
                    (_readBuf, _readyBuf) = (_readyBuf, _readBuf);
                    (_readPin, _readyPin) = (_readyPin, _readPin);
                    w = _pendingWidth;
                    h = _pendingHeight;
                    readPtr = _readPin.AddrOfPinnedObject();
                    readLen = _readBuf.Length;
                    _frameDirty = false;
                    hasNewFrame = true;
                }
            }

            if (hasNewFrame && readPtr != IntPtr.Zero && readLen > 0)
            {
                if (_vlcTexture == null || _vlcTexture.width != w || _vlcTexture.height != h)
                {
                    if (_vlcTexture != null) SafeDestroy(_vlcTexture);
                    // 统一使用 RGBA32，Windows 与 Android 色彩通道完全一致。
                    _vlcTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    _videoWidth = w;
                    _videoHeight = h;
                    ApplyTextureToTargets(_vlcTexture, flipY: true);
                }
                // 锁外上传纹理，完全不阻塞 LibVLC 后台解码线程。
                _vlcTexture.LoadRawTextureData(readPtr, readLen);
                _vlcTexture.Apply(false, false);
                if (!_preparedInvoked)
                {
                    _preparedInvoked = true;
                    OnPrepared?.Invoke();
                }
                if (_pendingVlcSeekNotify)
                {
                    _pendingVlcSeekNotify = false;
                    OnSeekCompleted?.Invoke(CurrentTimeSeconds);
                }
                return true;
            }
            return false;
        }

        private void UnpinBuffersLocked()
        {
            if (_buffersPinned)
            {
                if (_writePin.IsAllocated) _writePin.Free();
                if (_readyPin.IsAllocated) _readyPin.Free();
                if (_readPin.IsAllocated) _readPin.Free();
                _buffersPinned = false;
            }
        }

        private void ReleaseVlcBackend()
        {
            if (_vlcPlayer != null)
            {
                try { _vlcPlayer.Stop(); } catch { }
                try { _vlcPlayer.Dispose(); } catch { }
                _vlcPlayer = null;
            }
            if (_vlcMedia != null)
            {
                try { _vlcMedia.Dispose(); } catch { }
                _vlcMedia = null;
            }
            lock (_frameLock)
            {
                UnpinBuffersLocked();
                _writeBuf = null;
                _readyBuf = null;
                _readBuf = null;
                _frameDirty = false;
            }
            if (_vlcTexture != null)
            {
                SafeDestroy(_vlcTexture);
                _vlcTexture = null;
            }
        }

        #endregion
#endif
    }
}
