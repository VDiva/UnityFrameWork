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
        public List<ReleaseFile> inputs;
    }

    public static class FrameworkReleaseEditor
    {
        private const string BaselinePath = "ProjectSettings/FrameWorkReleaseBaseline.json";
        public static string Fingerprint() => Fingerprint(null);

        internal static string Fingerprint(List<ReleaseFile> inputs)
        {
            // Keep legacy baseline hashes when SDKs only toggle an inactive platform.
            var reference = File.Exists(BaselinePath)
                ? JsonUtility.FromJson<ClientBaseline>(File.ReadAllText(BaselinePath)) : null;
            var referenceInputs = reference?.platform == EditorUserBuildSettings.activeBuildTarget.ToString()
                ? reference.inputs?.ToDictionary(x => x.path, x => x.sha256) : null;
            var hotSources = FrameworkHotUpdateLayout.SourceFiles();
            var hotDlls = FrameworkHotUpdateLayout.AssemblyNames();
            var included = new HashSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder(Application.unityVersion + "\n" + EditorUserBuildSettings.activeBuildTarget + "\n");
            builder.Append("business-hotupdate-v1\n").Append(string.Join(";", hotDlls.OrderBy(n => n, StringComparer.Ordinal))).Append('\n');
            foreach (string path in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                string normalized = path.Replace('\\', '/');
                if (!ClientBaselineInputPolicy.Includes(normalized, hotSources, hotDlls)) continue;
                string hash = Hash(path);
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL &&
                    normalized.EndsWith(".a.meta", StringComparison.OrdinalIgnoreCase) &&
                    referenceInputs != null && referenceInputs.TryGetValue(normalized, out string previousHash) &&
                    hash != previousHash && MatchesInactiveWeixinToggle(File.ReadAllBytes(path), previousHash))
                    hash = previousHash;
                inputs?.Add(new ReleaseFile { path = normalized, sha256 = hash });
                included.Add(normalized);
                builder.Append(normalized).Append(':').Append(hash).Append('\n');
            }
            builder.Append(File.ReadAllText("Packages/manifest.json"));
            inputs?.Add(new ReleaseFile { path = "Packages/manifest.json", sha256 = Hash("Packages/manifest.json") });
            var startupInputs = new[] { "ProjectSettings/ProjectSettings.asset", FrameworkIntegrationEditor.BootScenePath }
                .Concat(AssetDatabase.GetDependencies(FrameworkIntegrationEditor.BootScenePath, true))
                .SelectMany(path => new[] { path, path + ".meta" }).Distinct();
            foreach (string setting in startupInputs)
                if (File.Exists(setting) && included.Add(setting))
                {
                    string hash = Hash(setting);
                    inputs?.Add(new ReleaseFile { path = setting, sha256 = hash });
                    builder.Append(setting).Append(':').Append(hash).Append('\n');
                }
            using var algorithm = SHA256.Create();
            return Hex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        // SDKs serialize the Tuanjie WeixinMiniGame switch even in Unity WebGL projects.
        // Accept only a byte-exact match after flipping that single inactive switch;
        // WebGL compatibility, plugin contents and all other importer settings remain guarded.
        internal static bool MatchesInactiveWeixinToggle(byte[] metadata, string expectedHash)
        {
            string text = Encoding.UTF8.GetString(metadata);
            var match = System.Text.RegularExpressions.Regex.Match(text,
                @"(?m)^  - first:\r?\n      WeixinMiniGame: WeixinMiniGame\r?\n    second:\r?\n      enabled: ([01])\r?$");
            if (!match.Success) return false;
            int index = match.Groups[1].Index;
            string alternate = text.Substring(0, index) + (text[index] == '0' ? "1" : "0") + text.Substring(index + 1);
            using var algorithm = SHA256.Create();
            return Hex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(alternate))) == expectedHash;
        }

        public static void SaveClientBaseline()
        {
            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(FrameworkIntegrationEditor.BootSettingsPath);
            var inputs = new List<ReleaseFile>();
            var baseline = new ClientBaseline {
                platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                clientBuildId = boot.clientBuildId, aotFingerprint = Fingerprint(inputs), inputs = inputs
            };
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));
        }

        internal static bool CanReuseClientBaseline(out string reason)
        {
            reason = "尚未生成客户端基线";
            if (!File.Exists(BaselinePath)) return false;
            var baseline = JsonUtility.FromJson<ClientBaseline>(File.ReadAllText(BaselinePath));
            var boot = AssetDatabase.LoadAssetAtPath<FrameworkBootSettings>(FrameworkIntegrationEditor.BootSettingsPath);
            if (baseline == null || boot == null || string.IsNullOrEmpty(boot.clientBuildId) ||
                baseline.clientBuildId != boot.clientBuildId)
            {
                reason = "客户端 ID 与基线不一致";
                return false;
            }
            if (baseline.platform != EditorUserBuildSettings.activeBuildTarget.ToString())
            {
                reason = "构建平台已变化";
                return false;
            }
            if (boot.aotMetadataDlls == null || boot.aotMetadataDlls.Length == 0 ||
                boot.aotMetadataDlls.Any(dll => !File.Exists(Path.Combine(FrameworkIntegrationEditor.DllDirectory, dll + ".bytes"))))
            {
                reason = "AOT 元数据缺失";
                return false;
            }
            var inputs = new List<ReleaseFile>();
            if (baseline.aotFingerprint == Fingerprint(inputs))
            {
                reason = "客户端输入未变化，复用现有 AOT 和客户端 ID";
                return true;
            }
            reason = "客户端输入已变化（含嵌入安装包的 Resources）";
            if (baseline.inputs != null)
            {
                var previous = baseline.inputs.ToDictionary(x => x.path, x => x.sha256);
                var current = inputs.ToDictionary(x => x.path, x => x.sha256);
                var changed = previous.Keys.Union(current.Keys).Where(path =>
                    !previous.TryGetValue(path, out string oldHash) ||
                    !current.TryGetValue(path, out string newHash) || oldHash != newHash).Take(8);
                reason += "：\n" + string.Join("\n", changed);
            }
            return false;
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
            if (!CanReuseClientBaseline(out string reason))
                throw new InvalidOperationException("需要重新准备客户端基线和安装包：" + reason);
            var runtime = AssetDatabase.LoadAssetAtPath<YooResourceSettings>(YooResourceEditor.SettingsPath);
            string publish = Path.Combine("Publish", baseline.platform, baseline.clientBuildId, runtime.packageName);
            string archive = Path.Combine("Publish", "_releases", baseline.platform,
                baseline.clientBuildId, runtime.packageName, version);
            // 兼容尚未迁移的历史存档，仍禁止重复发布旧版本。
            if (Directory.Exists(archive) || Directory.Exists(Path.Combine(publish, "_releases", version)))
                throw new InvalidOperationException("该版本已发布，请使用新的版本号：" + version);
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
            EditorGUILayout.HelpBox("发布统一的 HotUpdate.dll（框架业务和游戏逻辑）及 YooAsset 资源。只有启动壳、AOT 插件或平台变化才需要新客户端。产物保存在 Publish 目录。", UnityEditor.MessageType.Info);
            if (GUILayout.Button("编译 DLL 并生成发布文件"))
            {
                try { FrameworkReleaseEditor.Publish(_version); Close(); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }
    }
}
