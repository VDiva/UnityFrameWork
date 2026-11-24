using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork
{
    [CreateAssetMenu(fileName = "FrameworkBootSettings", menuName = "FrameWork/启动配置")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "FrameWork.Runtime", null)]
    public sealed class FrameworkBootSettings : ScriptableObject
    {
        [LabelText("启动前下载资源")] public bool downloadBeforeStart = true;
        [LabelText("使用编辑器程序集")] public bool useEditorAssemblies = false;
        [LabelText("客户端基线 ID")] public string clientBuildId = "";
        [Tooltip("按依赖顺序排列，不带 .bytes 后缀；构建工具自动同步。")]
        [LabelText("热更新程序集")] public string[] hotUpdateDlls = { "HotUpdate.dll" };
        [Tooltip("使用当前安装包构建时的裁剪后 AOT 元数据，不能用别的客户端版本替换。")]
        [LabelText("AOT 元数据")] public string[] aotMetadataDlls = new string[0];
        [LabelText("入口程序集")] public string entryAssembly = "HotUpdate";
        [LabelText("入口类型")] public string entryType = "HotUpdate.GameEntry";
        [LabelText("入口方法")] public string entryMethod = "Start";
    }
}
