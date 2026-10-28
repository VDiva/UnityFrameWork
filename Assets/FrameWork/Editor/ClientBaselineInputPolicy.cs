using System;
using System.Collections.Generic;
using System.IO;

namespace FrameWork.Editor
{
    // 无 Unity 状态依赖，供指纹计算和发布边界回归检查共用。
    internal static class ClientBaselineInputPolicy
    {
        internal static bool Includes(string path, HashSet<string> hotSources, HashSet<string> hotDlls)
        {
            const string dllRoot = "Assets/FrameWork/Asset/Dll/";
            path = path.Replace('\\', '/');
            if (path.Contains("/Editor/") || path.Contains("/Data~/") || path.Contains("/Samples~/")) return false;
            string asset = path.EndsWith(".meta", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path;
            if (hotSources.Contains(asset)) return false;
            if (asset.StartsWith(dllRoot, StringComparison.Ordinal) && asset.EndsWith(".dll.bytes", StringComparison.Ordinal) &&
                hotDlls.Contains(Path.GetFileName(asset).Replace(".dll.bytes", ""))) return false;
            string extension = Path.GetExtension(path);
            string assetExtension = Path.GetExtension(asset);
            bool native = path.Contains("/Plugins/") &&
                (assetExtension == ".jslib" || assetExtension == ".a" || assetExtension == ".so" ||
                 assetExtension == ".dylib" || assetExtension == ".cpp" || assetExtension == ".c" ||
                 assetExtension == ".h" || assetExtension == ".mm");
            return extension == ".cs" || extension == ".asmdef" || extension == ".dll" ||
                path.Contains("/Resources/") || path.StartsWith(dllRoot, StringComparison.Ordinal) || native ||
                path.EndsWith(".cs.meta", StringComparison.Ordinal) || path.EndsWith(".asmdef.meta", StringComparison.Ordinal) ||
                path.EndsWith(".dll.meta", StringComparison.Ordinal);
        }
    }
}
