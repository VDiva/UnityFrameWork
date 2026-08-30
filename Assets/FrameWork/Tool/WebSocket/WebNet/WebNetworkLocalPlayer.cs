using UnityEngine;

namespace FrameWork.Script.WebNet
{
    /// <summary>
    /// 显式标记场景中的本地玩家。每个可联机场景只能有一个启用的标记，
    /// 不再通过 FindObjectsOfType 后取第一个对象来猜测本地玩家。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WebNetworkIdentity))]
    public sealed class WebNetworkLocalPlayer : MonoBehaviour
    {
        public static WebNetworkLocalPlayer Active { get; private set; }

        WebNetworkIdentity identity;

        void Awake()
        {
            identity = GetComponent<WebNetworkIdentity>();
        }

        void OnEnable()
        {
            if (Active != null && Active != this)
                Debug.LogError("场景中存在多个启用的 WebNetworkLocalPlayer。", this);
            Active = this;
            Register();
        }

        void Start()
        {
            Register();
        }

        void OnDisable()
        {
            WebNetworkManager.Instance?.ClearLocalPlayer(identity);
            if (Active == this)
                Active = null;
        }

        public void Register()
        {
            if (identity == null)
                identity = GetComponent<WebNetworkIdentity>();
            WebNetworkManager.Instance?.SetLocalPlayer(identity);
        }
    }
}
