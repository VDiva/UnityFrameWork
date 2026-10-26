using UnityEngine;

namespace FrameWork
{
    public enum YooPlayerMode { Offline, Host }

    [CreateAssetMenu(fileName = "YooResourceSettings", menuName = "FrameWork/YooAsset 资源配置")]
    public sealed class YooResourceSettings : ScriptableObject
    {
        public string packageName = "DefaultPackage";
        public bool editorSimulate = true;
        public YooPlayerMode playerMode = YooPlayerMode.Offline;
        [Tooltip("正式环境资源完整 URL，支持 {platform}、{client}、{package}。未定义 FRAMEWORK_TEST 时使用。")]
        public string remoteUrl = "";
        public string fallbackRemoteUrl = "";
        [Tooltip("定义 FRAMEWORK_TEST 时使用的测试环境资源 URL；留空会报错，不会回退到正式环境。")]
        public string testRemoteUrl = "";
        public string testFallbackRemoteUrl = "";

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
        public string packageVersion = "";
        [Min(1)] public int timeoutSeconds = 60;
        [Min(1)] public int downloadConcurrency = 8;
        [Min(0)] public int downloadRetryCount = 3;
    }
}
