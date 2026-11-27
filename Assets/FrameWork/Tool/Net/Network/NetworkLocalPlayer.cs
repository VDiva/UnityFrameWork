using UnityEngine;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// 显式标记场景中的本地玩家。每个可联机场景只能有一个启用的标记，
    /// 不再通过 FindObjectsOfType 后取第一个对象来猜测本地玩家。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class NetworkLocalPlayer : MonoBehaviour
    {
        public static NetworkLocalPlayer Active { get; private set; }

        NetworkIdentity identity;

        void Awake()
        {
            identity = GetComponent<NetworkIdentity>();
        }

        void OnEnable()
        {
            if (Active != null && Active != this)
                Debug.LogError("场景中存在多个启用的 NetworkLocalPlayer。", this);
            Active = this;
            Register();
        }

        void Start()
        {
            Register();
        }

        void OnDisable()
        {
            NetworkManager.Instance?.ClearLocalPlayer(identity);
            if (Active == this)
                Active = null;
        }

        public void Register()
        {
            if (identity == null)
                identity = GetComponent<NetworkIdentity>();
            NetworkManager.Instance?.SetLocalPlayer(identity);
        }
    }
}
