using UnityEngine;

namespace FrameWork
{
    [CreateAssetMenu(fileName = "FrameworkBootSettings", menuName = "FrameWork/启动配置")]
    public sealed class FrameworkBootSettings : ScriptableObject
    {
        public bool downloadBeforeStart = true;
        public bool useEditorAssemblies = false;
        public string clientBuildId = "";
        [Tooltip("按依赖顺序排列，不带 .bytes 后缀；构建工具自动同步。")]
        public string[] hotUpdateDlls = { "HotUpdate.dll" };
        [Tooltip("使用当前安装包构建时的裁剪后 AOT 元数据，不能用别的客户端版本替换。")]
        public string[] aotMetadataDlls = new string[0];
        public string entryAssembly = "HotUpdate";
        public string entryType = "HotUpdate.GameEntry";
        public string entryMethod = "Start";
    }
}
