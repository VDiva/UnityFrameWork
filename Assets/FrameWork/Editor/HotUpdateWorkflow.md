# 业务与资源热更新

## 程序集边界

- `FrameWork.Bootstrap`：安装包中的固定启动壳，包括启动器、资源初始化/下载、AOT 元数据和 DLL 加载，以及启动进度界面。只能引用 AOT 程序集。
- `HotUpdate`：唯一的项目业务热更新程序集，定义文件位于 `Assets/FrameWork/HotUpdate.asmdef`。同时包含 Tool 下的 UI、网络、对象池、公共业务与 Scripts 下的游戏代码；不再分为 FrameWork.Runtime 与 HotUpdate 两份 DLL。Bootstrap、Editor 和自带 asmdef 的第三方目录仍由各自程序集管理。
- Unity、UniTask、YooAsset、HybridCLR、已有独立程序集的第三方库和平台 SDK 仍为 AOT。其原生代码与启动依赖不能通过业务 DLL 替换。

## 第一次迁移

这次调整改变了程序集边界，必须准备一次新的客户端基线并重新构建安装包。原先内置 FrameWork.Runtime 或按两个业务 DLL 加载的旧客户端，不能直接使用合并后的发布文件。

1. 等待 Unity 编译完成，执行 `FrameWork/验证/检查热更新边界`。
2. 执行 `FrameWork/发布/准备新的客户端基线（含 AOT）`。
3. 构建并分发新的客户端。Player 场景列表只保留启动场景，业务场景通过 YooAsset 加载。

## 日常更新

Host 模式为纯远程资源模式：不复制 startup 内置包，不向 Player 的 StreamingAssets 暂存 YooAsset 资源；DLL、AOT 元数据和业务资源均从服务器资源包下载（下载后可使用缓存）。发布前必须上传完整资源包及版本清单。Offline 模式仍复制全部内置资源。

不要手工删除导出目录中的分包而保留 game.json 声明。抖音导出的 StreamingAssets 分包应由转换器按实际构建内容生成；仅在确认目录不存在且游戏不再依赖该分包时，才能同步移除其声明。

修改 Tool（Bootstrap 目录除外）、Scripts、Asset 内的业务代码/资源后，直接执行 `FrameWork/发布/发布热更新版本（DLL 和资源）`。保持客户端 ID，上传同一客户端目录下的新资源版本，版本指针最后上传。

`ConfigData.asset` 已保留 GUID 迁到 `Assets/FrameWork/Asset/Base/ConfigData.asset`，不再从 Resources 加载。热更新入口应在启动 UI、网络前先 `await Config.InitializeAsync()`。Base 资源应设置 startup 标签，在首包和启动下载中可用。

启动资源地址、包名等最初下载所必需的配置仍位于 Resources/YooResourceSettings.asset；FrameworkBootSettings 也属于启动壳。修改这些配置仍需新安装包。

## 仍需新客户端的改动

必须关闭 Player Settings 的 Strip Engine Code。客户端只包含启动场景，业务资源包内的碰撞体等引擎组件不能依赖启动场景推导保留。配置菜单自动关闭该选项，构建前再次检查。修改此选项后需要重新准备基线和构建安装包，替换热更 DLL 无法补回旧包中已裁剪的原生引擎代码。

启动壳、启动场景、程序集结构、AOT 第三方库、原生插件、Unity 版本、包依赖、平台和编译设置变化时，重新准备基线。热更新新增使用了旧客户端已裁剪的 AOT 类型/方法、原生入口或缺少的桥接签名时，也需要调整保留配置并重新构建客户端。业务热更新不等于任意替换 Unity 或原生 SDK。

准备普通基线会在客户端输入未变时复用 AOT；强制重建始终生成新的 AOT 和客户端 ID。日志显示失效输入及各阶段耗时。

验证报告位于 `Library/FrameWork/Validation/HotUpdateLayout.json`。
