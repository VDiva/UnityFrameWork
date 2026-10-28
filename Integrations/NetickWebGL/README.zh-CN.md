# Netick WebGL 接入核对

已核对官方 NetickUnityTransport，版本提交 `2e8460ea240321c60553793a702a3f03b510d8a7`。

结论：该传输适配器已经支持 WebGL 客户端，不需要再实现浏览器 WebSocket 传输。

- 仓库：https://github.com/NetickNetworking/NetickUnityTransport
- package.json：Unity 2022.3，依赖 Unity Transport 2.4.0；README 要求 Netick 2。
- `UnityTransportProvider.GetClientNetworkProtocol()` 在 `UNITY_WEBGL && !UNITY_EDITOR` 下将 Auto 解析为 WebSocket，其他平台解析为 UDP。
- `ConstructDriverWS()` 使用 Unity Transport 的 WebSocketNetworkInterface。
- 同时启用 UDP 和 WebSocket 时，服务端先绑定 UDP，再绑定 WebSocket，端口按启用顺序递增。例如基准 7777 对应 UDP 7777 和 WebSocket 7778。
- 示例 Provider asset 的客户端和服务器协议初值均为 None，必须配置后再启动。

## 配置方式

1. 在目标 Netick 项目中安装 Netick 和本目录依赖片段中的官方适配器。片段只用于合并 dependencies，不能覆盖整个 manifest。
2. 创建 `Create > Netick > Transport > UnityTransportProvider`。
3. 最简单的浏览器验证：ClientProtocol 选择 WebSocket，ServerProtocol 只启用 WebSocket。两端使用同一端口。
4. 若同时服务原生 UDP 客户端：ClientProtocol 选择 Auto，服务器启用 UDP 与 WebSocket；浏览器应连接 WebSocket 的实际端口，不要直接沿用 UDP 基准端口。
5. HTTPS 页面应配置 WSS，按官方 Secure Connections 配置证书和服务端 TLS；浏览器 WebGL 不作为监听服务器。
6. 微信小游戏需要单独验证平台适配；浏览器 WebGL 支持不能直接等同于微信小游戏支持。

## 验证状态

已读取官方包清单和 C# 传输实现，确认以上协议分支及端口行为。尚未安装这些依赖、运行真实 Netick 客户端、构建 WebGL 或验证与当前 Netick 核心 DLL 的兼容性。NetickForUnity 的 README 与包清单版本标记存在差异，实际安装时还需确认所用 Netick 版本。

当前 UnityFramework 主工程及现有 WebSocket 服务未修改。这里提供接入配置，不表示已完成现有房间/对象同步协议到 Netick 的迁移。
