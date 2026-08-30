using System;
using System.Collections;
using System.Collections.Generic;
using Concentus.Enums;
using Concentus.Structs;
using FrameWork;
using GameData;
using UnityEngine;
#if WEIXINMINIGAME && !UNITY_EDITOR
using WeChatWASM;
#endif
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>
    /// 自建房间范围语音：麦克风采集、RMS 语音检测、Opus 编解码及 3D 播放。
    /// 服务端依据网络位置只向同房间、指定距离内的玩家转发。
    /// </summary>
    public sealed class WebNetworkVoiceManager : MonoBehaviour
    {
        public const int SampleRate = 16000;
        /// <summary>每个 Opus 包包含的音频时长；20ms 适合实时语音。</summary>
        public const int FrameMilliseconds = 20;
        public const int SamplesPerFrame = SampleRate * FrameMilliseconds / 1000;
        /// <summary>接收端累计 100ms 再播放，减少频繁创建短 AudioClip 造成的卡顿。</summary>
        const int PlaybackSamples = SampleRate / 10;
        /// <summary>Opus 协议允许的单包最大长度。</summary>
        const int MaxOpusPacketBytes = 1275;

        // RMS 超过该阈值时判定为正在说话；环境噪声较大时可适当调高。
        [SerializeField, Range(0.001f, 0.1f)] float voiceThreshold = 0.015f;
        [SerializeField, Range(0f, 1f)] float playbackVolume = 1f;
        // AOI 开启时使用的 Unity 3D 音频衰减距离，应与服务端 voiceRange 保持一致。
        [SerializeField] float minDistance = 2f;
        [SerializeField] float maxDistance = 25f;
        // 检测到说话结束后继续发送一小段时间，避免句尾被 VAD 截断。
        [SerializeField] int silenceHangoverFrames = 15;

        // 本地屏蔽列表只影响收听，不影响服务端转发和对方麦克风。
        readonly HashSet<uint> mutedPlayers = new HashSet<uint>();
        // 每位远端玩家必须独立持有解码器，避免不同 Opus 流的内部状态互相污染。
        readonly Dictionary<uint, OpusDecoder> decoders = new Dictionary<uint, OpusDecoder>();
        readonly Dictionary<uint, List<float>> playbackBuffers = new Dictionary<uint, List<float>>();
        OpusEncoder encoder;
        AudioClip microphoneClip;
        string microphoneDevice;
        int microphoneReadPosition = -1;
        int hangoverFrames;
        bool permissionRequested;
#if WEIXINMINIGAME&&!UNITY_EDITOR
        WXRecorderManager wxRecorder;
        bool wxRecorderStarted;
        readonly List<byte> wxPcmBuffer = new List<byte>(SamplesPerFrame * 4);
#endif

        public static WebNetworkVoiceManager Instance { get; private set; }
        /// <summary>是否采集并发送自己的麦克风。</summary>
        public bool MicrophoneEnabled { get; set; } = false;

        /// <summary>是否播放其他玩家的语音。</summary>
        public bool ListeningEnabled { get; set; } = false;

        /// <summary>语音总开关；关闭时同时关闭麦克风和收听。</summary>
        public bool VoiceEnabled
        {
            get => MicrophoneEnabled && ListeningEnabled;
            set
            {
                MicrophoneEnabled = value;
                ListeningEnabled = value;
            }
        }

        public bool IsSpeaking { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateInstance()
        {
            if (Instance != null)
                return;

            var root = new GameObject(nameof(WebNetworkVoiceManager));
            root.AddComponent<WebNetworkVoiceManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 使用单声道 VoIP 模式；服务端不解码，只按房间、队伍和距离转发压缩包。
            encoder = new OpusEncoder(SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Bitrate = 20000,
                Complexity = 6,
                UseDTX = true,
                UseInbandFEC = false,
                PacketLossPercent = 10
            };

#if WEIXINMINIGAME&&!UNITY_EDITOR
            wxRecorder = WX.GetRecorderManager();
            wxRecorder.OnStart(() => wxRecorderStarted = true);
            wxRecorder.OnFrameRecorded(OnWxPcmFrameRecorded);
            wxRecorder.OnStop(_ =>
            {
                wxRecorderStarted = false;
                // 微信单次录音最长 10 分钟；仍处于语音房间时允许 Update 自动续录。
                permissionRequested = false;
            });
            wxRecorder.OnError(() =>
            {
                wxRecorderStarted = false;
                Debug.LogError("微信小游戏麦克风录音启动失败，请检查用户授权和设备麦克风权限。");
            });
#endif
        }

        void OnEnable()
        {
            WebNetworkManager.ServerMessageReceived += OnServerMessage;
            WebNetworkManager.ObjectDespawned += OnObjectDespawned;
        }

        void OnDisable()
        {
            WebNetworkManager.ServerMessageReceived -= OnServerMessage;
            WebNetworkManager.ObjectDespawned -= OnObjectDespawned;
            StopMicrophone();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            NetworkRoomData room = WebNetworkRoomManager.Instance?.CurrentRoom;
            bool canUseVoice = MicrophoneEnabled &&
                               LobbyWebNet.IsConnected &&
                               room != null &&
                               room.RoomId > WebNetworkRoomManager.LobbyRoomId;

            if (!canUseVoice)
            {
                StopMicrophone();
                return;
            }

#if WEIXINMINIGAME&&!UNITY_EDITOR
            if (!wxRecorderStarted)
            {
                if (!permissionRequested)
                    StartWxMicrophone();
                return;
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            // 普通 WebGL 没有 UnityEngine.Microphone；微信小游戏使用上面的 WX SDK 分支。
            return;
#else
            if (microphoneClip == null)
            {
                if (!permissionRequested)
                    StartCoroutine(RequestMicrophoneAndStart());
                return;
            }

            CaptureAvailableFrames();
#endif
        }

#if UNITY_EDITOR || !UNITY_WEBGL
        IEnumerator RequestMicrophoneAndStart()
        {
            permissionRequested = true;
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone) ||
                Microphone.devices.Length == 0)
                yield break;

            microphoneDevice = Microphone.devices[0];
            microphoneClip = Microphone.Start(microphoneDevice, true, 1, SampleRate);
            microphoneReadPosition = -1;
        }

        void CaptureAvailableFrames()
        {
            int writePosition = Microphone.GetPosition(microphoneDevice);
            if (writePosition < 0)
                return;

            if (microphoneReadPosition < 0)
            {
                microphoneReadPosition = writePosition;
                return;
            }

            int available = writePosition - microphoneReadPosition;
            if (available < 0)
                available += microphoneClip.samples;

            while (available >= SamplesPerFrame)
            {
                var samples = new float[SamplesPerFrame];
                microphoneClip.GetData(samples, microphoneReadPosition);
                microphoneReadPosition =
                    (microphoneReadPosition + SamplesPerFrame) % microphoneClip.samples;
                available -= SamplesPerFrame;
                ProcessCapturedFrame(samples);
            }
        }
#endif

#if WEIXINMINIGAME&& !UNITY_EDITOR
        void StartWxMicrophone()
        {
            permissionRequested = true;
            wxPcmBuffer.Clear();
            wxRecorder.Start(new RecorderManagerStartOption
            {
                duration = 600000,
                sampleRate = SampleRate,
                numberOfChannels = 1,
                format = "PCM",
                frameSize = 1,
                audioSource = "auto"
            });
        }

        void OnWxPcmFrameRecorded(OnFrameRecordedCallbackResult result)
        {
            if (result?.frameBuffer == null || result.frameBuffer.Length == 0)
                return;

            wxPcmBuffer.AddRange(result.frameBuffer);
            const int bytesPerSample = 2;
            int bytesPerOpusFrame = SamplesPerFrame * bytesPerSample;
            while (wxPcmBuffer.Count >= bytesPerOpusFrame)
            {
                var samples = new float[SamplesPerFrame];
                for (int i = 0; i < SamplesPerFrame; i++)
                {
                    int offset = i * bytesPerSample;
                    short pcm = (short)(wxPcmBuffer[offset] | (wxPcmBuffer[offset + 1] << 8));
                    samples[i] = pcm / 32768f;
                }

                wxPcmBuffer.RemoveRange(0, bytesPerOpusFrame);
                ProcessCapturedFrame(samples);
            }
        }
#endif

        void ProcessCapturedFrame(float[] samples)
        {
            // 计算当前帧的均方根响度，用于自动判断玩家是否正在说话。
            double energy = 0;
            for (int i = 0; i < samples.Length; i++)
                energy += samples[i] * samples[i];

            bool detected = Math.Sqrt(energy / samples.Length) >= voiceThreshold;
            if (detected)
                hangoverFrames = silenceHangoverFrames;
            else if (hangoverFrames > 0)
                hangoverFrames--;

            IsSpeaking = detected || hangoverFrames > 0;
            if (!IsSpeaking)
                return;

            // 只有 VAD 判定为说话时才编码上传，静音阶段不占用房间语音带宽。
            byte[] packet = new byte[MaxOpusPacketBytes];
            int packetLength = encoder.Encode(
                samples, 0, SamplesPerFrame,
                packet, 0, packet.Length);
            if (packetLength <= 0)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkVoice,
                Id = Convert.ToBase64String(packet, 0, packetLength)
            });
        }

        void OnServerMessage(Msg msg)
        {
            if (!ListeningEnabled ||
                msg.ServerMsgType != ServerMsgType.NetworkVoiceFrame ||
                msg.NetworkObjectId == 0 ||
                mutedPlayers.Contains(msg.NetworkObjectId) ||
                string.IsNullOrEmpty(msg.Id))
                return;

            byte[] packet;
            try
            {
                packet = Convert.FromBase64String(msg.Id);
            }
            catch (FormatException)
            {
                return;
            }

            if (packet.Length == 0 || packet.Length > MaxOpusPacketBytes ||
                WebNetworkManager.Instance == null ||
                !WebNetworkManager.Instance.TryGetObject(msg.NetworkObjectId, out WebNetworkIdentity identity) ||
                identity == null)
                return;

            // NetworkObjectId 表示说话玩家，用它维护各玩家独立的 Opus 解码状态。
            if (!decoders.TryGetValue(msg.NetworkObjectId, out OpusDecoder decoder))
            {
                decoder = new OpusDecoder(SampleRate, 1);
                decoders.Add(msg.NetworkObjectId, decoder);
            }

            var decoded = new float[SamplesPerFrame * 6];
            int decodedSamples = decoder.Decode(
                packet, 0, packet.Length,
                decoded, 0, decoded.Length,
                false);
            if (decodedSamples <= 0)
                return;

            if (!playbackBuffers.TryGetValue(msg.NetworkObjectId, out List<float> buffer))
            {
                buffer = new List<float>(PlaybackSamples * 2);
                playbackBuffers.Add(msg.NetworkObjectId, buffer);
            }

            for (int i = 0; i < decodedSamples; i++)
                buffer.Add(decoded[i]);

            if (buffer.Count < PlaybackSamples)
                return;

            float[] playback = buffer.GetRange(0, PlaybackSamples).ToArray();
            buffer.RemoveRange(0, PlaybackSamples);
            PlayVoiceFrame(
                identity.gameObject,
                playback,
                msg.NetworkRoomRequest?.Ready ?? true);
        }

        void PlayVoiceFrame(GameObject speaker, float[] samples, bool useAoi)
        {
            AudioSource source = speaker.GetComponent<AudioSource>();
            if (source == null)
                source = speaker.AddComponent<AudioSource>();

            source.playOnAwake = false;
            // AOI 开启时声音来自玩家所在位置；关闭时使用 2D 音频，让全房间都能听清。
            source.spatialBlend = useAoi ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.volume = playbackVolume;

            AudioClip clip = AudioClip.Create(
                $"Voice_{speaker.GetInstanceID()}",
                samples.Length,
                1,
                SampleRate,
                false);
            clip.SetData(samples, 0);
            source.PlayOneShot(clip);
            StartCoroutine(DestroyClipLater(clip));
        }

        void OnObjectDespawned(uint objectId)
        {
            decoders.Remove(objectId);
            playbackBuffers.Remove(objectId);
            mutedPlayers.Remove(objectId);
        }

        static IEnumerator DestroyClipLater(AudioClip clip)
        {
            yield return new WaitForSeconds(1f);
            if (clip != null)
                Destroy(clip);
        }

        void StopMicrophone()
        {
#if WEIXINMINIGAME&&!UNITY_EDITOR
            if (wxRecorderStarted || permissionRequested)
                wxRecorder.Stop();
            wxRecorderStarted = false;
            wxPcmBuffer.Clear();
#elif UNITY_EDITOR
            if (microphoneClip != null && !string.IsNullOrEmpty(microphoneDevice))
                Microphone.End(microphoneDevice);
#endif

            microphoneClip = null;
            microphoneReadPosition = -1;
            hangoverFrames = 0;
            IsSpeaking = false;
            permissionRequested = false;
        }

        public void SetPlayerMuted(uint objectId, bool muted)
        {
            if (muted)
                mutedPlayers.Add(objectId);
            else
                mutedPlayers.Remove(objectId);
        }

        /// <summary>查询指定网络玩家是否已被本地静音。</summary>
        public bool IsPlayerMuted(uint objectId)
        {
            return mutedPlayers.Contains(objectId);
        }
    }
}
