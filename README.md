# UnityFrameWork 框架使用说明

更新日期：2026-10-28。本文依据当前源码编写，适用于独立框架项目及已同步的 Game 项目。

框架源项目：`C:\Svn\Game\UnityFrameWork`。业务项目：`C:\Svn\Game`。

## 1. 框架组成

框架使用 Unity 2022.3.62f3，主要由以下部分组成：

| 模块 | 作用 |
| --- | --- |
| YooAsset | 资源收集、AssetBundle 构建、版本清单、下载与加载 |
| HybridCLR | IL2CPP 平台加载热更新 C# DLL、补充 AOT 元数据 |
| UniTask | 异步初始化、资源加载和生命周期任务 |
| Actor / UiActor | 将预制体封装成可管理的业务对象 |
| UiManager | UI 根节点、窗口打开关闭、层级和提示接入 |
| 配置表工具 | 从 XLSX 生成 C# 类型、查询代码及运行时数据 |
| WebNet | WebSocket 连接、消息、房间、RPC、对象同步及语音 |
| 编辑器发布工具 | 客户端基线检查、热更新 DLL 和资源发布 |

当前资源后端是 **YooAsset**。`ABMrg` 名称作为原调用入口保留，并不表示仍在使用 Addressables。Game 项目中的旧 Addressables 包、配置及扩展已移除。

## 2. 目录结构

```text
Assets/FrameWork/
├─ HotUpdate.asmdef                 # 框架业务和游戏代码所属程序集
├─ Asset/                           # YooAsset 管理的业务资源
│  ├─ Base/ConfigData.asset          # 业务版本、服务器地址等配置
│  ├─ Dll/                          # 构建工具生成的 DLL.bytes
│  ├─ Ui/                           # UI 预制体
│  ├─ Scene/                        # 业务场景（Game 项目使用）
│  └─ Xlsx/                         # 生成的配置表数据
├─ Editor/                          # 生成代码、收集资源和发布工具
├─ Generated/HybridCLR/             # 生成的 link.xml、AOT 泛型引用等
├─ Networking/Protocol/             # 共享网络协议
├─ Plugins/                         # 框架依赖；Game 中部分插件保留原目录
├─ Resources/                       # 安装包内的启动和资源系统配置
│  ├─ FrameworkBootSettings.asset
│  └─ YooResourceSettings.asset
├─ Scenes/Boot.unity                # 安装包启动场景
├─ Scripts/
│  ├─ GameEntry.cs                  # 热更新业务入口
│  ├─ PrefabScript/                 # 预制体生成代码与业务扩展
│  └─ Xlsx/                         # 配置表生成代码
├─ Tool/
│  ├─ Bootstrap/                    # AOT 启动壳
│  │  ├─ FrameWork.Bootstrap.asmdef
│  │  ├─ FrameworkLauncher.cs
│  │  ├─ AbMrg.cs
│  │  └─ FrameworkBootSettings.cs
│  ├─ Base/                         # Actor、UiActor 等
│  ├─ UiTool/                       # UI 管理
│  ├─ ObjectPool/                   # 对象池
│  └─ WebSocket/                    # 网络运行时
├─ Xlsx/                            # XLSX 源表格
└─ YooAssetSettings/                # 资源收集配置
```

Game 项目有自己的业务目录、SDK 和资源，不要求将 Assets 下所有内容移入 FrameWork。移动已有脚本或资源时，应同时保留对应 `.meta`，避免场景和预制体引用丢失。

## 3. 程序集与热更新边界

当前采用两个主要运行时程序集：

| 程序集 | 内容 | 修改后的发布方式 |
| --- | --- | --- |
| `FrameWork.Bootstrap` | 启动器、资源加载器、启动配置和资源系统配置类型 | 重新准备客户端基线并构建安装包 |
| `HotUpdate` | Actor、UI、网络等框架业务代码，以及游戏逻辑 | 在基线兼容时发布热更新 |

`FrameWork.Editor` 仅供编辑器使用。旧的 `FrameWork.Runtime.asmdef` 和 `Scripts/HotUpdate.asmdef` 已被当前布局替代；HotUpdate 定义位于 FrameWork 根目录。

依赖方向为：

```text
FrameWork.Editor ──→ HotUpdate ──→ FrameWork.Bootstrap
                          两者按需引用独立插件程序集
```

启动壳不能直接引用 HotUpdate 中的业务类型。启动场景也不能提前挂载或引用热更新脚本及其业务预制体。业务对象应在 DLL 加载完成后创建。

“可以热更新”不代表任何修改都可以沿用旧安装包。启动代码、AOT 插件、安装包内 Resources、原生库及构建配置等变化，需要由基线检查判断是否重建客户端。

## 4. 启动流程

```text
Boot.unity
  → FrameworkLauncher
  → ABMrg.Init：初始化 YooAsset、获取版本并加载资源清单
  → 按启动配置下载 startup 标签资源
  → IL2CPP Player 补充当前客户端对应的 AOT 元数据
  → 按依赖顺序加载热更新 DLL
  → Player 中补充执行热更新程序集的启动回调
  → 调用 HotUpdate.GameEntry.Start
  → 初始化业务配置、UI、网络接入及首个业务场景
```

编辑器的 `useEditorAssemblies` 可以使用已编译的编辑器程序集，便于调试；这不能替代真实 Player 的 DLL 加载与 AOT 验证。

Game 项目的入口保留了游戏接入逻辑，启动顺序为 `Boot → HotUpdate → LoadScene → StartScene`。独立框架中的入口仅提供基础示例。

## 5. 首次配置与编辑器运行

1. 使用项目指定的 Unity 版本打开项目，等待包解析和编译完成。
2. 选择实际发布平台。微信小游戏使用 WebGL，并检查微信 SDK 的配置。
3. 执行 `FrameWork/初始化抽离框架`，生成或更新资源、HybridCLR 和启动场景配置。
4. 查看 `FrameworkBootSettings.asset`，编辑器调试时按需开启 `useEditorAssemblies`。
5. 查看 `YooResourceSettings.asset`，本地调试可使用编辑器模拟模式。
6. 执行 `FrameWork/打开启动场景`，再运行游戏。

初始化菜单会调整启动场景列表及部分构建设置。Game 项目中的业务场景通过 YooAsset 加载，当前安装包场景列表只保留 Boot。

## 6. 配置文件

### 6.1 FrameworkBootSettings

位置：`Assets/FrameWork/Resources/FrameworkBootSettings.asset`。

| 字段 | 含义 |
| --- | --- |
| `downloadBeforeStart` | 是否在业务启动前下载 startup 标签资源 |
| `useEditorAssemblies` | 编辑器是否优先使用当前已编译程序集 |
| `clientBuildId` | 客户端基线标识，由发布流程维护 |
| `hotUpdateDlls` | 按依赖顺序加载的 DLL 名称 |
| `aotMetadataDlls` | 当前客户端对应的 AOT 元数据 DLL |
| `entryAssembly` / `entryType` / `entryMethod` | 热更新入口，默认 HotUpdate / HotUpdate.GameEntry / Start |

不要将其他平台、其他客户端基线或其他构建版本的 AOT DLL 混入当前资源包。

### 6.2 YooResourceSettings

位置：`Assets/FrameWork/Resources/YooResourceSettings.asset`。

- `packageName`：资源包名称，默认 `DefaultPackage`。
- `editorSimulate`：编辑器是否使用模拟资源构建。
- `playerMode`：`Offline` 为离线模式，`Host` 为远端资源模式。
- `remoteUrl` / `fallbackRemoteUrl`：正式环境资源地址。
- `testRemoteUrl` / `testFallbackRemoteUrl`：定义 `FRAMEWORK_TEST` 时使用的测试资源地址。
- URL 支持 `{platform}`、`{client}`、`{package}` 占位符。
- `packageVersion`：固定资源版本；留空时向文件系统请求版本。
- `timeoutSeconds`、`downloadConcurrency`、`downloadRetryCount`：请求及下载参数。

测试模式下测试 URL 留空会报错，不会自动改用正式 URL。资源 URL 与游戏 WebSocket 服务器地址是两套配置。

### 6.3 ConfigData

位置：`Assets/FrameWork/Asset/Base/ConfigData.asset`，通过 YooAsset 加载。

当前类型包含业务版本 `versions`、服务端兼容版本 `serverVersion`、正式服地址 `serverIp` 和调试服地址 `debugServerIp`。

业务入口应先执行：

```csharp
await Config.InitializeAsync();
```

完成后再读取 `Config.ServerIp` 等属性。不要在静态字段初始化时提前读取尚未加载的配置。

## 7. 资源收集与加载

将资源放到 `Assets/FrameWork/Asset` 的业务子目录中，避免直接散放在 Asset 根目录。执行 `FrameWork/YooAsset/配置资源收集`，或通过兼容入口 `FrameWork/更新资源` 更新收集配置。

工具按一级目录补充收集器，默认使用目录打包规则；已有收集器会按 GUID 跟随移动或重命名，并保留已有打包规则和标签。`Dll`、`Ui`、`Xlsx`、`Base` 默认标记为 `startup`。

### 7.1 异步加载与释放

以下示例放在已初始化框架的异步业务方法中；资源名替换为项目实际地址：

```csharp
var prefab = await ABMrg.LoadAsync<UnityEngine.GameObject>("MyPrefab");
var icon = await ABMrg.LoadAsync<UnityEngine.Sprite>("MyIcon");

// 确定没有使用者需要此缓存资源后，再释放对应缓存句柄。
ABMrg.ReleaseAsset<UnityEngine.Sprite>("MyIcon");
```

`LoadAsync` 会缓存资源句柄。实例化对象建议使用专门的接口：

```csharp
var instance = await ABMrg.InstantiateAsync("MyPrefab", parent);

// 该实例独立持有资源句柄，销毁时由挂载的 lease 释放。
ABMrg.ReleaseInstantiate(instance);
```

不要把普通资源缓存、实例自身持有的句柄和对象池回收混为一件事。对象池回收通常只是停用或重新入池，并不代表资源已经销毁。

### 7.2 WebGL 同步加载限制

WebGL Player 不支持用同步等待阻塞主线程。优先使用 `LoadAsync`；需要调用 `Load<T>` 的代码，应先异步预加载同类型、同地址的资源。

```csharp
await ABMrg.PreloadAsync<UnityEngine.Sprite>(new[] { "IconA", "IconB" });
var icon = ABMrg.Load<UnityEngine.Sprite>("IconA");
```

### 7.3 场景与下载

```csharp
ABMrg.LoadSceneAsync("StartScene",
    progress: value => UnityEngine.Debug.Log(value),
    call: () => UnityEngine.Debug.Log("场景加载完成"),
    failure: error => UnityEngine.Debug.LogException(error));

await ABMrg.DownloadByTagAsync("startup");
```

可通过 `BeforeSceneLoading`、`SceneLoading`、`SceneLoaded`、`SceneLoadingFinished` 接入加载窗口及业务状态。`UiManager.Init` 会设置切场景前隐藏 UI 的回调，自定义时需注意不要误覆盖该行为。

### 7.4 Game 项目的重名资源兼容

独立框架默认按文件名生成地址。Game 项目额外保留 `GameResourceAddressRule` 和 `GameResourceAliasTable.json`，以处理同名装饰预制体，以及同名音效、图标和特效。

兼容表由 Game 项目的资源配置工具生成，不需要手填。新增或移动重名资源后重新配置资源收集。相同名称、相同类型的多个资源仍有歧义，业务代码应使用明确的完整资源路径。

## 8. Actor 与 UI

### 8.1 预制体代码生成

选择预制体后执行右键菜单 `FrameWork/预制体/生成代码`。生成路径为 `Assets/FrameWork/Scripts/PrefabScript`。

带 RectTransform 的预制体生成 UiActor 类型，普通预制体使用 Actor。生成代码通常拆为 `.H`、`.Awake`、`.Attr`、`.Extend`、`.Fun` 等部分；优先将手写业务放在 `.Fun` 或独立 partial 文件中，重新生成前检查工具会覆盖哪些文件。

需要绑定的子节点可使用 `AddScripts` 标记，再重新生成组件引用。

### 8.2 等待 Actor 初始化

Actor 构造时会启动异步实例化，`new` 返回并不代表 GameObject 已经准备好。

```csharp
var actor = new MyGeneratedActor(); // 项目中已生成的 Actor 类型
await actor.Initialization;
actor.GetGameObject().SetActive(true);
```

初始化失败会反映到 `Initialization` 任务。需要可靠地等待创建结果时，应等待该任务，而不是一直轮询 GameObject 是否非空。

### 8.3 UI 根节点及窗口

在业务入口中初始化生成的 UI 根节点：

```csharp
await UiManager.InitAsync(new UiRoot());

var window = await UiManager.OpenUi<MyWindow>();
UiManager.HideUi<MyWindow>();   // 隐藏，保留窗口对象
UiManager.RemoveUi<MyWindow>(); // 移除窗口
```

示例中的 UiRoot、MyWindow 必须是项目实际生成的类型，UiRoot 应包含项目所需的层级和 Camera。

提示与 UI 特效由业务接入：

```csharp
UiManager.TipsHandler = ShowTipsAsync;
UiManager.UiFxHandler = ShowUiFxAsync;
```

两个处理方法分别返回 `UniTask`，参数分别为 `(string text)` 和 `(string effectName, float seconds)`。未设置处理器时调用 `ShowTips` 或 `ShowUiFx` 会抛出异常。

## 9. 配置表

| 内容 | 路径 |
| --- | --- |
| XLSX 源文件 | `Assets/FrameWork/Xlsx` |
| 运行时数据 | `Assets/FrameWork/Asset/Xlsx` |
| 生成代码 | `Assets/FrameWork/Scripts/Xlsx` |

表格基本格式为第一行描述、第二行类型、第三行字段名，第四行开始是数据。执行 `FrameWork/配置表/生成所有配置表代码` 生成类型、查询代码和数据索引。

在业务管理器和查询类的静态初始化触发前，先预加载配置表：

```csharp
await FrameWork.Tool.LoadAllXlsx();
```

随后使用生成的 `Xlsx_表名_Query` 查询。具体字段、枚举键和查询维度以当前生成代码为准。修改表格结构并重新生成代码后，需要同时发布匹配的 HotUpdate DLL 与配置数据。

## 10. 网络接入

连接入口是 `FrameWork.WebSocket.WebNet`，配置初始化完成后可按业务需要调用 `Connect()` 或 `Connect(address)`。断开使用 `Disconnect()`。

主要组件：

| 类型 | 作用 |
| --- | --- |
| WebNetworkRoomManager | 房间进入、退出、场景切换等 |
| WebNetworkManager | 网络对象创建、注册和回收 |
| WebNetworkIdentity | 网络对象及控制权身份 |
| WebNetworkBehaviour | 网络行为基类 |
| WebNetworkTransform | 位置等变换同步 |
| WebNetworkAnimator / WebNetworkSpineAnimator | 动画同步 |
| WebNetworkSyncVars / WebSyncVar | 同步变量 |
| WebNetworkVoiceManager | 语音采集、编码和播放 |

`WebClientRpc` 用于房间对象 RPC。被标记的方法必须返回 void，参数类型应在当前 RPC 分发器支持范围内；编译后的 Weaver 自动注入发送逻辑。`IncludeSender=false` 为本端先执行、转发给其他端；true 为本端也等待服务端回包执行。

业务规则通过 `WebNetworkIntegration` 接入，包括错误提示、首次登录的新手房间、地图准备、生成物配置、Spine 动画抑制及父节点解析。框架不会自动推断项目的关卡、掉落、UI 或布局规则。

Game 项目的 GameEntry 已配置这些业务钩子。不要使用独立框架的示例入口直接覆盖它。协议及服务端有变更时，需要另外确认客户端、服务端版本兼容；同步 Unity 框架不等于同步服务端。

## 11. 打包与发布

### 11.1 首次发布或原生客户端变更

1. 切换正确的平台、环境和 SDK 配置。
2. 检查服务器地址、资源 URL、包名及启动入口。
3. 执行 `FrameWork/发布/准备新的客户端基线（含 AOT）`。
4. 等待本地解释器安装、脚本编译、HybridCLR 代码生成、AOT 元数据整理和资源构建完成。
5. 使用对应平台的构建流程生成安装包；微信小游戏继续使用微信 SDK 导出流程。
6. 在真实 Player 中验证 DLL 加载、资源下载、登录和主要业务流程。

准备流程会检查是否能复用现有基线。需要明确重新生成时，使用 `FrameWork/发布/强制重建客户端基线（含 AOT）`。

**准备客户端基线并不等于已经生成并发布最终安装包。** 构建过程中应先处理 Console 中最早的错误，不要只看后续 DLL 缺失提示。

### 11.2 基线兼容的热更新

业务或资源修改后执行 `FrameWork/发布/发布热更新版本（DLL 和资源）`。工具会检查客户端基线、重新编译和复制热更新 DLL，并构建发布资源。

当前发布主目录结构为：

```text
Publish/<平台>/<客户端基线ID>/<资源包名>/
```

发布流程还保存历史版本及差异清单，避免重复发布同一资源版本。资源与清单先写入，版本指针后更新。

生成本地 Publish 目录不代表已经上传 CDN。部署时应保持目录结构，并使配置中的远端 URL 指向实际资源目录。旧 Addressables 的 catalog 和 bundle 不能直接用作 YooAsset 发布资源。

### 11.3 哪些修改需要新客户端

| 修改 | 处理方式 |
| --- | --- |
| HotUpdate 内业务代码、动态配置或资源 | 基线兼容时可发布热更新 |
| Bootstrap、AOT 插件或原生库 | 重新准备基线并构建客户端 |
| 嵌入安装包的 Resources、启动场景或构建设置 | 检查基线；输入变化时重建客户端 |
| 新平台或不同客户端版本的 AOT 元数据 | 使用对应平台和对应基线重新生成 |
| 引入原生接口、调整插件程序集边界 | 单独确认兼容性，不默认当作纯业务热更 |

## 12. 验证与常见问题

可执行 `FrameWork/验证/检查热更新边界`，检查启动壳、业务程序集依赖及启动场景。Game 项目另有 `FrameWork/验证/编译当前平台 Player 脚本` 工具。

编辑器运行通过，只能说明编辑器路径正常；正式发布仍需检查 WebGL/IL2CPP 的脚本编译、原生构建和真实运行。

| 现象 | 检查方式 |
| --- | --- |
| 缺少 `Data~/hybridclr_version.json` | 检查 HybridCLR 对应版本的完整 Data~ 目录；同时保留 NetStandard 参考程序集 |
| Player 编译找不到 `WX` | 检查微信 SDK 的程序集引用和平台条件；Game 已使用 SDK asmdef GUID 引用 |
| Duplicate references: Wx / WxEditor | 同一 SDK 不要同时保留名称和 GUID 两种引用 |
| 找不到 HotUpdate.dll | 先修复最早的 Player 编译错误，确认当前平台 DLL 实际生成 |
| 提示 Config 未初始化 | 在业务代码访问配置前等待 `Config.InitializeAsync()` |
| UI 或 Actor 的 GameObject 为空 | 等待 `Initialization` 或 `UiManager.InitAsync`，并检查真实初始化异常 |
| WebGL 同步加载异常 | 改用异步加载，或先预加载同类型资源 |
| YooAsset 地址重复 | 处理同名资源；Game 使用项目专用地址规则和兼容表 |
| 发布提示客户端输入已变化 | 重新准备客户端基线，不要直接沿用旧 AOT 元数据 |
| 修改框架后游戏画面没有变化 | 程序集、异步初始化、资源和发布检查等底层更新不会自动改变业务界面 |

## 13. 维护约定

- 修改生成代码前区分自动生成部分与手写 partial 文件。
- 配置表数据、生成代码和对应 DLL 应保持版本一致。
- DLL、AOT 元数据、资源清单和客户端基线应成套管理，不手工混用历史输出。
- Game 项目的服务器配置、业务入口和兼容代码不能直接被框架示例覆盖。
- 框架差异同步后，至少检查编辑器编译、目标平台 Player 编译、热更新边界及启动流程。
- `Library`、`Temp` 和验证输出属于生成内容；资源和脚本的 `.meta` 属于引用身份，应正确保留。

Game 项目的历史迁移说明见根目录 `FRAMEWORK_MIGRATION.md`，最近一次同步说明见 `FRAMEWORK_SYNC_20261028.md`。当前程序集结构以本文及最新源码为准。
