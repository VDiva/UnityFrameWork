using System;
using Cysharp.Threading.Tasks;
using GameData;
using UnityEngine;

namespace FrameWork.Script.WebNet
{
    /// <summary>网络框架与游戏规则之间的接入点；运输、重连、房间、同步不依赖业务管理器。</summary>
    public static class WebNetworkIntegration
    {
        public static string LoginSceneName { get; set; } = "StartScene";
        /// <summary>请求失败的提示接入；未设置时写日志，不依赖任何业务窗口。</summary>
        public static Func<string, UniTask> ErrorHandler;
        public static UniTask ShowErrorAsync(string message)
        {
            if (ErrorHandler != null) return ErrorHandler(message);
            Debug.LogWarning(message);
            return UniTask.CompletedTask;
        }
        /// <summary>可选：登录大厅后改为进入教程等初始房间；返回 null 则使用服务端房间。</summary>
        public static Func<NetworkRoomData, UserData, NetworkRoomData> InitialRoomFactory;
        /// <summary>可选：在场景 Awake 前准备业务地图配置；返回 false 阻止此次加载。</summary>
        public static Func<NetworkRoomData, bool> PrepareRoomScene;
        /// <summary>可选：网络身份已绑定、对象尚未激活时设置掉落或其它业务数据。</summary>
        public static Action<GameObject, NetworkObjectData> ConfigureSpawnedObject;
        /// <summary>可选：如怪物登场期间暂缓远端 Spine 动画覆盖。</summary>
        public static Func<GameObject, bool> SuppressRemoteSpineAnimation;
        /// <summary>使用网络 SetParent RPC 时提供布局编号对应的父节点。</summary>
        public static Func<int, Transform> ParentResolver;
        public static Transform ResolveParent(int id)
        {
            if (ParentResolver == null)
                throw new InvalidOperationException("使用网络 SetParent 前请设置 WebNetworkIntegration.ParentResolver。");
            return ParentResolver(id);
        }
    }
}
