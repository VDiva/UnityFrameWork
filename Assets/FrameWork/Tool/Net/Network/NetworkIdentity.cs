

using UnityEngine;

namespace FrameWork.Net.Network
{
    /// <summary>大厅网络对象身份，ObjectId 只由 Web 服务器分配，作用类似 Mirror netId。</summary>
    public sealed class NetworkIdentity : MonoBehaviour
    {
        public uint ObjectId { get; private set; }
        public string PlayerId { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public string PrefabId { get; private set; }
        public string OwnerPlayerId { get; private set; }
        public bool IsPlayerObject { get; private set; }
        public bool IsAiObject { get; private set; }
        public bool HasAuthority => IsLocalPlayer || (!string.IsNullOrWhiteSpace(OwnerPlayerId) &&
                                                     OwnerPlayerId == FrameWork.Net.GameNet.CurrentUserId);

        internal NetworkTransform CachedTransform { get; private set; }
        internal NetworkAnimator CachedAnimator { get; private set; }
        internal NetworkSpineAnimator CachedSpineAnimator { get; private set; }
        internal NetworkSyncVars CachedSyncVars { get; private set; }
        bool componentCacheReady;

        internal void RefreshComponentCache()
        {
            CachedTransform = GetComponent<NetworkTransform>();
            CachedAnimator = GetComponent<NetworkAnimator>();
            CachedSpineAnimator = GetComponent<NetworkSpineAnimator>();
            CachedSyncVars = GetComponent<NetworkSyncVars>();
            componentCacheReady = true;
        }

        internal void EnsureComponentCache()
        {
            if (!componentCacheReady) RefreshComponentCache();
        }

        internal void Configure(uint objectId, string playerId, bool isLocalPlayer,
            string prefabId = "", string ownerPlayerId = "", bool playerObject = true, bool aiObject = false)
        {
            ObjectId = objectId;
            PlayerId = playerId ?? string.Empty;
            IsLocalPlayer = isLocalPlayer;
            PrefabId = prefabId ?? string.Empty;
            OwnerPlayerId = ownerPlayerId ?? string.Empty;
            IsPlayerObject = playerObject;
            IsAiObject = aiObject;
            RefreshComponentCache();

            // 对象池会复用同步组件；绑定新的 ObjectId 时必须清除上一个网络对象的
            // 序列号和首帧状态，否则重连后的低序列快照会被当成旧消息丢弃。
            foreach (NetworkTransform component in GetComponents<NetworkTransform>())
                component.ResetNetworkState();
            foreach (NetworkAnimator component in GetComponents<NetworkAnimator>())
                component.ResetNetworkState();
            foreach (NetworkSpineAnimator component in GetComponents<NetworkSpineAnimator>())
                component.ResetNetworkState();
            foreach (NetworkSyncVars component in GetComponents<NetworkSyncVars>())
                component.ResetNetworkState();
        }

        internal void UpdateAuthority(string ownerPlayerId)
        {
            OwnerPlayerId = ownerPlayerId ?? string.Empty;
        }

        [ClientRpc(IncludeSender = true)]
        public void SetParent(int transformId)
        {
            transform.SetParent(NetworkIntegration.ResolveParent(transformId));
        }
    }
}
