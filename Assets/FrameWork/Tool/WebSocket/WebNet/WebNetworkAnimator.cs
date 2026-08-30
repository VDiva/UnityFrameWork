using GameData;
using UnityEngine;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>独立负责大厅 Animator 状态同步，不包含位置逻辑。</summary>
    [RequireComponent(typeof(WebNetworkIdentity))]
    public sealed class WebNetworkAnimator : WebNetworkBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField, Min(0.02f)] float checkInterval = 0.1f;
        [SerializeField, Range(0f, 0.5f)] float fadeTime = 0.1f;
        [SerializeField, Min(0.2f)] float heartbeatInterval = 1f;

        float nextCheckTime;
        float lastSendTime;
        int lastStateHash;
        ulong sendSequence;
        ulong receiveSequence;

        void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
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
                animator == null || !animator.isActiveAndEnabled || Time.unscaledTime < nextCheckTime)
                return;

            nextCheckTime = Time.unscaledTime + checkInterval;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool changed = state.fullPathHash != lastStateHash;
            if (!changed && Time.unscaledTime - lastSendTime < heartbeatInterval)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkAnimation,
                NetworkAnimation = new NetworkAnimationData
                {
                    ObjectId = NetId,
                    StateHash = state.fullPathHash,
                    NormalizedTime = state.normalizedTime - Mathf.Floor(state.normalizedTime),
                    Sequence = ++sendSequence
                }
            });

            lastStateHash = state.fullPathHash;
            lastSendTime = Time.unscaledTime;
        }

        void OnAnimationReceived(NetworkAnimationData data)
        {
            if (HasAuthority || data.ObjectId != NetId || data.Sequence <= receiveSequence ||
                data.StateHash == 0 || animator == null || !animator.isActiveAndEnabled)
                return;

            receiveSequence = data.Sequence;
            if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != data.StateHash)
                animator.CrossFade(data.StateHash, fadeTime, 0, data.NormalizedTime);
        }

        void OnAuthorityChanged(WebNetworkIdentity changed)
        {
            if (changed != Identity) return;
            ResetNetworkState();
        }

        internal void ResetNetworkState()
        {
            sendSequence = 0;
            receiveSequence = 0;
            lastStateHash = 0;
            nextCheckTime = 0f;
            lastSendTime = 0f;
        }
    }
}
