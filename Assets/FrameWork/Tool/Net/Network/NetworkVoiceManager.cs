using System;
using System.Collections;
using System.Collections.Generic;
using Concentus.Enums;
using Concentus.Structs;
using FrameWork;
using GameData;
using Google.Protobuf;
using UnityEngine;
using UnityWebSocket;
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
using WeChatWASM;
#endif
using FrameWork.Net;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// 自建房间范围语音：麦克风采集、RMS 语音检测、Opus 编解码及 3D 播放。
    /// 服务端依据网络位置只向同房间、指定距离内的玩家转发。
    /// </summary>
    public sealed class NetworkVoiceManager : MonoBehaviour
    {
        public const int SampleRate = 16000;
        /// <summary>每个 Opus 包包含的音频时长；20ms 适合实时语音。</summary>
        public const int FrameMilliseconds = 20;
        public const int SamplesPerFrame = SampleRate * FrameMilliseconds / 1000;
        /// <summary>流式环形 AudioClip 容量（1 秒 = 50 帧），每个说话者仅创建一次。</summary>
        const int RingClipSamples = SampleRate;
        /// <summary>开播前预缓冲帧数（60ms = 3 帧），抵抗网络微抖动避免断续。</summary>
        const int PrebufferSamples = SamplesPerFrame * 3;
        /// <summary>Opus 协议允许的单包最大长度。</summary>
        const int MaxOpusPacketBytes = 1275;
        const float PermissionRetryDelaySeconds = 2f;
        const float IdleStopGraceSeconds = 0.2f;

        sealed class SpeakerStream
        {
            public OpusDecoder Decoder;
            public AudioSource Source;
            public AudioClip RingClip;
            public readonly float[] Prebuffer = new float[PrebufferSamples * 2];
            public int PrebufferCount;
            public int WriteSamplePos;
            public int ClearedSamplePos;
            public float PlaybackEndTime;
            public uint LastVoiceSeq;
            public bool HasVoiceSeq;
            public bool Playing;
        }

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
        // 每位远端玩家独立持有解码器与环形流式 AudioClip，彻底消除每帧创建 AudioClip 与协程开销。
        readonly Dictionary<uint, SpeakerStream> speakers = new Dictionary<uint, SpeakerStream>();
        readonly List<SpeakerStream> activeSpeakers = new List<SpeakerStream>(8);

        // 复用采集/编解码缓冲，避免 50Hz 语音链路频繁 GC 分配。
        readonly float[] captureBuffer = new float[SamplesPerFrame];
        readonly byte[] encodeBuffer = new byte[MaxOpusPacketBytes];
        readonly byte[] receiveOpusBuffer = new byte[MaxOpusPacketBytes];
        readonly float[] decodeBuffer = new float[SamplesPerFrame * 6];
        readonly float[] frameWriteBuffer = new float[SamplesPerFrame];
        readonly float[] silenceFrame = new float[SamplesPerFrame];
        readonly float[] fullSilenceRing = new float[RingClipSamples];

        OpusEncoder encoder;
        AudioClip microphoneClip;
        string microphoneDevice;
        int microphoneReadPosition = -1;
        int hangoverFrames;
        uint outgoingVoiceSeq;
        bool permissionRequested;
        float nextPermissionRetryTime;
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
        WXRecorderManager wxRecorder;
        bool wxRecorderStarted;
        readonly byte[] wxPcmRing = new byte[SamplesPerFrame * 16];
        int wxPcmHead;
        int wxPcmCount;
#endif

        public static NetworkVoiceManager Instance { get; private set; }
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateInstance()
        {
            if (Instance != null)
                return;

            var root = new GameObject(nameof(NetworkVoiceManager));
            root.AddComponent<NetworkVoiceManager>();
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

#if WX_MINIGAME_BUILD && !UNITY_EDITOR
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
                permissionRequested = false;
                nextPermissionRetryTime = Time.unscaledTime + PermissionRetryDelaySeconds;
                Debug.LogError("微信小游戏麦克风录音启动失败，请检查用户授权和设备麦克风权限。");
            });
#endif
        }

        OpusEncoder GetOrCreateEncoder()
        {
            if (encoder == null)
            {
                // 开启 Opus 带内前向纠错（In-Band FEC），配合非可靠有序 UDP 数据报在单包丢失时零延迟补偿还原语音帧。
                encoder = new OpusEncoder(SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP)
                {
                    Bitrate = 24000,
                    Complexity = 6,
                    UseDTX = true,
                    UseInbandFEC = true,
                    PacketLossPercent = 10
                };
            }
            return encoder;
        }

        void OnEnable()
        {
            NetworkManager.ServerMessageReceived += OnServerMessage;
            NetworkManager.ObjectDespawned += OnObjectDespawned;
            GameNet.OnClose += OnConnectionClosed;
        }

        void OnDisable()
        {
            NetworkManager.ServerMessageReceived -= OnServerMessage;
            NetworkManager.ObjectDespawned -= OnObjectDespawned;
            GameNet.OnClose -= OnConnectionClosed;
            StopMicrophone();
            ClearRemoteVoiceState();
        }

        void OnDestroy()
        {
            ClearRemoteVoiceState();
            if (Instance == this)
                Instance = null;
        }

        void OnConnectionClosed(object sender, CloseEventArgs e)
        {
            StopMicrophone();
            ClearRemoteVoiceState();
        }

        void ClearRemoteVoiceState()
        {
            foreach (SpeakerStream stream in speakers.Values)
            {
                if (stream.Source != null) stream.Source.Stop();
                if (stream.RingClip != null) Destroy(stream.RingClip);
            }
            speakers.Clear();
            activeSpeakers.Clear();
        }

        void Update()
        {
            UpdateActiveSpeakers();

            NetworkRoomData room = NetworkRoomManager.Instance?.CurrentRoom;
            bool canUseVoice = MicrophoneEnabled &&
                               GameNet.IsConnected &&
                               room != null &&
                               room.RoomId > NetworkRoomManager.LobbyRoomId;

            if (!canUseVoice)
            {
                StopMicrophone();
                return;
            }

            if (Time.unscaledTime < nextPermissionRetryTime)
                return;

#if WX_MINIGAME_BUILD && !UNITY_EDITOR
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
            {
                permissionRequested = false;
                nextPermissionRetryTime = Time.unscaledTime + PermissionRetryDelaySeconds;
                yield break;
            }

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
                microphoneClip.GetData(captureBuffer, microphoneReadPosition);
                microphoneReadPosition =
                    (microphoneReadPosition + SamplesPerFrame) % microphoneClip.samples;
                available -= SamplesPerFrame;
                ProcessCapturedFrame(captureBuffer);
            }
        }
#endif

#if WX_MINIGAME_BUILD && !UNITY_EDITOR
        void StartWxMicrophone()
        {
            permissionRequested = true;
            wxPcmHead = 0;
            wxPcmCount = 0;
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

            byte[] src = result.frameBuffer;
            int ringCap = wxPcmRing.Length;
            for (int i = 0; i < src.Length; i++)
            {
                if (wxPcmCount == ringCap)
                {
                    wxPcmHead = (wxPcmHead + 1) % ringCap;
                    wxPcmCount--;
                }
                wxPcmRing[(wxPcmHead + wxPcmCount) % ringCap] = src[i];
                wxPcmCount++;
            }

            const int bytesPerSample = 2;
            int bytesPerOpusFrame = SamplesPerFrame * bytesPerSample;
            while (wxPcmCount >= bytesPerOpusFrame)
            {
                for (int i = 0; i < SamplesPerFrame; i++)
                {
                    int b0 = wxPcmRing[(wxPcmHead + i * 2) % ringCap];
                    int b1 = wxPcmRing[(wxPcmHead + i * 2 + 1) % ringCap];
                    short pcm = (short)(b0 | (b1 << 8));
                    captureBuffer[i] = pcm * (1f / 32768f);
                }
                wxPcmHead = (wxPcmHead + bytesPerOpusFrame) % ringCap;
                wxPcmCount -= bytesPerOpusFrame;
                ProcessCapturedFrame(captureBuffer);
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
            int packetLength = GetOrCreateEncoder().Encode(
                samples, 0, SamplesPerFrame,
                encodeBuffer, 0, encodeBuffer.Length);
            if (packetLength <= 0)
                return;

            uint seq = ++outgoingVoiceSeq;
            if (seq == 0) seq = ++outgoingVoiceSeq;

            GameNet.SendRealtime(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkVoice,
                VoiceSequence = seq,
                VoiceData = ByteString.CopyFrom(encodeBuffer, 0, packetLength)
            });
        }

        void OnServerMessage(Msg msg)
        {
            if (!ListeningEnabled ||
                msg.ServerMsgType != ServerMsgType.NetworkVoiceFrame ||
                msg.NetworkObjectId == 0 ||
                mutedPlayers.Contains(msg.NetworkObjectId))
                return;

            int packetLen = 0;
            byte[] packetBuf = receiveOpusBuffer;
            if (msg.VoiceData != null && msg.VoiceData.Length > 0)
            {
                if (msg.VoiceData.Length > MaxOpusPacketBytes)
                    return;
                packetLen = msg.VoiceData.Length;
                msg.VoiceData.Span.CopyTo(new Span<byte>(receiveOpusBuffer, 0, packetLen));
            }
            else if (!string.IsNullOrEmpty(msg.Id))
            {
                try
                {
                    packetBuf = Convert.FromBase64String(msg.Id);
                    packetLen = packetBuf.Length;
                }
                catch (FormatException)
                {
                    return;
                }
            }

            if (packetLen <= 0 || packetLen > MaxOpusPacketBytes ||
                NetworkManager.Instance == null ||
                !NetworkManager.Instance.TryGetObject(msg.NetworkObjectId, out NetworkIdentity identity) ||
                identity == null)
                return;

            if (!speakers.TryGetValue(msg.NetworkObjectId, out SpeakerStream stream))
            {
                stream = new SpeakerStream
                {
                    Decoder = new OpusDecoder(SampleRate, 1),
                    RingClip = AudioClip.Create($"VoiceRing_{msg.NetworkObjectId}", RingClipSamples, 1, SampleRate, false)
                };
                speakers.Add(msg.NetworkObjectId, stream);
            }

            uint incomingSeq = msg.VoiceSequence;
            bool useAoi = msg.NetworkRoomRequest?.Ready ?? true;
            if (incomingSeq != 0)
            {
                if (stream.HasVoiceSeq)
                {
                    int seqDiff = unchecked((int)(incomingSeq - stream.LastVoiceSeq));
                    if (seqDiff <= 0)
                        return;
                    // 若恰好丢失 1 帧（seqDiff == 2），先使用当前包夹带的 Opus FEC 数据解码补偿上一帧语音！
                    if (seqDiff == 2)
                    {
                        int fecSamples = stream.Decoder.Decode(
                            packetBuf, 0, packetLen,
                            decodeBuffer, 0, SamplesPerFrame,
                            true);
                        if (fecSamples > 0)
                            AppendSpeakerSamples(stream, identity.gameObject, decodeBuffer, fecSamples, useAoi);
                    }
                }
                stream.LastVoiceSeq = incomingSeq;
                stream.HasVoiceSeq = true;
            }

            int decodedSamples = stream.Decoder.Decode(
                packetBuf, 0, packetLen,
                decodeBuffer, 0, decodeBuffer.Length,
                false);
            if (decodedSamples <= 0)
                return;

            AppendSpeakerSamples(
                stream,
                identity.gameObject,
                decodeBuffer,
                decodedSamples,
                useAoi);
        }

        void AppendSpeakerSamples(SpeakerStream stream, GameObject speakerGo, float[] samples, int sampleCount, bool useAoi)
        {
            if (stream.Source == null)
            {
                AudioSource source = speakerGo.GetComponent<AudioSource>();
                if (source == null)
                    source = speakerGo.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.clip = stream.RingClip;
                stream.Source = source;
            }

            stream.Source.spatialBlend = useAoi ? 1f : 0f;
            stream.Source.minDistance = minDistance;
            stream.Source.maxDistance = maxDistance;
            stream.Source.volume = playbackVolume;

            float now = Time.unscaledTime;
            if (!stream.Playing)
            {
                int canCopy = Math.Min(sampleCount, stream.Prebuffer.Length - stream.PrebufferCount);
                if (canCopy > 0)
                {
                    Array.Copy(samples, 0, stream.Prebuffer, stream.PrebufferCount, canCopy);
                    stream.PrebufferCount += canCopy;
                }

                if (stream.PrebufferCount < PrebufferSamples)
                    return;

                stream.RingClip.SetData(fullSilenceRing, 0);
                stream.WriteSamplePos = 0;
                stream.ClearedSamplePos = 0;
                WriteRingSamples(stream, stream.Prebuffer, stream.PrebufferCount);
                int startedSamples = stream.PrebufferCount;
                stream.PrebufferCount = 0;

                stream.Source.timeSamples = 0;
                stream.Source.Play();
                stream.Playing = true;
                stream.PlaybackEndTime = now + (float)startedSamples / SampleRate;
                if (!activeSpeakers.Contains(stream))
                    activeSpeakers.Add(stream);
                return;
            }

            // 如果播放头已经大幅落后或追平写指针，重新对齐到当前播放头前方一小段安全裕量。
            int playPos = stream.Source.timeSamples;
            int ahead = (stream.WriteSamplePos - playPos + RingClipSamples) % RingClipSamples;
            if (now > stream.PlaybackEndTime || ahead > RingClipSamples - SamplesPerFrame * 4)
            {
                stream.WriteSamplePos = (playPos + SamplesPerFrame) % RingClipSamples;
                stream.PlaybackEndTime = now + (float)SamplesPerFrame / SampleRate;
            }

            WriteRingSamples(stream, samples, sampleCount);
            stream.PlaybackEndTime = Mathf.Max(stream.PlaybackEndTime, now) + (float)sampleCount / SampleRate;
        }

        void WriteRingSamples(SpeakerStream stream, float[] samples, int count)
        {
            int offset = 0;
            while (count - offset >= SamplesPerFrame)
            {
                Array.Copy(samples, offset, frameWriteBuffer, 0, SamplesPerFrame);
                stream.RingClip.SetData(frameWriteBuffer, stream.WriteSamplePos);
                stream.WriteSamplePos = (stream.WriteSamplePos + SamplesPerFrame) % RingClipSamples;
                offset += SamplesPerFrame;
            }
            int rem = count - offset;
            if (rem > 0)
            {
                Array.Clear(frameWriteBuffer, 0, SamplesPerFrame);
                Array.Copy(samples, offset, frameWriteBuffer, 0, rem);
                stream.RingClip.SetData(frameWriteBuffer, stream.WriteSamplePos);
                stream.WriteSamplePos = (stream.WriteSamplePos + rem) % RingClipSamples;
            }
        }

        void UpdateActiveSpeakers()
        {
            if (activeSpeakers.Count == 0)
                return;

            float now = Time.unscaledTime;
            for (int i = activeSpeakers.Count - 1; i >= 0; i--)
            {
                SpeakerStream stream = activeSpeakers[i];
                if (stream.Source == null || !stream.Playing)
                {
                    stream.Playing = false;
                    activeSpeakers.RemoveAt(i);
                    continue;
                }

                if (now > stream.PlaybackEndTime + IdleStopGraceSeconds)
                {
                    stream.Source.Stop();
                    stream.Playing = false;
                    stream.PrebufferCount = 0;
                    activeSpeakers.RemoveAt(i);
                    continue;
                }

                // 将已经播放过的扇区清零，防止环形缓冲追尾重播残留旧语音。
                int playPos = stream.Source.timeSamples;
                int played = (playPos - stream.ClearedSamplePos + RingClipSamples) % RingClipSamples;
                while (played >= SamplesPerFrame * 2)
                {
                    stream.RingClip.SetData(silenceFrame, stream.ClearedSamplePos);
                    stream.ClearedSamplePos = (stream.ClearedSamplePos + SamplesPerFrame) % RingClipSamples;
                    played -= SamplesPerFrame;
                }
            }
        }

        void OnObjectDespawned(uint objectId)
        {
            if (speakers.TryGetValue(objectId, out SpeakerStream stream))
            {
                if (stream.Source != null) stream.Source.Stop();
                if (stream.RingClip != null) Destroy(stream.RingClip);
                activeSpeakers.Remove(stream);
                speakers.Remove(objectId);
            }
            mutedPlayers.Remove(objectId);
        }

        void StopMicrophone()
        {
#if WX_MINIGAME_BUILD && !UNITY_EDITOR
            if (wxRecorderStarted || permissionRequested)
                wxRecorder.Stop();
            wxRecorderStarted = false;
            wxPcmHead = 0;
            wxPcmCount = 0;
#elif UNITY_EDITOR || !UNITY_WEBGL
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
