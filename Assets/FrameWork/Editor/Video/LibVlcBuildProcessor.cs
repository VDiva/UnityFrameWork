using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FrameWork.Editor.Video
{
    /// <summary>
    /// Windows 64 位打包后自动将 LibVLC 原生动态库与 plugins 目录原样拷贝到 &lt;Build&gt;_Data/LibVLC/win-x64/，
    /// 避免 Unity 扁平化拷贝破坏 LibVLC 插件子目录结构，同时保证 WebGL / 微信小游戏零包体污染。
    /// </summary>
    public sealed class LibVlcBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
                return;

            string srcDir = Path.GetFullPath("Assets/FrameWork/Plugins/LibVLC/Native~/win-x64");
            if (!Directory.Exists(srcDir))
            {
                Debug.LogWarning($"[LibVLC] 未找到原生库目录，跳过拷贝：{srcDir}");
                return;
            }

            string outputExe = report.summary.outputPath;
            string buildDir = Path.GetDirectoryName(outputExe);
            string exeName = Path.GetFileNameWithoutExtension(outputExe);
            if (string.IsNullOrEmpty(buildDir) || string.IsNullOrEmpty(exeName))
                return;

            string dstDir = Path.Combine(buildDir, exeName + "_Data", "LibVLC", "win-x64");
            CopyDirectory(srcDir, dstDir);

            string noticeSrc = Path.GetFullPath("Assets/FrameWork/Plugins/LibVLC/THIRD_PARTY_NOTICE_LGPL.txt");
            if (File.Exists(noticeSrc))
                File.Copy(noticeSrc, Path.Combine(dstDir, "THIRD_PARTY_NOTICE_LGPL.txt"), true);

            Debug.Log($"[LibVLC] 已拷贝 Windows x64 原生库到：{dstDir}");
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (string subDir in Directory.GetDirectories(sourceDir))
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
        }
    }
}
