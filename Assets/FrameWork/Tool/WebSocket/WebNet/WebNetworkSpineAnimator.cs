using GameData;
using Spine;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>
    /// 同步 Spine SkeletonAnimation 当前播放的动画。
    /// 有控制权的一端上传动画名、循环状态和播放进度，其他客户端负责播放。
    /// 不要和 WebNetworkAnimator 同时挂在同一个网络角色上。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WebNetworkIdentity))]
    public sealed class WebNetworkSpineAnimator : WebNetworkBehaviour
    {
        const string ClearTrackMessage = "__WEB_SPINE_CLEAR_TRACK__";
        [Header("Spine 组件")]
        [Tooltip("需要同步的 SkeletonAnimation。")]
        [SerializeField] SkeletonAnimation skeletonAnimation;

        [Tooltip("关闭“同步所有轨道”时，只同步该 Spine Track。")]
        [Min(0)]
        [SerializeField] int trackIndex;

        [Tooltip("开启后同步所有当前正在播放动画的 Spine Track。")]
        [SerializeField] bool syncAllTracks = true;

        [Header("同步设置")]
        [Tooltip("检查本地动画变化的时间间隔。")]
        [Min(0.02f)]
        [SerializeField] float checkInterval = 0.1f;

        [Tooltip("动画没有改变时，定期发送播放进度用于校正。")]
        [Min(0.2f)]
        [SerializeField] float heartbeatInterval = 1f;

        [Tooltip("远端播放进度偏差超过该比例时才校正，避免频繁跳帧。")]
        [Range(0.01f, 0.5f)]
        [SerializeField] float normalizedTimeCorrectionThreshold = 0.15f;

        [Tooltip("远端切换 Spine 动画时使用的混合时间。")]
        [Min(0f)]
        [SerializeField] float mixDuration = 0.1f;

        float nextCheckTime;
        readonly Dictionary<int, string> lastAnimationNames = new Dictionary<int, string>();
        readonly Dictionary<int, bool> lastLoops = new Dictionary<int, bool>();
        readonly Dictionary<int, float> lastTrackSendTimes = new Dictionary<int, float>();
        ulong sendSequence;
        ulong receiveSequence;

        void Reset()
        {
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        }

        void Awake()
        {
            if (skeletonAnimation == null)
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        }

        void OnEnable()
        {
            WebNetworkManager.AnimationReceived += OnAnimationReceived;
            WebNetworkManager.AuthorityChanged += OnAuthorityChanged;
        }

        void OnDisable()
        {
            WebNetworkManager.AnimationReceived -= OnAnimationReceived;
            WebNetworkManager.AuthorityChanged -= OnAuthorityChanged;
        }

        void Update()
        {
            if (!HasAuthority || NetId == 0 || !LobbyWebNet.IsConnected ||
                skeletonAnimation == null || !skeletonAnimation.isActiveAndEnabled ||
                Time.unscaledTime < nextCheckTime)
                return;

            nextCheckTime = Time.unscaledTime + checkInterval;
            Spine.AnimationState animationState = skeletonAnimation.AnimationState;
            if (syncAllTracks)
            {
                var activeTracks = new HashSet<int>();
                for (int index = 0; index < animationState.Tracks.Count; index++)
                {
                    if (GetCurrentTrack(animationState, index)?.Animation != null)
                        activeTracks.Add(index);
                    TrySendTrack(animationState, index);
                }

                var removedTracks = new List<int>();
                foreach (int index in lastAnimationNames.Keys)
                    if (!activeTracks.Contains(index))
                        removedTracks.Add(index);
                foreach (int index in removedTracks)
                    SendClearTrack(index);
            }
            else
            {
                TrySendTrack(animationState, trackIndex);
            }
        }

        void TrySendTrack(Spine.AnimationState animationState, int index)
        {
            TrackEntry entry = GetCurrentTrack(animationState, index);
            if (entry?.Animation == null)
                return;

            string animationName = entry.Animation.Name;
            bool changed = !lastAnimationNames.TryGetValue(index, out string previousName) ||
                           animationName != previousName ||
                           !lastLoops.TryGetValue(index, out bool previousLoop) ||
                           entry.Loop != previousLoop;
            float trackLastSendTime = lastTrackSendTimes.TryGetValue(index, out float value) ? value : 0f;
            if (!changed && Time.unscaledTime - trackLastSendTime < heartbeatInterval)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkAnimation,
                NetworkAnimation = new NetworkAnimationData
                {
                    ObjectId = NetId,
                    AnimationName = animationName,
                    Loop = entry.Loop,
                    TrackIndex = index,
                    NormalizedTime = GetNormalizedTime(entry),
                    Sequence = ++sendSequence
                }
            });

            lastAnimationNames[index] = animationName;
            lastLoops[index] = entry.Loop;
            lastTrackSendTimes[index] = Time.unscaledTime;
        }

        void SendClearTrack(int index)
        {
            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkAnimation,
                NetworkAnimation = new NetworkAnimationData
                {
                    ObjectId = NetId,
                    AnimationName = ClearTrackMessage,
                    TrackIndex = index,
                    Sequence = ++sendSequence
                }
            });
            lastAnimationNames.Remove(index);
            lastLoops.Remove(index);
            lastTrackSendTimes.Remove(index);
        }

        void OnAnimationReceived(NetworkAnimationData data)
        {
            // AnimationName 为空的是 WebNetworkAnimator 消息，不由 Spine 组件处理。
            if (HasAuthority || data == null || data.ObjectId != NetId ||
                data.Sequence <= receiveSequence || string.IsNullOrWhiteSpace(data.AnimationName) ||
                skeletonAnimation == null || !skeletonAnimation.isActiveAndEnabled)
                return;

            receiveSequence = data.Sequence;
            int remoteTrackIndex = Mathf.Max(0, data.TrackIndex);
            if (data.AnimationName == ClearTrackMessage)
            {
                skeletonAnimation.AnimationState.ClearTrack(remoteTrackIndex);
                return;
            }
            TrackEntry current = GetCurrentTrack(skeletonAnimation.AnimationState, remoteTrackIndex);
            bool animationChanged = current?.Animation == null ||
                                    current.Animation.Name != data.AnimationName ||
                                    current.Loop != data.Loop;

            if (animationChanged)
            {
                TrackEntry newEntry;
                try
                {
                    newEntry = skeletonAnimation.AnimationState.SetAnimation(
                        remoteTrackIndex, data.AnimationName, data.Loop);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning(
                        $"[WebNetworkSpineAnimator] 找不到 Spine 动画 {data.AnimationName}：{exception.Message}", this);
                    return;
                }

                newEntry.MixDuration = mixDuration;
                SetNormalizedTime(newEntry, data.NormalizedTime);
                return;
            }

            float localNormalizedTime = GetNormalizedTime(current);
            float difference = current.Loop
                ? Mathf.Abs(Mathf.DeltaAngle(localNormalizedTime * 360f, data.NormalizedTime * 360f)) / 360f
                : Mathf.Abs(localNormalizedTime - data.NormalizedTime);

            if (difference >= normalizedTimeCorrectionThreshold)
                SetNormalizedTime(current, data.NormalizedTime);
        }

        void OnAuthorityChanged(WebNetworkIdentity changed)
        {
            if (changed != Identity)
                return;

            ResetNetworkState();
        }

        internal void ResetNetworkState()
        {
            sendSequence = 0;
            receiveSequence = 0;
            lastAnimationNames.Clear();
            lastLoops.Clear();
            lastTrackSendTimes.Clear();
            nextCheckTime = 0f;
        }

        static float GetNormalizedTime(TrackEntry entry)
        {
            if (entry?.Animation == null || entry.AnimationEnd <= 0f)
                return 0f;

            float normalized = entry.AnimationTime / entry.AnimationEnd;
            return entry.Loop ? Mathf.Repeat(normalized, 1f) : Mathf.Clamp01(normalized);
        }

        // 当前项目使用的 Spine 运行库通过 Tracks 集合访问当前轨道，没有 GetCurrent 方法。
        static TrackEntry GetCurrentTrack(Spine.AnimationState animationState, int index)
        {
            if (animationState == null || index < 0 || index >= animationState.Tracks.Count)
                return null;

            return animationState.Tracks.Items[index];
        }

        static void SetNormalizedTime(TrackEntry entry, float normalizedTime)
        {
            if (entry?.Animation == null || entry.AnimationEnd <= 0f)
                return;

            float normalized = entry.Loop
                ? Mathf.Repeat(normalizedTime, 1f)
                : Mathf.Clamp01(normalizedTime);
            entry.TrackTime = normalized * entry.AnimationEnd;
        }
    }
}
