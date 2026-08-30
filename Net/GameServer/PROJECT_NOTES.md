# GameServer Project Notes

## 2026-08-27 按用户要求暂时开放 ID 登录

- 主配置 `appsettings.json` 显式设置 `Authentication:AllowInsecureDevelopmentLogin=true`，不限 Development 环境。此项覆盖下方历史记录中的“正式服默认关闭”部署约定。
- 未指定微信登录类型的客户端可直接使用玩家 ID 登录；微信 code/会话凭证登录分支仍执行原有验证。
- 该入口不证明账号归属，按用户明确要求暂时放开。需更新部署端配置并重启服务才生效；恢复限制时将该项设为 false。

## 2026-08-27 StartScene 大厅进度卡死修复

- 根因：正式服安全加固默认关闭 ID 登录，但编辑器仍发送 ID；Development 配置现显式开启测试登录，Production 默认仍关闭。不得把 Development 配置部署为正式配置。
- 客户端识别登录错误码并触发 OnLoginFailed；StartScene 同时监听登录失败与 RoomError，恢复服务器选择并展示错误，不再停在 80%/100%。
- StartScene 进度到 100% 的收尾只执行一次，避免空 LoadScene 方法被每帧重复调用。

## 2026-08-27 登录锁与只读查询等待优化

- 64 段登录锁替换为真正按账号隔离的 `AccountLoginGate`，无持有者和等待者时移除条目；等待取消、重复释放受保护，不同账号不再因哈希碰撞互相等待。
- 排行榜、好友列表、服务器列表、公告查询进入每连接单独串行的 `SerialQueryQueue`（最多 8 个任务 / 2MiB 请求），实时消息和房间操作不等待这些查询。队满返回 QUERY_QUEUE_FULL，不无限创建后台任务。
- Login/Logout 在切换身份前等待旧查询完成；队列执行时校验账号、区服及连接是否仍有效。奖励、背包、邮件领取等写操作保留原处理顺序，数据库变慢时仍可能阻塞该连接，不能直接并发。
- 服务器入站 protobuf 只解析一次，路由和业务层复用同一个消息。新增限频 `[HandlerPerf]` 记录超过 500ms 的业务处理，和 `[SendQueue]` 区分业务等待/网络等待。
- 本轮不强行中断 Addressables 场景加载或正在发送的可靠包，也不提前释放仍在保存的数据库任务名额；这些等待仍保留以避免串场景、消息乱序或数据丢失。
- 验证：ConcurrencyChecks 覆盖账号隔离、同账号互斥、取消、锁复用、查询 FIFO/容量/字节预算/身份切换屏障及异常恢复；仍需部署后真实负载验证。

## 2026-08-27 网络全链路回归修正

- 客户端异步对象生成使用场景代次和对象 ticket，切图/销毁后迟到的实例回收，不注册到新场景；异步请求先清空字典再取消，避免同步 continuation 修改枚举。
- 网络消息/断线监听异常隔离，消息业务异常不阻断请求完成，断线业务异常不阻断重连调度；文本消息不按 protobuf 解析。
- Addressables 加载校验真实状态并通知失败；加载中收到新场景请求时，旧加载完成后执行最新请求，不重叠加载、不用旧加载确认新房间。60 秒仅告警，不能安全取消的底层场景操作仍等待结束，持续无响应需重新进入小游戏。
- 同账号登录使用固定 64 个分段异步锁串行化，账号索引移除改成匹配旧值后原子删除。被替换连接立即失去处理新消息资格，关闭码 4001 让新客户端停止自动重连；关闭握手不阻塞新登录。
- 下线保存先取得并发名额才启动任务，超时后等真实保存结束才释放名额；重复待处理离线按账号/区服合并，不静默丢保存。队列仍按不同账号数量增长，并非硬容量上限。
- 安全变更：默认拒绝未认证的玩家 ID 登录。仅独立开发/压测服务器可配置 `Authentication:AllowInsecureDevelopmentLogin=true` 或环境变量 `ALLOW_INSECURE_DEVELOPMENT_LOGIN=true`。此开关会开放 ID 登录，不可在正式微信服开启。客户端与服务器需一起更新以识别替换关闭码。

## 2026-08-27 发送队列回归复查

- 修正上版每次入队依赖 SemaphoreFullException 判断唤醒的高频异常开销，改为生产者锁内检查信号。
- 弱网容量保护先淘汰可丢弃的待发实时状态，保留可靠包 FIFO；仅实时包无法入队时丢帧、不踢连接。淘汰实时包后可靠包仍超出包数/字节预算才隔离连接。
- 修正资源释放等待不完整：接收和发送结束后，还必须等待异步关闭握手结束才能释放发送锁；重复关闭返回同一关闭任务。
- SendQueueChecks 增加弱网拥堵、实时帧让位、可靠顺序和不误断线回归；本地模拟不代表实际网络延迟保证。

## 2026-08-27 统一有界发送与慢连接隔离（覆盖下方旧发送策略）

- `SendMessageAsync/SendBinaryAsync` 的完成语义改为“已入队”，不代表发送完成或对端确认。单连接 `SessionSendQueue` 单写者、可靠 FIFO，所有 RPC、共享值、房间、AOI、生成及初始化快照不再等待其他连接网络 I/O。
- 每连接最多排队 1024 包 / 4MiB（另有一个正在发送的包）；超限或单次写入超过 5 秒则中止连接，不静默丢弃可靠包后继续运行。慢日志 `[SendQueue]` 区分排队与写入耗时，每连接限频。
- 玩家和房间对象 Transform/动画均可合并；动画按 objectId + TrackIndex 区分。合并不跨越可靠包边界，保证生成、状态、销毁的时序；这替代此前的发送锁优先级轮询方案。
- `NetworkRoomState` 改为单路可靠 FIFO，不再同时最新队列和直接广播两遍；不跨 Entered/Load 合并房间状态，避免旧场景状态覆盖新场景。
- 全员切图时服务端清理旧对象但不逐个广播销毁；客户端收到 Load 后统一清理。普通单对象销毁仍可靠通知。
- 同账号重连先中止旧 socket、同步完成权威对象/房间清理，再完成新登录。不再保证旧连接收到踢线提示，确保旧连接失效优先。
- `[RoomPerf]` 的 queued 只表示服务器处理及入队耗时，不能据此排除后续网络排队；需结合 `[SendQueue]` 与客户端加载日志。
- 回归：SendQueueChecks 覆盖可靠 FIFO、可靠包边界、实时合并、单写者、包数/字节上限、发送超时及 50 队列中 1 慢连接隔离；这是本地模拟，不是真实 50 人压测。

## 2026-08-27 房间操作延迟优化

- 创建、匹配、邀请接受和主动加入房间时，先完成权威房间状态迁移并立即返回 `NetworkRoomLoad`；旧大厅的对象销毁及完整成员状态广播改为后台通知，最慢客户端不再阻塞发起者切场景。
- 完成场景就绪后先发送 `NetworkRoomEntered` 和 `NetworkRoomState`，再发送共享值、网络对象及同步变量快照，减少房间 UI 刷新等待。
- 可靠控制消息等待发送时，Transform/动画最新状态发送循环主动让出 session 发送锁，防止高频状态流量延迟房间与场景消息。
- 新增 `[RoomPerf]` 关键路径日志，分别记录创建房间到发送 Load、场景确认到发送 Entered/State 的服务端耗时，用于区分服务器等待和客户端 Addressables 场景加载耗时。
- 大厅离开产生的 `NetworkRoomState` 使用独立的最新控制状态队列：同一房间旧快照会被新快照覆盖，且发送优先于 Transform/动画，避免多人同时操作时积压过时的全量成员列表。

## 2026-08-27 重连超时及大厅恢复

- 真机日志定位到 `CancelReconnect -> CancellationTokenSource.Dispose` 的 wasm `function signature mismatch`，阻断 OnShow 重连；重试作废改为代次校验，不再使用 CTS/Task.Delay。

- 连接 15 秒无回调、登录 30 秒无确认时释放旧 socket 并退避重试；微信取 code 增加 15 秒超时。连接/登录看门狗绑定具体 socket 和代次，旧任务不干扰新连接；只有登录成功才重置重试次数。
- 重试等待使用 Unity PlayerLoop 的 UniTask，创建 socket 的同步异常也进入重试，并关闭废弃的旧连接。
- LoginSuc 即使返回相同大厅也重新加载房间场景，避免旧 CurrentRoom 和成员快照使加入回调被去重；不尝试续接旧战斗。

## 2026-08-27 微信重连复用会话

- 微信首次 code 登录成功后，服务端通过 `__wechat_session` 返回 HMAC-SHA256 签名、24 小时有效的会话凭证；客户端管理器仅在内存缓存凭证和可信 openid。
- 重连提交缓存 openid + 凭证，服务端校验签名、有效期及身份匹配，不再请求微信换取 openid。复用不会延长有效期。
- 凭证失效（包括服务进程重启）通过 `__wechat_session_invalid` 通知客户端清除缓存并重新执行 WX.Login；不接受单独 openid 作为微信登录认证。
- 客户端、服务端需配套更新，建议先更新服务器；旧客户端 code 登录继续兼容。当前凭证使用进程内密钥，不支持跨多个独立服务进程复用。
- 验证：服务端 build 通过（0 警告/错误），微信构建宏下客户端 C# 编译通过；`dotnet run --project Net/GameServer/Tests/WeChatSessionChecks/WeChatSessionChecks.csproj` 的 12 项凭证检查通过。尚需部署后真机验证重连与凭证失效回退。

## 2026-07-17 Server Isolation

- `Msg.ServerId` is fixed on the session after login; an empty value maps to `default`.
- MongoDB player documents and Redis player keys use the `serverId:userId` scope.
- Email queries/caches and rank data are separated by `serverId`.
- Weekly rank rewards preserve the winner's `serverId` on reward mail.
- Duplicate-login kicking is global by `userId`: the account can only have one online connection across all servers.
- A replacement login sends the old session a warning and closes it before returning `LoginSuc` to the new session.
- Matchmaking, invites, AOI and realtime messages stay within one `serverId`.
- A `serverId` is limited to 64 characters and cannot contain `:`.
- Unauthenticated WebSocket sessions no longer have the previous 15-second login timeout.
- `GameMsgType.Logout` saves and unbinds the current character without closing WebSocket; the client may then login to another `serverId`.

## 2026-08-08 Room Shared Values

- `RoomSharedValueManager` stores dynamic integer values by `serverId + roomId + key`.
- Clients submit deltas with `AddRoomSharedValue`; the server atomically adds and broadcasts `RoomSharedValueUpdate` to every player in that room, including the sender.
- Players receive the current room-value snapshot after entering a room. Empty game rooms remove their shared values.
- Any room member can request `ResetRoomSharedValues`; the server resets all existing keys to zero, broadcasts each result, then broadcasts a reset-completed notification.

本文件记录当前服务器结构、关键约定和后续变更记录。以后每次修改代码时，需要同步更新本文件。

## 2026-08-26 实时同步延迟优化

- Transform 和动画广播改为每个接收连接、每个网络对象只保留最新状态。
- 实时状态入队后不再等待所有 WebSocket 实际发送完成，慢客户端不会阻塞发送者的接收循环。
- 登录、生成、销毁、房间状态等可靠控制消息仍使用原有串行发送流程。

## 2026-08-27 Android 后台恢复重连

- 修复前台重连清理 PendingRequests 时同步 continuation 修改正在遍历的字典的问题；增加微信生命周期、重连及服务器登录确认日志，不记录登录 code。

- 补回缺失的 `WeChatPlatformMrg.cs` 和 meta，修复 `LoadScene` 引用缺失引发的 CS0234；同步补齐 `WebNet.ReconnectAfterResume` 接口，微信重新登录统一委托管理器处理。

- Android 从后台回到前台时不再信任旧 WebSocket 的 `Open` 状态，主动废弃旧连接并重新连接。
- 新连接打开后沿用最近一次登录的玩家和区服信息自动登录，恢复服务器会话。
- 微信小游戏使用 `WX.OnHide` / `WX.OnShow` 监听后台切换，不依赖 Unity 在小游戏平台上不稳定的 `OnApplicationPause` 回调。
- 微信恢复前台后若首次连接只触发错误而没有关闭事件，也会继续按退避策略重连。
- 新增仅在微信小游戏构建中创建并跨场景常驻的 `WeChatPlatformMrg`，统一负责 `WX.Login`、`WX.OnHide`、`WX.OnShow` 和重连后的重新登录。
- `LoadScene`、通用 `WebNet` 和网络对象管理器不再各自实现微信 SDK 生命周期及登录逻辑。

## 2026-08-27 Boss 秒杀结算竞态修复

- Boss 死亡监听改为可重放订阅；如果异步绑定前 Boss 已死亡或血量已经归零，会立即执行游戏结算。
- 房主在广播 Boss RPC 前同步派发本地 Boss 事件，确保负责结算的一端先完成死亡监听绑定。
- Boss UI 对重复生成事件和重复死亡通知增加幂等保护，并在关闭时解绑旧对象事件。
- 修正 `RoleGameObject.ExDie` 先设置死亡状态再判断、导致基类死亡流程永远提前返回的问题。

## 2026-08-27 位置同步平滑优化

- 核查后补回被还原的快照缓冲与 `OnError` 退避重连；重连任务退出只清理自身状态，避免覆盖后续重试。

- 远端 Transform 改用带 150ms 缓冲的双快照时间插值，不再快速追到单个目标点后停住等待下一包。
- 快照队列最多保留 6 帧，既吸收微信网络的短时抖动，也限制缓存占用。
- 首帧和长时间缺包时保留柔和收敛逻辑，避免直接跳点。

## 当前状态

### WebSocket

- WebSocket 入口：`GameServer/Server/WebSocketMiddleware.cs`
- 玩家会话：`GameServer/Server/PlayerSession.cs`
- 会话管理：`GameServer/Server/PlayerSessionManager.cs`
- 最大在线连接数：`2000`
- WebSocket keepalive：`30` 秒
- `ws://` 使用 HTTP 端口，默认 `5100`。
- `wss://` 使用 HTTPS 端口，默认 `7100`，必须配置 TLS 证书。
- WSS 证书配置位置：
  - `GameServer/appsettings.json`
  - 或环境变量：`GAME_SERVER_CERT_PATH`、`GAME_SERVER_CERT_PASSWORD`
- 推荐生产方式：使用 Caddy 反向代理。
  - 配置文件：`deploy/Caddyfile`
  - 客户端连接：`wss://你的域名`
  - Caddy 自动申请和续期 HTTPS 证书。
  - GameServer 继续监听：`ws://127.0.0.1:5100`
- 单条消息最大大小：`1 MB`
- 发送有串行锁，避免同一个 WebSocket 并发发送导致异常。
- 连接断开时，如果该 session 已绑定业务玩家 `UserId`，只把下线保存任务放入 `OfflineSaveQueue`，不等待 Redis/MongoDB 操作完成。
- 登录成功后会把 `UserId` 和当前 WebSocket session 绑定；同一个玩家重复登录时，新连接会主动踢掉旧连接，旧连接会先收到 `TIPS` 再关闭。
- 未登录连接超过 `15` 秒没有完成登录会被清理；已登录连接超过 `10` 分钟完全没有消息会被当作僵尸连接清理。
- WebSocket 诊断日志会记录握手 headers、HTTP 请求中断、接收包类型/大小、protobuf 解析结果、登录耗时、发送包和关闭原因。
- 客户端主动发送 Close 帧时，服务端只释放会话，不再强制等待完整 close handshake；服务端主动踢人或超时时才主动发送 Close 帧。

### MongoDB

- 管理类：`GameServer/Server/MongoDBMrg.cs`
- 默认数据库：`GameData`
- 玩家集合：`UserData`
- 邮件集合：`EmailData`
- 排行榜集合：`RankData`
- 每次 MongoDB 操作统一超时：`5` 秒
- MongoDB 负责最终持久化数据。

### Redis

- 管理类：`GameServer/Server/RedisMrg.cs`
- Redis 负责缓存和高频数据：
  - 玩家数据缓存：`player:data:{userId}`
  - 玩家 dirty 标记：`player:dirty:{userId}`
  - dirty 玩家集合：`player:dirty:users`
  - 玩家在线状态：`player:online:{userId}`
  - 在线玩家集合：`player:online:users`
  - 邮件缓存：`email:list:{userId}`
  - 排行榜集合：`rank:{rankName}`
  - 排行榜详情：`rank:data:{rankName}:{userId}`
  - 排行榜名称集合：`rank:names`

### GameDataMrg

- 统一数据入口：`GameServer/Server/GameDataMrg.cs`
- 业务层优先调用 `GameDataMrg`，不要直接判断 MongoDB/Redis。
- 玩家读取：Redis 优先，Redis 没有则回源 MongoDB。
- 玩家保存：Redis 可用时写 Redis 并标记 dirty；Redis 不可用时直接写 MongoDB。
- 玩家下线：把 Redis 玩家数据写回 MongoDB。
- 邮件读取：只从 Redis 读取。
- 邮件缓存：登录时立即刷新一次，后台每 10 秒刷新在线玩家邮件。
- 排行榜更新：同时写 MongoDB 和 Redis。
- 排行榜读取：Redis 优先，Redis 没数据时 MongoDB 兜底。

### 后台服务

- `DirtyUserSaveService`
  - 每 `60` 秒保存 Redis dirty 玩家到 MongoDB。
  - 服务停止时也会保存一次。

- `EmailCacheRefreshService`
  - 每 `10` 秒获取 Redis 在线玩家列表。
  - 从 MongoDB 查询在线玩家邮件，并写入 Redis 邮件缓存。

- `OfflineSaveService`
  - 后台消费 `OfflineSaveQueue`。
  - 玩家断开连接时不阻塞 WebSocket 关闭流程，后台再调用 `GameDataMrg.SetPlayerOffline()` 保存数据。

- `SessionCleanupService`
  - 每 `5` 秒清理已经不是 Open 状态的 WebSocket 会话。
  - 用来兜底处理 WSS/Nginx 下关闭事件延迟导致在线数短时间残留的问题。
  - 同时清理超时未登录连接和长时间无消息的僵尸连接。

- `WeeklyRankClearService`
  - 每周一 `00:00` 执行，也就是周日结束的午夜。
  - 清榜前先给所有排行榜前 `100` 名发放邮件奖励。
  - 发奖后清除 MongoDB 和 Redis 中所有排行榜相关数据。

### 邮件

- 邮件永久数据写 MongoDB。
- 客户端获取邮件时只读 Redis。
- 新增邮件后会清掉该玩家 Redis 邮件缓存，等待下一轮刷新或登录刷新。
- 排行榜奖励邮件：
  - 只发每个排行榜前 `100` 名。
  - `ItemId` 格式：`weekly_rank_reward:{rankName}:{rankIndex}`
  - 奖励数量：
    - 第 1 名：`1000`
    - 第 2-3 名：`800`
    - 第 4-10 名：`500`
    - 第 11-50 名：`300`
    - 第 51-100 名：`200`

### 排行榜

- 排行榜名由 `rankName` 表示，例如：`fightingCapacity`。
- MongoDB 中排行榜是每个玩家一条 `MongoRankData`，不是一个榜单数组。
- 发送给客户端时组装成 `Msg.RankData` repeated 数组。
- 战斗力字段：`RankData.UserData.FightingCapacity`
- `FightingCapacity` 是超大数字字符串，不转 `int/long/double`。
- 排序规则：先比数字字符串长度，再按字符串字典序比较，从大到小。

## 变更记录

### 2026-07-08

- 加固 WebSocket：
  - 调整 keepalive 为 30 秒。
  - 增加发送锁、关闭超时、消息大小限制、分片拼包。
  - 增加连接上限和广播并发限制。

- 加固 MongoDB：
  - 增加统一超时。
  - 增加通用增删改查方法。
  - 登录改为 `FindOneAndUpdate + Upsert`。
  - 增加邮件索引和排行榜索引。

- 增加 Redis：
  - 新增 `RedisMrg`。
  - 支持玩家缓存、dirty 标记、在线状态、邮件缓存、排行榜缓存。

- 增加统一数据入口：
  - 新增 `GameDataMrg`。
  - 统一判断 Redis/MongoDB 使用场景。

- 增加自动保存：
  - 玩家下线自动保存 Redis 玩家数据到 MongoDB。
  - 服务器退出/异常时抢救保存 dirty 玩家。
  - 每 60 秒定时保存 dirty 玩家。

- 增加排行榜：
  - 排行榜使用 `RankData`。
  - 按 `UserData.FightingCapacity` 排序。
  - 排行榜双写 Redis 和 MongoDB。
  - 每周清除所有排行榜数据。

- 增加排行榜奖励：
  - 每周清榜前给每个排行榜前 100 名发邮件奖励。

- 优化邮件：
  - 邮件读取改为只读 Redis。
  - 登录时刷新玩家邮件缓存。
  - 在线玩家每 10 秒从 MongoDB 刷新邮件到 Redis。

- 增加 WSS 配置入口：
  - Kestrel 默认监听 HTTP `5100`。
  - 配置证书后监听 HTTPS `7100`，客户端可使用 `wss://域名:7100`。

- 增加 Caddy 反向代理模板：
  - 新增 `deploy/Caddyfile`。
  - 推荐用 Caddy 对外提供 `wss://域名`，GameServer 内部继续使用 `ws://127.0.0.1:5100`。
  - 这样不需要在 C# 项目里管理证书，Caddy 会自动续期。

### 2026-07-09

- 优化 WebSocket 断开重连稳定性：
  - 新增 `OfflineSaveQueue`。
  - 新增 `OfflineSaveService`。
  - 玩家断线时只入队下线保存，不再等待 Redis/MongoDB 操作完成。
  - 减少主动断开后短时间无法重连的概率。
  - 新增 `SessionCleanupService`。
  - 客户端发送 Close 后，服务端先释放 Session，再后台尝试关闭 socket。
  - 新增同账号重连保护：新连接登录成功后会关闭旧连接，旧连接会先收到 `TIPS` 消息。
  - 新增连接活跃时间记录：未登录连接 `15` 秒超时，已登录连接 `10` 分钟无消息超时。
  - 新增登录成功日志和连接超时清理原因日志，便于区分是未登录测试连接还是已登录僵尸连接。
  - 下线保存增加 `2` 秒重连缓冲；如果同账号已经有新连接在线，只保存数据，不清 Redis 在线状态。
  - 增加 WebSocket 全链路诊断日志：握手、请求中断、接收片段、完整消息、protobuf 解析、登录数据读取、发送响应、关闭流程。
  - 调整客户端主动关闭处理：收到客户端 Close 后直接释放会话，减少频繁测试断开时的 close handshake/reset 噪音日志。

- 增加线上 WSS 排查能力：
  - 新增 `/health` 健康检查，返回服务状态和当前在线数。
  - WebSocket 接入日志增加 path、remote IP、forwarded headers、在线数和连接时长。
