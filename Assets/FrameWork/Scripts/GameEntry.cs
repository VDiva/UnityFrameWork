using Cysharp.Threading.Tasks;
using FrameWork;
using UnityEngine;

namespace HotUpdate
{
    /// <summary>热更新业务入口；此程序集不会作为 AOT 代码放进正式安装包。</summary>
    public static class GameEntry
    {
        public static async UniTask Start()
        {
            await Config.InitializeAsync();
            await UiManager.InitAsync(new UiRoot());
            if (UiManager.GetCamera() == null)
                throw new System.InvalidOperationException("UI 根节点缺少 Camera。");
            Debug.Log("HotUpdate.GameEntry.Start：YooAsset + HybridCLR 启动成功。");
            FrameworkLauncher.MarkHotUpdateStarted();
            // 在这里初始化业务服务、网络连接和首个游戏场景。

        }
    }
}
