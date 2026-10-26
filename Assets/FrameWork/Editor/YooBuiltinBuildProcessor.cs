using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace FrameWork.Editor
{
    /// <summary>仅构建 Player 时暂存首包；日常 Assets 仍只保留 FrameWork。</summary>
    [InitializeOnLoad]
    public sealed class YooBuiltinBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string Target = "Assets/StreamingAssets/yoo";
        private const string StagedKey = "FrameWork.YooAsset.Staged";
        private const string RootCreatedKey = "FrameWork.YooAsset.StreamingRootCreated";
        public int callbackOrder => 100;

        static YooBuiltinBuildProcessor()
        {
            EditorApplication.delayCall += () => {
                if (!BuildPipeline.isBuildingPlayer) Cleanup();
            };
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUserBuildSettings.buildScriptsOnly) return; // HybridCLR 生成裁剪 AOT 的内部构建。
            Cleanup();
            string source = Path.Combine(YooResourceEditor.BuiltinRoot, report.summary.platform.ToString(), "yoo");
            if (!Directory.Exists(source))
            {
                throw new BuildFailedException("请先构建当前平台资源，再构建 Player；启动需要内置资源目录和 BuiltinCatalog。");
            }
            if (Directory.Exists(Target))
                throw new BuildFailedException("StreamingAssets/yoo 已存在；请先自行合并，框架不会覆盖已有首包。");
            SessionState.SetBool(RootCreatedKey, !Directory.Exists("Assets/StreamingAssets"));
            SessionState.SetBool(StagedKey, true);
            try
            {
                Directory.CreateDirectory("Assets/StreamingAssets");
                FileUtil.CopyFileOrDirectory(source, Target);
                AssetDatabase.Refresh();
            }
            catch { Cleanup(); throw; }
        }

        public void OnPostprocessBuild(BuildReport report) => Cleanup();

        [MenuItem("FrameWork/YooAsset/清理构建临时首包")]
        public static void Cleanup()
        {
            if (!SessionState.GetBool(StagedKey, false)) return;
            if (Directory.Exists(Target)) AssetDatabase.DeleteAsset(Target);
            if (SessionState.GetBool(RootCreatedKey, false) && Directory.Exists("Assets/StreamingAssets") &&
                Directory.GetFileSystemEntries("Assets/StreamingAssets").Length == 0)
                AssetDatabase.DeleteAsset("Assets/StreamingAssets");
            SessionState.SetBool(StagedKey, false);
            SessionState.SetBool(RootCreatedKey, false);
        }
    }
}
