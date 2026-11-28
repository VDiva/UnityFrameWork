using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FrameWork.Editor.SteamDeploy
{
    public sealed class SteamDeployService
    {
        private Process _activeProcess;
        private readonly object _procGate = new object();

        public bool IsUploading
        {
            get
            {
                lock (_procGate)
                    return _activeProcess != null && !_activeProcess.HasExited;
            }
        }

        public List<SteamBuildEntry> ScanBuilds(SteamDeployConfig config)
        {
            var list = new List<SteamBuildEntry>();
            string root = config.GetAbsoluteBuildsRoot();
            if (!Directory.Exists(root))
                return list;

            foreach (string dir in Directory.GetDirectories(root))
            {
                string folderName = Path.GetFileName(dir);
                if (string.Equals(folderName, "_vdf_temp", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(folderName, "_steam_output", StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = new DirectoryInfo(dir);
                var entry = new SteamBuildEntry
                {
                    FolderName = folderName,
                    FullPath = dir,
                    CreatedTime = info.CreationTime,
                    SizeBytes = CalculateDirectorySize(dir),
                    Metadata = ReadMetadata(dir)
                };
                list.Add(entry);
            }

            list.Sort((a, b) => b.CreatedTime.CompareTo(a.CreatedTime));
            return list;
        }

        public bool BuildPlayer(SteamDeployConfig config, Action<string> onLog, out SteamBuildEntry created, out string error)
        {
            created = null;
            error = null;

            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(s => s != null && s.enabled && !string.IsNullOrWhiteSpace(s.path))
                .Select(s => s.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                error = "Build Settings 中没有启用的场景，请先在 File -> Build Settings 中勾选至少一个场景。";
                return false;
            }

            string version = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
                ? "0.1.0"
                : PlayerSettings.bundleVersion.Trim();
            string safeVer = SteamDeployConfig.SanitizeFileName(version);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folderName = $"v{safeVer}_{timestamp}";
            string root = config.GetAbsoluteBuildsRoot();
            string outputDir = Path.Combine(root, folderName);
            Directory.CreateDirectory(outputDir);

            string exeBase = SteamDeployConfig.SanitizeFileName(
                string.IsNullOrWhiteSpace(config.executableName) ? PlayerSettings.productName : config.executableName);
            string locationPath = GetExecutableLocation(outputDir, exeBase, config.buildTarget);

            onLog?.Invoke($"[Build] 开始打包 ({config.buildTarget}) -> {outputDir}");
            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = locationPath,
                target = config.buildTarget,
                options = config.developmentBuild ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                error = $"打包失败：{report.summary.result}（错误数：{report.summary.totalErrors}）";
                onLog?.Invoke($"[Build] {error}");
                return false;
            }

            if (config.writeSteamAppIdInBuild && !string.IsNullOrWhiteSpace(config.appId))
            {
                File.WriteAllText(Path.Combine(outputDir, "steam_appid.txt"), config.appId.Trim());
            }

            var meta = new SteamBuildMetadata
            {
                version = version,
                buildTarget = config.buildTarget.ToString(),
                createdLocalTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                description = config.buildDescription ?? string.Empty
            };
            WriteMetadata(outputDir, meta);

            created = new SteamBuildEntry
            {
                FolderName = folderName,
                FullPath = outputDir,
                CreatedTime = Directory.GetCreationTime(outputDir),
                SizeBytes = CalculateDirectorySize(outputDir),
                Metadata = meta
            };

            onLog?.Invoke($"[Build] 打包完成：{folderName}（体积：{created.FormatSize()}，耗时：{report.summary.totalTime.TotalSeconds:0.1}s）");
            return true;
        }

        public string GenerateVdfFiles(SteamDeployConfig config, SteamBuildEntry build, string overrideDesc)
        {
            string root = config.GetAbsoluteBuildsRoot();
            string vdfDir = Path.Combine(root, "_vdf_temp");
            string steamOutDir = Path.Combine(root, "_steam_output");
            Directory.CreateDirectory(vdfDir);
            Directory.CreateDirectory(steamOutDir);

            string appId = config.appId.Trim();
            string depotId = config.depotId.Trim();
            string desc = string.IsNullOrWhiteSpace(overrideDesc)
                ? $"{build.DisplayVersion} ({build.FolderName})"
                : overrideDesc.Trim();

            string depotVdfPath = Path.Combine(vdfDir, $"depot_build_{depotId}.vdf");
            string appVdfPath = Path.Combine(vdfDir, $"app_build_{appId}.vdf");

            var depotSb = new StringBuilder();
            depotSb.AppendLine("\"DepotBuildConfig\"");
            depotSb.AppendLine("{");
            depotSb.AppendLine($"\t\"DepotID\" \"{depotId}\"");
            depotSb.AppendLine($"\t\"ContentRoot\" \"{EscapeVdf(build.FullPath)}\"");
            depotSb.AppendLine("\t\"FileMapping\"");
            depotSb.AppendLine("\t{");
            depotSb.AppendLine("\t\t\"LocalPath\" \"*\"");
            depotSb.AppendLine("\t\t\"DepotPath\" \".\"");
            depotSb.AppendLine("\t\t\"recursive\" \"1\"");
            depotSb.AppendLine("\t}");
            depotSb.AppendLine($"\t\"FileExclusion\" \"{SteamBuildMetadata.FileName}\"");
            if (config.excludePatterns != null)
            {
                foreach (string pattern in config.excludePatterns)
                {
                    if (!string.IsNullOrWhiteSpace(pattern))
                        depotSb.AppendLine($"\t\"FileExclusion\" \"{EscapeVdf(pattern.Trim())}\"");
                }
            }
            depotSb.AppendLine("}");
            File.WriteAllText(depotVdfPath, depotSb.ToString(), new UTF8Encoding(false));

            var appSb = new StringBuilder();
            appSb.AppendLine("\"appbuild\"");
            appSb.AppendLine("{");
            appSb.AppendLine($"\t\"appid\" \"{appId}\"");
            appSb.AppendLine($"\t\"desc\" \"{EscapeVdf(desc)}\"");
            appSb.AppendLine($"\t\"buildoutput\" \"{EscapeVdf(steamOutDir)}\"");
            appSb.AppendLine($"\t\"contentroot\" \"{EscapeVdf(build.FullPath)}\"");
            if (!string.IsNullOrWhiteSpace(config.branchName))
                appSb.AppendLine($"\t\"setlive\" \"{EscapeVdf(config.branchName.Trim())}\"");
            appSb.AppendLine("\t\"preview\" \"0\"");
            appSb.AppendLine("\t\"depots\"");
            appSb.AppendLine("\t{");
            appSb.AppendLine($"\t\t\"{depotId}\" \"{EscapeVdf(depotVdfPath)}\"");
            appSb.AppendLine("\t}");
            appSb.AppendLine("}");
            File.WriteAllText(appVdfPath, appSb.ToString(), new UTF8Encoding(false));

            return appVdfPath;
        }

        public bool StartUpload(
            SteamDeployConfig config,
            string password,
            SteamBuildEntry build,
            string description,
            Action<string> onLog,
            Action<bool, string> onCompleted)
        {
            if (IsUploading)
            {
                onCompleted?.Invoke(false, "已有上传任务正在运行。");
                return false;
            }
            if (build == null || !Directory.Exists(build.FullPath))
            {
                onCompleted?.Invoke(false, "所选打包目录不存在，请先刷新列表或重新打包。");
                return false;
            }
            if (string.IsNullOrWhiteSpace(config.steamCmdPath) || !File.Exists(config.steamCmdPath))
            {
                onCompleted?.Invoke(false, "未找到 steamcmd.exe，请在配置区指定正确的 SteamCMD 路径。");
                return false;
            }
            if (string.IsNullOrWhiteSpace(config.steamAccount))
            {
                onCompleted?.Invoke(false, "请填写 Steam 开发者账号。");
                return false;
            }
            if (!uint.TryParse(config.appId?.Trim(), out _) || !uint.TryParse(config.depotId?.Trim(), out _))
            {
                onCompleted?.Invoke(false, "AppID 和 DepotID 必须为有效数字。");
                return false;
            }

            string appVdfPath = GenerateVdfFiles(config, build, description);
            string account = config.steamAccount.Trim();
            string loginArg = string.IsNullOrEmpty(password)
                ? $"+login \"{account}\""
                : $"+login \"{account}\" \"{password}\"";
            string args = $"{loginArg} +run_app_build \"{appVdfPath}\" +quit";

            onLog?.Invoke($"[SteamCMD] 准备上传：{build.FolderName} (AppID={config.appId}, DepotID={config.depotId}, Branch={(string.IsNullOrWhiteSpace(config.branchName) ? "<不切分支>" : config.branchName)})");
            onLog?.Invoke($"[SteamCMD] VDF: {appVdfPath}");

            var psi = new ProcessStartInfo
            {
                FileName = config.steamCmdPath,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(config.steamCmdPath) ?? Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            bool sawError = false;

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                if (e.Data.IndexOf("ERROR!", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.Data.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
                    sawError = true;
                onLog?.Invoke(MaskSecret(e.Data, password));
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                sawError = true;
                onLog?.Invoke("[ERR] " + MaskSecret(e.Data, password));
            };
            process.Exited += (_, _) =>
            {
                int exitCode = -1;
                try { exitCode = process.ExitCode; } catch { }
                lock (_procGate)
                {
                    if (ReferenceEquals(_activeProcess, process))
                        _activeProcess = null;
                }
                process.Dispose();

                bool ok = exitCode == 0 && !sawError;
                if (ok)
                {
                    SteamBuildMetadata meta = build.Metadata ?? new SteamBuildMetadata();
                    meta.lastUploadedLocalTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    meta.lastUploadedBranch = string.IsNullOrWhiteSpace(config.branchName) ? "(未切分支)" : config.branchName.Trim();
                    meta.lastUploadedAppId = config.appId.Trim();
                    if (!string.IsNullOrWhiteSpace(description))
                        meta.description = description.Trim();
                    build.Metadata = meta;
                    WriteMetadata(build.FullPath, meta);
                }

                string msg = ok
                    ? $"上传成功：{build.FolderName} (ExitCode=0)"
                    : $"上传失败或已中止 (ExitCode={exitCode})，请检查上方 SteamCMD 日志（如 Steam Guard 验证或权限问题）。";
                onCompleted?.Invoke(ok, msg);
            };

            try
            {
                lock (_procGate)
                    _activeProcess = process;

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return true;
            }
            catch (Exception ex)
            {
                lock (_procGate)
                {
                    if (ReferenceEquals(_activeProcess, process))
                        _activeProcess = null;
                }
                process.Dispose();
                onCompleted?.Invoke(false, $"启动 SteamCMD 失败：{ex.Message}");
                return false;
            }
        }

        public void CancelUpload(Action<string> onLog)
        {
            lock (_procGate)
            {
                if (_activeProcess != null && !_activeProcess.HasExited)
                {
                    try
                    {
                        _activeProcess.Kill();
                        onLog?.Invoke("[SteamCMD] 已手动终止上传进程。");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SteamDeploy] 终止 SteamCMD 失败：{ex.Message}");
                    }
                }
            }
        }

        public bool DeleteBuild(SteamBuildEntry build, out string error)
        {
            error = null;
            if (build == null || !Directory.Exists(build.FullPath))
                return true;
            try
            {
                Directory.Delete(build.FullPath, true);
                return true;
            }
            catch (Exception ex)
            {
                error = $"删除目录失败：{ex.Message}";
                return false;
            }
        }

        private static string GetExecutableLocation(string outputDir, string exeBase, BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(outputDir, exeBase + ".exe");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(outputDir, exeBase + ".app");
                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(outputDir, exeBase + ".x86_64");
                default:
                    return Path.Combine(outputDir, exeBase + ".exe");
            }
        }

        private static SteamBuildMetadata ReadMetadata(string dir)
        {
            string path = Path.Combine(dir, SteamBuildMetadata.FileName);
            if (!File.Exists(path)) return new SteamBuildMetadata();
            try
            {
                return JsonUtility.FromJson<SteamBuildMetadata>(File.ReadAllText(path)) ?? new SteamBuildMetadata();
            }
            catch
            {
                return new SteamBuildMetadata();
            }
        }

        private static void WriteMetadata(string dir, SteamBuildMetadata meta)
        {
            try
            {
                File.WriteAllText(Path.Combine(dir, SteamBuildMetadata.FileName), JsonUtility.ToJson(meta, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamDeploy] 写入构建元数据失败：{ex.Message}");
            }
        }

        private static long CalculateDirectorySize(string dir)
        {
            long total = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    total += new FileInfo(file).Length;
            }
            catch { }
            return total;
        }

        private static string EscapeVdf(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string MaskSecret(string line, string secret) =>
            !string.IsNullOrEmpty(secret) ? line.Replace(secret, "******") : line;
    }
}
