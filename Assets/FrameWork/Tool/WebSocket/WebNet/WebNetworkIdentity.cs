using UnityEngine;

namespace FrameWork.Script.WebNet
{
    /// <summary>大厅网络对象身份，ObjectId 只由 Web 服务器分配，作用类似 Mirror netId。</summary>
    public sealed class WebNetworkIdentity : MonoBehaviour
    {
        public uint ObjectId { get; private set; }
        public string PlayerId { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public string PrefabId { get; private set; }
        public string OwnerPlayerId { get; private set; }
        public bool IsPlayerObject { get; private set; }
        public bool IsAiObject { get; private set; }
        public bool HasAuthority => IsLocalPlayer || (!string.IsNullOrWhiteSpace(OwnerPlayerId) &&
                                                     OwnerPlayerId == FrameWork.WebSocket.WebNet.CurrentUserId);

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

            // 对象池会复用同步组件；绑定新的 ObjectId 时必须清除上一个网络对象的
            // 序列号和首帧状态，否则重连后的低序列快照会被当成旧消息丢弃。
            foreach (WebNetworkTransform component in GetComponents<WebNetworkTransform>())
                component.ResetNetworkState();
            foreach (WebNetworkAnimator component in GetComponents<WebNetworkAnimator>())
                component.ResetNetworkState();
            foreach (WebNetworkSpineAnimator component in GetComponents<WebNetworkSpineAnimator>())
                component.ResetNetworkState();
        }

        internal void UpdateAuthority(string ownerPlayerId)
        {
            OwnerPlayerId = ownerPlayerId ?? string.Empty;
        }

        // [WebClientRpc(IncludeSender = true)]
        // public void SetParent(int transformId)
        // {
        //     var parent = (GameMapLayout)transformId;
        //     transform.SetParent(GameRoomMrg.Instance.GetLayoutTran(parent));
        // }
    }
}
