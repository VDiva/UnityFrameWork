using System.Runtime.CompilerServices;
using UnityEngine;

namespace FrameWork.Net.Network
{
    /// <summary>所有 Web 实时同步组件的基类，提供类似 Mirror NetworkBehaviour 的身份访问。</summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        NetworkIdentity identity;

        public NetworkIdentity Identity => identity != null ? identity : identity = GetComponent<NetworkIdentity>();
        public uint NetId => Identity.ObjectId;
        public bool IsLocalPlayer => Identity.IsLocalPlayer;
        public bool HasAuthority => Identity.HasAuthority;
        public string PlayerId => Identity.PlayerId;

        /// <summary>
        /// 旧版手动 RPC 的兼容入口。启用 NetRpc Weaver 后，业务 RPC 不再需要手动调用此方法。
        /// </summary>
        protected bool RelayClientRpc(object[] arguments,
            [CallerMemberName] string methodName = null)
        {
            return NetworkRpcDispatcher.TryRelay(this, methodName, arguments);
        }

        /// <summary>旧版无参数 RPC 的兼容入口。</summary>
        protected bool RelayClientRpcNoArgs([CallerMemberName] string methodName = null)
        {
            return NetworkRpcDispatcher.TryRelay(this, methodName, System.Array.Empty<object>());
        }
    }
}
