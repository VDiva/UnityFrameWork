using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork
{
    public enum YooPlayerMode { Offline, Host }

    [CreateAssetMenu(fileName = "YooResourceSettings", menuName = "FrameWork/YooAsset 资源配置")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "FrameWork.Runtime", null)]
    public sealed class YooResourceSettings : ScriptableObject
    {
        [LabelText("资源包名称")] public string packageName = "DefaultPackage";
        [LabelText("编辑器模拟模式")] public bool editorSimulate = true;
        [LabelText("运行模式")] public YooPlayerMode playerMode = YooPlayerMode.Offline;
        [Tooltip("正式环境资源完整 URL，支持 {platform}、{client}、{package}。未定义 FRAMEWORK_TEST 时使用。")]
        [LabelText("正式资源地址")] public string remoteUrl = "";
        [LabelText("正式备用资源地址")] public string fallbackRemoteUrl = "";
        [Tooltip("定义 FRAMEWORK_TEST 时使用的测试环境资源 URL；留空会报错，不会回退到正式环境。")]
        [LabelText("测试资源地址")] public string testRemoteUrl = "";
        [LabelText("测试备用资源地址")] public string testFallbackRemoteUrl = "";

#if FRAMEWORK_TEST
        public const string EnvironmentName = "测试";
        public string ActiveRemoteUrl => testRemoteUrl;
        public string ActiveFallbackRemoteUrl => testFallbackRemoteUrl;
#else
        public const string EnvironmentName = "正式";
        public string ActiveRemoteUrl => remoteUrl;
        public string ActiveFallbackRemoteUrl => fallbackRemoteUrl;
#endif
        [Tooltip("留空时向文件系统请求包版本。")]
        [LabelText("固定资源版本")] public string packageVersion = "";
        [LabelText("超时时间（秒）")] [Min(1)] public int timeoutSeconds = 60;
        [LabelText("并发下载数")] [Min(1)] public int downloadConcurrency = 8;
        [LabelText("下载重试次数")] [Min(0)] public int downloadRetryCount = 3;
    }
}
