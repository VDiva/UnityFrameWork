using UnityEngine;

namespace FrameWork.Script.WebNet
{
    /// <summary>根据服务端分配的 AI Authority 自动启用或关闭本地 AI 决策脚本。</summary>
    [RequireComponent(typeof(WebNetworkIdentity))]
    public sealed class WebNetworkAIAuthority : WebNetworkBehaviour
    {
        [Tooltip("只放 AI 决策、寻路和攻击脚本；不要放网络同步组件。")]
        [SerializeField] Behaviour[] aiBehaviours;

        void OnEnable()
        {
            WebNetworkManager.AuthorityChanged += OnAuthorityChanged;
            Refresh();
        }

        void Start()
        {
            Refresh();
        }

        void OnDisable()
        {
            WebNetworkManager.AuthorityChanged -= OnAuthorityChanged;
        }

        void OnAuthorityChanged(WebNetworkIdentity changed)
        {
            if (changed == Identity) Refresh();
        }

        void Refresh()
        {
            bool shouldRun = Identity.IsAiObject && HasAuthority;
            if (aiBehaviours == null) return;
            foreach (Behaviour behaviour in aiBehaviours)
                if (behaviour != null)
                    behaviour.enabled = shouldRun;
        }
    }
}
