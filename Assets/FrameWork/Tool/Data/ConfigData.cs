using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork
{
    /// <summary>
    /// 非 WebGL 平台（PC / Android / iOS / Editor）的网络传输模式。
    /// WebGL / 微信小游戏平台始终强制使用 WebSocket。
    /// </summary>
    public enum NativeTransportMode
    {
        [LabelText("TCP + KCP 双通道（推荐）")]
        TcpAndKcp = 0,
        [LabelText("仅 TCP")]
        Tcp = 1,
        [LabelText("仅 KCP (UDP)")]
        Kcp = 2,
        [LabelText("WebSocket")]
        WebSocket = 3
    }

    [CreateAssetMenu(fileName = "ConfigData", menuName = "FrameWork/CreateConfigData")]
    public class ConfigData : ScriptableObject
    {
        [Tooltip("业务版本，可随热更新配置发布；安装包兼容性由客户端基线独立管理。")]
        [LabelText("业务版本")] public string versions = "1.0.0";
        [Tooltip("非负整数服务器兼容版本，低于服务端要求时提示更新客户端。")]
        [LabelText("服务端兼容版本")] public string serverVersion = "1008";
        [Tooltip("正式服 WebSocket 地址（WebGL / 微信小游戏使用，并作为原生平台默认主机来源）。")]
        [LabelText("正式服地址")] public string serverIp = "ws://127.0.0.1:5100";
        [Tooltip("调试服 WebSocket 地址（WebGL / 微信小游戏使用，并作为原生平台默认主机来源）。")]
        [LabelText("调试服地址")] public string debugServerIp = "ws://127.0.0.1:5100";

        [Title("原生平台传输配置（WebGL 自动使用 WebSocket）")]
        [Tooltip("非 WebGL 平台默认使用的传输协议（默认 TCP 承载可靠消息 + KCP 承载高频实时同步）。")]
        [LabelText("原生平台传输模式")] public NativeTransportMode nativeTransportMode = NativeTransportMode.TcpAndKcp;
        [Tooltip("原生平台 TCP 端口（当 TCP 地址留空时自动从正式/调试服地址提取主机并拼接此端口）。")]
        [LabelText("TCP 端口")] public int tcpPort = 5101;
        [Tooltip("原生平台 KCP(UDP) 端口（当 KCP 地址留空时自动从正式/调试服地址提取主机并拼接此端口）。")]
        [LabelText("KCP 端口")] public int kcpPort = 5102;
        [Tooltip("可选：显式指定正式服 TCP 地址（如 tcp://127.0.0.1:5101），留空则自动推导。")]
        [LabelText("正式服 TCP 地址(可选)")] public string tcpServerIp = "";
        [Tooltip("可选：显式指定调试服 TCP 地址（如 tcp://127.0.0.1:5101），留空则自动推导。")]
        [LabelText("调试服 TCP 地址(可选)")] public string debugTcpServerIp = "";
        [Tooltip("可选：显式指定正式服 KCP 地址（如 kcp://127.0.0.1:5102），留空则自动推导。")]
        [LabelText("正式服 KCP 地址(可选)")] public string kcpServerIp = "";
        [Tooltip("可选：显式指定调试服 KCP 地址（如 kcp://127.0.0.1:5102），留空则自动推导。")]
        [LabelText("调试服 KCP 地址(可选)")] public string debugKcpServerIp = "";
    }
}
