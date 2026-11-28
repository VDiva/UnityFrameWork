using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor.SteamDeploy
{
    [Serializable]
    public sealed class SteamDeployConfig
    {
        public const string ConfigPath = "ProjectSettings/SteamDeployConfig.json";
        private const string PasswordPrefKey = "FrameWork.SteamDeploy.Password";
        private const string RememberPwdPrefKey = "FrameWork.SteamDeploy.RememberPassword";

        public string steamCmdPath = "";
        public string steamAccount = "";
        public string appId = "480";
        public string depotId = "481";
        public string branchName = "";
        public string buildsRootDir = "Builds/Steam";
        public string executableName = "";
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
        public bool developmentBuild = false;
        public bool writeSteamAppIdInBuild = false;
        public string buildDescription = "";
        public List<string> excludePatterns = new List<string>
        {
            "*.pdb",
            "*_BurstDebugInformation_DoNotShip*",
            "*_BackUpThisFolder_ButDontShipItWithYourGame*"
        };

        public static SteamDeployConfig Load()
        {
            SteamDeployConfig cfg = null;
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    cfg = JsonUtility.FromJson<SteamDeployConfig>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SteamDeploy] 读取配置失败，已使用默认配置：{ex.Message}");
                }
            }

            if (cfg == null)
            {
                cfg = new SteamDeployConfig();
                string rootAppIdFile = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
                if (File.Exists(rootAppIdFile))
                {
                    string id = File.ReadAllText(rootAppIdFile).Trim();
                    if (uint.TryParse(id, out uint parsed) && parsed > 0)
                    {
                        cfg.appId = parsed.ToString();
                        cfg.depotId = (parsed + 1).ToString();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(cfg.executableName))
                cfg.executableName = SanitizeFileName(PlayerSettings.productName);
            if (string.IsNullOrWhiteSpace(cfg.buildsRootDir))
                cfg.buildsRootDir = "Builds/Steam";
            if (cfg.excludePatterns == null)
                cfg.excludePatterns = new List<string>();
            return cfg;
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(this, true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamDeploy] 保存配置失败：{ex.Message}");
            }
        }

        public string GetAbsoluteBuildsRoot()
        {
            string root = string.IsNullOrWhiteSpace(buildsRootDir) ? "Builds/Steam" : buildsRootDir.Trim();
            return Path.IsPathRooted(root)
                ? Path.GetFullPath(root)
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), root));
        }

        public static bool RememberPassword
        {
            get => EditorPrefs.GetBool(RememberPwdPrefKey, false);
            set => EditorPrefs.SetBool(RememberPwdPrefKey, value);
        }

        public static string LoadSavedPassword()
        {
            return RememberPassword ? EditorPrefs.GetString(PasswordPrefKey, string.Empty) : string.Empty;
        }

        public static void SavePassword(string password)
        {
            if (RememberPassword && !string.IsNullOrEmpty(password))
                EditorPrefs.SetString(PasswordPrefKey, password);
            else
                EditorPrefs.DeleteKey(PasswordPrefKey);
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Game";
            char[] invalid = Path.GetInvalidFileNameChars();
            string result = name.Trim();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return string.IsNullOrWhiteSpace(result) ? "Game" : result;
        }
    }

    [Serializable]
    public sealed class SteamBuildMetadata
    {
        public const string FileName = "steam_build_meta.json";

        public string version = "";
        public string buildTarget = "";
        public string createdLocalTime = "";
        public string description = "";
        public string lastUploadedLocalTime = "";
        public string lastUploadedBranch = "";
        public string lastUploadedAppId = "";
    }

    public sealed class SteamBuildEntry
    {
        public string FolderName;
        public string FullPath;
        public DateTime CreatedTime;
        public long SizeBytes;
        public SteamBuildMetadata Metadata;

        public string DisplayVersion =>
            !string.IsNullOrWhiteSpace(Metadata?.version) ? Metadata.version : FolderName;

        public string DisplayTarget =>
            !string.IsNullOrWhiteSpace(Metadata?.buildTarget) ? Metadata.buildTarget : "Unknown";

        public bool HasUploaded => !string.IsNullOrWhiteSpace(Metadata?.lastUploadedLocalTime);

        public string FormatSize()
        {
            double mb = SizeBytes / (1024.0 * 1024.0);
            if (mb >= 1024.0) return $"{mb / 1024.0:0.00} GB";
            return $"{mb:0.0} MB";
        }
    }
}
