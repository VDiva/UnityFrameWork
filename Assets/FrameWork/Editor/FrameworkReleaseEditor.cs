using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    [Serializable] public sealed class ReleaseFile
    {
        public string path;
        public long size;
        public string sha256;
    }
    [Serializable] public sealed class FrameworkReleaseManifest
    {
        public int schema = 1;
        public string platform, clientBuildId, packageName, resourceVersion, unityVersion, createdUtc, aotFingerprint;
        public string[] hotUpdateDlls;
        public List<ReleaseFile> files = new();
        public List<string> changedFiles = new();
    }
    [Serializable] internal sealed class ClientBaseline
    {
        public string platform, clientBuildId, aotFingerprint;
    }

    public static class FrameworkReleaseEditor
    {
        private const string BaselinePath = "ProjectSettings/FrameWorkReleaseBaseline.json";
        public static string Fingerprint()
        {
            string root = "Assets/FrameWork";
            var builder = new StringBuilder(Application.unityVersion + "\n" + EditorUserBuildSettings.activeBuildTarget + "\n");
            foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.StartsWith(root + "/Scripts/", StringComparison.Ordinal) ||
                    normalized.Contains("/Editor/") || normalized.Contains("/Data~/") || normalized.Contains("/Samples~/")) continue;
                string extension = Path.GetExtension(path);
                bool embedded = normalized.StartsWith(root + "/Resources/", StringComparison.Ordinal);
                bool metadata = normalized.StartsWith(root + "/Asset/Dll/", StringComparison.Ordinal) && !normalized.EndsWith("/HotUpdate.dll.bytes", StringComparison.Ordinal);
                bool nativeMeta = normalized.EndsWith(".cs.meta", StringComparison.Ordinal) || normalized.EndsWith(".asmdef.meta", StringComparison.Ordinal) || normalized.EndsWith(".dll.meta", StringComparison.Ordinal);
                if (extension != ".cs" && extension != ".asmdef" && extension != ".dll" && !embedded && !metadata && !nativeMeta) continue;
                builder.Append(normalized).Append(':').Append(Hash(path)).Append('\n');
            }
            builder.Append(File.ReadAllText("Packages/manifest.json"));
            foreach (string setting in new[] { "ProjectSettings/ProjectSettings.asset", FrameworkIntegrationEditor.BootScenePath })
                if (File.Exists(setting)) builder.Append(setting).Append(':').Append(Hash(setting)).Append('\n');
            using var algorithm = SHA256.Create();
            return Hex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        public static void SaveClientBaseline()
        {
            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(FrameworkIntegrationEditor.BootSettingsPath);
            var baseline = new ClientBaseline {
                platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                clientBuildId = boot.clientBuildId, aotFingerprint = Fingerprint()
            };
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));
        }

        [MenuItem("FrameWork/发布/发布热更新版本（DLL 和资源）")]
        public static void OpenReleaseWindow() => EditorWindow.GetWindow<FrameworkReleaseWindow>(true, "发布热更新版本");

        public static FrameworkReleaseManifest Publish(string version)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑器完成编译并停止运行后发布。");
            if (string.IsNullOrWhiteSpace(version) || !System.Text.RegularExpressions.Regex.IsMatch(version, @"^[A-Za-z0-9][A-Za-z0-9._-]*$"))
                throw new ArgumentException("资源版本只能包含字母、数字、点、下划线和短横线。");
            if (!File.Exists(BaselinePath)) throw new InvalidOperationException("请先准备新的客户端基线（含 AOT）。");
            var baseline = JsonUtility.FromJson<ClientBaseline>(File.ReadAllText(BaselinePath));
            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(FrameworkIntegrationEditor.BootSettingsPath);
            if (baseline.aotFingerprint != Fingerprint() || baseline.clientBuildId != boot.clientBuildId ||
                baseline.platform != EditorUserBuildSettings.activeBuildTarget.ToString())
                throw new InvalidOperationException("AOT 框架、插件、包依赖或平台已变化，需要重新准备客户端基线和安装包，不能作为旧客户端热更新发布。");
            var runtime = AssetDatabase.LoadAssetAtPath<YooResourceSettings>(YooResourceEditor.SettingsPath);
            string publish = Path.Combine("Publish", baseline.platform, baseline.clientBuildId, runtime.packageName);
            string archive = Path.Combine(publish, "_releases", version);
            if (Directory.Exists(archive)) throw new InvalidOperationException("该版本已发布，请使用新的版本号：" + version);
            FrameworkIntegrationEditor.CompileHotUpdate();
            // 编译热更新不能改动 AOT 基线；生成变化后的 AOT 源码同样需要新客户端。
            if (baseline.aotFingerprint != Fingerprint()) throw new InvalidOperationException("编译后检测到 AOT 代码变化。");
            var result = YooResourceEditor.Build(version);
            var manifest = new FrameworkReleaseManifest {
                platform = baseline.platform, clientBuildId = baseline.clientBuildId,
                packageName = runtime.packageName, resourceVersion = version,
                unityVersion = Application.unityVersion, createdUtc = DateTime.UtcNow.ToString("o"),
                aotFingerprint = baseline.aotFingerprint, hotUpdateDlls = boot.hotUpdateDlls
            };
            var previous = File.Exists(Path.Combine(publish, "version.json"))
                ? JsonUtility.FromJson<FrameworkReleaseManifest>(File.ReadAllText(Path.Combine(publish, "version.json"))) : null;
            var oldFiles = previous?.files.ToDictionary(f => f.path, f => f.sha256) ?? new Dictionary<string, string>();
            var sourceRoot = Path.GetFullPath(result.OutputPackageDirectory).TrimEnd(Path.DirectorySeparatorChar);
            foreach (string source in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                string relative = source.Substring(sourceRoot.Length + 1).Replace('\\', '/');
                var file = new ReleaseFile { path = relative, size = new FileInfo(source).Length, sha256 = Hash(source) };
                manifest.files.Add(file);
                if (!oldFiles.TryGetValue(relative, out string oldHash) || oldHash != file.sha256)
                    manifest.changedFiles.Add(relative);
            }
            Directory.CreateDirectory(archive);
            string patch = Path.Combine(publish, "_patches", version);
            Directory.CreateDirectory(patch);
            // 先写资源和清单，最后才更新 YooAsset .version 指针及自定义版本索引。
            foreach (var file in manifest.files.OrderBy(f => f.path.EndsWith(".version", StringComparison.Ordinal) ? 1 : 0))
            {
                string source = Path.Combine(sourceRoot, file.path);
                Copy(source, Path.Combine(publish, file.path));
                if (manifest.changedFiles.Contains(file.path)) Copy(source, Path.Combine(patch, file.path));
            }
            string json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(Path.Combine(archive, "version.json"), json);
            File.WriteAllText(Path.Combine(patch, "version.json"), json);
            File.WriteAllText(Path.Combine(patch, "changed-files.json"), JsonUtility.ToJson(new ChangedFiles { files = manifest.changedFiles.ToArray() }, true));
            File.WriteAllText(Path.Combine(publish, "version.json"), json);
            Debug.Log($"热更新发布完成：{publish}；变更 {manifest.changedFiles.Count}/{manifest.files.Count} 个文件。版本指针需最后上传。");
            return manifest;
        }

        public static void ValidatePublishingBatch()
        {
            try
            {
                string prefix = "validation-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var first = Publish(prefix + "-1");
                var second = Publish(prefix + "-2");
                if (first.files.Count == 0 || second.changedFiles.Count >= second.files.Count)
                    throw new InvalidOperationException("相同资源二次发布没有复用资源包。");
                string root = Path.Combine("Publish", second.platform, second.clientBuildId, second.packageName);
                foreach (var file in second.files)
                    if (Hash(Path.Combine(root, file.path)) != file.sha256)
                        throw new InvalidOperationException("发布文件哈希不符：" + file.path);
                bool duplicateBlocked = false;
                try { Publish(second.resourceVersion); }
                catch (InvalidOperationException) { duplicateBlocked = true; }
                if (!duplicateBlocked) throw new InvalidOperationException("重复版本未拦截。");
                string aot = "Assets/FrameWork/Tool/Bootstrap/FrameworkLauncher.cs";
                byte[] original = File.ReadAllBytes(aot);
                bool aotBlocked = false;
                try
                {
                    File.AppendAllText(aot, "\n// validation: AOT baseline guard\n");
                    try { Publish(prefix + "-guard"); }
                    catch (InvalidOperationException) { aotBlocked = true; }
                }
                finally { File.WriteAllBytes(aot, original); }
                if (!aotBlocked) throw new InvalidOperationException("AOT 变更未拦截。");
                Directory.CreateDirectory("Library/FrameWork/Validation");
                File.WriteAllText("Library/FrameWork/Validation/Publishing.json",
                    JsonUtility.ToJson(second, true));
                Debug.Log($"FRAMEWORK_PUBLISH_VALIDATION_OK files={second.files.Count} changed={second.changedFiles.Count}");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        [Serializable] private sealed class ChangedFiles { public string[] files; }
        private static void Copy(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }
        private static string Hash(string path)
        {
            using var algorithm = SHA256.Create();
            using var file = File.OpenRead(path);
            return Hex(algorithm.ComputeHash(file));
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public sealed class FrameworkReleaseWindow : EditorWindow
    {
        private string _version;
        private void OnEnable() { _version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"); }
        private void OnGUI()
        {
            _version = EditorGUILayout.TextField("资源版本", _version);
            EditorGUILayout.HelpBox("只发布 HotUpdate 程序集和资源。AOT 代码变化时请先准备新客户端基线。发布产物保存在项目 Publish 目录。", UnityEditor.MessageType.Info);
            if (GUILayout.Button("编译 DLL 并生成发布文件"))
            {
                try { FrameworkReleaseEditor.Publish(_version); Close(); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }
    }
}
