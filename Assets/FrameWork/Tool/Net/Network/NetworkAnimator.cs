using GameData;
using UnityEngine;
using FrameWork.Net;

namespace FrameWork.Net.Network
{
    /// <summary>独立负责大厅 Animator 状态同步，不包含位置逻辑。</summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class NetworkAnimator : NetworkBehaviour
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
        internal int tickIndex = -1;

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
            NetworkManager.AuthorityChanged += OnAuthorityChanged;
            NetworkManager.RegisterTickableAnimator(this);
        }

        void OnDisable()
        {
            NetworkManager.AuthorityChanged -= OnAuthorityChanged;
            NetworkManager.UnregisterTickableAnimator(this);
        }

        internal void TickAuthority(float now, bool soloRoom)
        {
            if (!HasAuthority || NetId == 0 || !GameNet.IsConnected ||
                animator == null || !animator.isActiveAndEnabled || now < nextCheckTime)
                return;

            bool soloAi = soloRoom && Identity.IsAiObject;
            float interval = soloAi ? Mathf.Max(checkInterval, 0.5f) : checkInterval;
            float hb = soloAi ? Mathf.Max(heartbeatInterval, 2f) : heartbeatInterval;

            nextCheckTime = now + interval;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool changed = state.fullPathHash != lastStateHash;
            if (!changed && now - lastSendTime < hb)
                return;

            NetworkManager.SendAnimation(new NetworkAnimationData
            {
                ObjectId = NetId,
                StateHash = state.fullPathHash,
                NormalizedTime = state.normalizedTime - Mathf.Floor(state.normalizedTime),
                Sequence = ++sendSequence
            });

            lastStateHash = state.fullPathHash;
            lastSendTime = now;
        }

        internal void OnAnimationReceived(NetworkAnimationData data)
        {
            if (HasAuthority || data.ObjectId != NetId || data.Sequence <= receiveSequence ||
                data.StateHash == 0 || animator == null || !animator.isActiveAndEnabled)
                return;

            receiveSequence = data.Sequence;
            if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != data.StateHash)
                animator.CrossFade(data.StateHash, fadeTime, 0, data.NormalizedTime);
        }

        void OnAuthorityChanged(NetworkIdentity changed)
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
