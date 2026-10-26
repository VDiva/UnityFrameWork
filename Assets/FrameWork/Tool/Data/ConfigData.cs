using UnityEngine;

namespace FrameWork
{
    [CreateAssetMenu(fileName = "ConfigData", menuName = "FrameWork/CreateConfigData")]
    public class ConfigData : ScriptableObject
    {
        [Tooltip("随客户端发布的版本；资源版本由 YooAsset 独立管理。")]
        public string versions = "1.0.0";
        [Tooltip("非负整数服务器兼容版本，低于服务端要求时提示更新客户端。")]
        public string serverVersion = "1008";
        [Tooltip("正式服 WebSocket 地址。")]
        public string serverIp = "ws://127.0.0.1:5100";
        [Tooltip("调试服 WebSocket 地址。")]
        public string debugServerIp = "ws://127.0.0.1:5100";
    }
}
