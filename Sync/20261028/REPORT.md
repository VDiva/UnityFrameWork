# 2026-10-28 框架差异同步

源：`C:\Svn\Game\UnityFrameWork` 当前工作区。

目标：`C:\Svn\Game`。

已同步框架运行时、编辑器工具、资源收集、发布基线检查、对象池、动画、UI、计时器和配置表工具的更新差异。

## 当前程序集结构

- `FrameWork.Bootstrap`：AOT 启动器、资源加载和启动配置。
- `HotUpdate`：框架业务代码及项目游戏代码。定义位于 `Assets/FrameWork/HotUpdate.asmdef`。
- 移除旧 `FrameWork.Runtime` 及 `Scripts/HotUpdate.asmdef` 定义，调整外部脚本的程序集引用。
- 配置迁到 `Assets/FrameWork/Asset/Base/ConfigData.asset`，保留原内容和 GUID，通过 `Config.InitializeAsync()` 加载。
- 安装包场景列表只包含 Boot；业务场景通过 YooAsset 加载。

保留项目业务入口、业务资源、微信 SDK GUID 引用、同名资源按类型加载兼容、登录存档适配和原有服务器地址。未同步 `Net` 服务端，未恢复旧 Addressables 包和资源目录，未复制源框架的预编译 DLL 或配置样例值。

## 验证结果

- Unity 编辑器编译及配置初始化通过。
- WebGL Player 编译通过，生成新的 `HotUpdate.dll` 和 `FrameWork.Bootstrap.dll`。
- 热更新边界检查通过：AOT 无业务程序集反向依赖，Boot 场景不引用业务脚本。
- 启动检查通过：Boot → HotUpdate → LoadScene → StartScene。
- 同名 HuoQiuShu 音效、图标和预制体按类型加载通过。

验证日志位于源项目 `Sync/20261028`；边界检查明细位于目标项目 `Library/FrameWork/Validation/HotUpdateLayout.json`。

未执行正式 Player 构建、服务端登录或完整战斗回归。此次程序集结构已改变，正式发布前必须重新执行“FrameWork/发布/准备新的客户端基线（含 AOT）”，生成当前平台 AOT 元数据和热更新 DLL，并重建 YooAsset 资源。旧的安装包基线不能用于发布此次更新。

## 备份

初始替换及移除文件备份：`FrameworkMigrationBackup/Sync-20261028-1793191012628`。

备份中的 `plan.json` 记录初始同步清单；额外保留了迁移验证工具原路径的备份。此备份针对受影响文件，并非整个项目快照。

本报告中的当前结构取代 `FRAMEWORK_MIGRATION.md` 中 2026-10-26 的历史结构描述。
