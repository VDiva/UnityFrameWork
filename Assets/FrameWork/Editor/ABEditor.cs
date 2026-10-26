using UnityEditor;

namespace FrameWork
{
    /// <summary>保留旧编辑器入口；资源配置和构建统一交给 YooAsset。</summary>
    public static class ABEditor
    {
        public static string abAssetPath = "Assets/FrameWork/Asset";
        [MenuItem("FrameWork/更新资源")]
        public static void AssetPackaged() => Editor.YooResourceEditor.Configure();
        [MenuItem("FrameWork/刷新代码")]
        public static void UpdateScript() => UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
    }
}
