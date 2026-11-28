using System;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor.AssetOptimizer
{
    public enum AssetOptimizeCategory
    {
        All = 0,
        Texture = 1,
        Audio = 2,
        Model = 3,
        PrefabAndMaterial = 4,
        LargeFile = 5
    }

    public enum AssetIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum AssetOptimizeRuleId
    {
        TextureReadWrite,
        TextureUiMipMap,
        TextureMaxSize,
        TextureUncompressed,
        TextureUnusedAlpha,
        TextureNonPowerOfTwo,

        AudioLongNotStreaming,
        AudioShortNotDecompressOnLoad,
        AudioMediumNotCompressedInMemory,
        AudioLongPcmCompression,
        AudioForceToMono,

        ModelReadWrite,
        ModelMeshCompression,
        ModelImportCamerasAndLights,
        ModelOptimizeMesh,
        ModelAnimationCompression,

        PrefabMissingScript,
        PrefabUnusedRaycastTarget,
        MaterialUnusedProperties,

        LargeAssetFile
    }

    public sealed class AssetIssueItem
    {
        public string AssetPath;
        public string AssetName;
        public AssetOptimizeCategory Category;
        public AssetIssueSeverity Severity;
        public AssetOptimizeRuleId RuleId;
        public string IssueSummary;
        public string FixDescription;
        public long FileSizeBytes;
        public bool CanAutoFix;
        public bool Selected;
        public bool IsFixed;
        public string ExtraData;

        public string FileSizeText => FormatBytes(FileSizeBytes);

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024d;
            if (kb < 1024d) return $"{kb:0.0} KB";
            double mb = kb / 1024d;
            if (mb < 1024d) return $"{mb:0.00} MB";
            return $"{mb / 1024d:0.00} GB";
        }
    }

    [Serializable]
    public sealed class AssetOptimizeSettings
    {
        private const string PrefsKey = "FrameWork.Editor.AssetOptimizer.Settings.v1";

        public string ScanRootFolder = "Assets/FrameWork/Asset";
        public string ExcludeFolders = "Plugins;Sirenix;TextMesh Pro;Gizmos;Editor";

        // Texture rules
        public bool CheckTextures = true;
        public int MaxTextureSizeLimit = 1024;
        public bool CheckTextureReadWrite = true;
        public bool CheckTextureUiMipMap = true;
        public bool CheckTextureUncompressed = true;
        public bool CheckTextureUnusedAlpha = true;
        public bool CheckTextureNpot = true;

        // Audio rules
        public bool CheckAudio = true;
        public float ShortAudioSeconds = 1.0f;
        public float LongAudioSeconds = 5.0f;
        public bool CheckAudioForceToMono = true;

        // Model rules
        public bool CheckModels = true;
        public bool CheckModelReadWrite = true;
        public bool CheckModelMeshCompression = true;
        public bool CheckModelCamerasAndLights = true;
        public bool CheckModelOptimizeMesh = true;
        public bool CheckModelAnimationCompression = true;

        // Prefab & Material rules
        public bool CheckPrefabsAndMaterials = true;
        public bool CheckPrefabMissingScripts = true;
        public bool CheckPrefabUnusedRaycast = true;
        public bool CheckMaterialUnusedProperties = true;

        // Large file rules
        public bool CheckLargeFiles = true;
        public float LargeFileThresholdMB = 5.0f;

        public static AssetOptimizeSettings Load()
        {
            string json = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<AssetOptimizeSettings>(json);
                    if (loaded != null)
                    {
                        loaded.Sanitize();
                        return loaded;
                    }
                }
                catch
                {
                    // Fall back to defaults if stored JSON is corrupted.
                }
            }

            var defaults = new AssetOptimizeSettings();
            defaults.Sanitize();
            return defaults;
        }

        public void Save()
        {
            Sanitize();
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
        }

        public void ResetToDefaults()
        {
            var defaults = new AssetOptimizeSettings();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), this);
            Save();
        }

        public void Sanitize()
        {
            if (string.IsNullOrWhiteSpace(ScanRootFolder))
            {
                ScanRootFolder = "Assets/FrameWork/Asset";
            }

            ScanRootFolder = ScanRootFolder.Replace('\\', '/').TrimEnd('/');
            MaxTextureSizeLimit = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(64, MaxTextureSizeLimit)), 64, 8192);
            ShortAudioSeconds = Mathf.Clamp(ShortAudioSeconds, 0.2f, 5f);
            LongAudioSeconds = Mathf.Clamp(LongAudioSeconds, ShortAudioSeconds + 0.5f, 60f);
            LargeFileThresholdMB = Mathf.Clamp(LargeFileThresholdMB, 0.5f, 200f);
        }

        public string[] GetExcludeFolderList()
        {
            if (string.IsNullOrWhiteSpace(ExcludeFolders))
            {
                return Array.Empty<string>();
            }

            return ExcludeFolders.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
