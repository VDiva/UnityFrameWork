using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FrameWork.Editor.AssetOptimizer
{
    public sealed class AssetScanReport
    {
        public int ScannedAssetCount;
        public bool WasCancelled;
        public double ElapsedSeconds;
        public List<AssetIssueItem> Issues { get; } = new List<AssetIssueItem>();
    }

    public sealed class AssetOptimizeBatchResult
    {
        public int FixedIssueCount;
        public int ModifiedAssetCount;
        public int FailedAssetCount;
        public List<string> Errors { get; } = new List<string>();
    }

    public static class AssetOptimizeScanner
    {
        private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".bmp", ".exr", ".hdr"
        };

        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac"
        };

        private static readonly HashSet<string> ModelExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".fbx", ".obj", ".dae", ".blend", ".3ds", ".max"
        };

        public static AssetScanReport Scan(AssetOptimizeSettings settings)
        {
            var report = new AssetScanReport();
            if (settings == null) return report;

            settings.Sanitize();
            if (!AssetDatabase.IsValidFolder(settings.ScanRootFolder))
            {
                return report;
            }

            double startTime = EditorApplication.timeSinceStartup;
            string[] excludes = settings.GetExcludeFolderList();
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { settings.ScanRootFolder });
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                int total = guids.Length;
                for (int i = 0; i < total; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(assetPath) || !seenPaths.Add(assetPath))
                    {
                        continue;
                    }

                    if (AssetDatabase.IsValidFolder(assetPath) || IsExcluded(assetPath, settings.ScanRootFolder, excludes))
                    {
                        continue;
                    }

                    if (i % 15 == 0)
                    {
                        float progress = total > 0 ? (float)i / total : 1f;
                        if (EditorUtility.DisplayCancelableProgressBar("资源优化扫描", $"正在检查 ({i + 1}/{total}): {assetPath}", progress))
                        {
                            report.WasCancelled = true;
                            break;
                        }
                    }

                    report.ScannedAssetCount++;
                    long fileSize = GetFileSizeBytes(assetPath);
                    string ext = Path.GetExtension(assetPath);

                    if (settings.CheckLargeFiles)
                    {
                        CheckLargeFile(assetPath, fileSize, settings, report.Issues);
                    }

                    if (settings.CheckTextures && TextureExtensions.Contains(ext))
                    {
                        CheckTexture(assetPath, fileSize, settings, report.Issues);
                    }
                    else if (settings.CheckAudio && AudioExtensions.Contains(ext))
                    {
                        CheckAudio(assetPath, fileSize, settings, report.Issues);
                    }
                    else if (settings.CheckModels && ModelExtensions.Contains(ext))
                    {
                        CheckModel(assetPath, fileSize, settings, report.Issues);
                    }
                    else if (settings.CheckPrefabsAndMaterials && ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        CheckPrefab(assetPath, fileSize, settings, report.Issues);
                    }
                    else if (settings.CheckPrefabsAndMaterials && ext.Equals(".mat", StringComparison.OrdinalIgnoreCase))
                    {
                        CheckMaterial(assetPath, fileSize, settings, report.Issues);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                report.ElapsedSeconds = EditorApplication.timeSinceStartup - startTime;
            }

            report.Issues.Sort((a, b) =>
            {
                int sev = b.Severity.CompareTo(a.Severity);
                if (sev != 0) return sev;
                int size = b.FileSizeBytes.CompareTo(a.FileSizeBytes);
                if (size != 0) return size;
                return string.Compare(a.AssetPath, b.AssetPath, StringComparison.OrdinalIgnoreCase);
            });

            return report;
        }

        public static AssetOptimizeBatchResult Optimize(IEnumerable<AssetIssueItem> items, AssetOptimizeSettings settings)
        {
            var result = new AssetOptimizeBatchResult();
            if (items == null || settings == null) return result;

            settings.Sanitize();
            var grouped = items
                .Where(x => x != null && x.CanAutoFix && x.Selected && !x.IsFixed)
                .GroupBy(x => x.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (grouped.Count == 0) return result;

            bool needSaveAssets = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < grouped.Count; i++)
                {
                    var group = grouped[i];
                    string assetPath = group.Key;
                    EditorUtility.DisplayProgressBar("批量优化资源", $"正在优化 ({i + 1}/{grouped.Count}): {assetPath}", (float)(i + 1) / grouped.Count);

                    try
                    {
                        var issueList = group.ToList();
                        int fixedCount = ApplyFixesForAsset(assetPath, issueList, settings, out bool modifiedAssetDatabase);
                        if (fixedCount > 0)
                        {
                            result.FixedIssueCount += fixedCount;
                            result.ModifiedAssetCount++;
                            if (modifiedAssetDatabase) needSaveAssets = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedAssetCount++;
                        result.Errors.Add($"{assetPath}: {ex.Message}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            if (needSaveAssets)
            {
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            return result;
        }

        public static void ExportCsv(AssetScanReport report, string outputPath)
        {
            if (report == null || string.IsNullOrEmpty(outputPath)) return;

            var sb = new StringBuilder();
            sb.AppendLine("严重级别,分类,规则,资源名称,资源路径,文件大小,问题描述,优化建议,可自动优化,已优化");
            foreach (var item in report.Issues)
            {
                sb.Append(CsvEscape(item.Severity.ToString())).Append(',')
                  .Append(CsvEscape(item.Category.ToString())).Append(',')
                  .Append(CsvEscape(item.RuleId.ToString())).Append(',')
                  .Append(CsvEscape(item.AssetName)).Append(',')
                  .Append(CsvEscape(item.AssetPath)).Append(',')
                  .Append(CsvEscape(item.FileSizeText)).Append(',')
                  .Append(CsvEscape(item.IssueSummary)).Append(',')
                  .Append(CsvEscape(item.FixDescription)).Append(',')
                  .Append(item.CanAutoFix ? "是" : "否").Append(',')
                  .Append(item.IsFixed ? "是" : "否").AppendLine();
            }

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(true));
        }

        private static void CheckLargeFile(string assetPath, long fileSize, AssetOptimizeSettings settings, List<AssetIssueItem> issues)
        {
            long thresholdBytes = (long)(settings.LargeFileThresholdMB * 1024f * 1024f);
            if (fileSize < thresholdBytes) return;

            var severity = fileSize >= thresholdBytes * 2 ? AssetIssueSeverity.Error : AssetIssueSeverity.Warning;
            issues.Add(CreateIssue(
                assetPath,
                AssetOptimizeCategory.LargeFile,
                severity,
                AssetOptimizeRuleId.LargeAssetFile,
                $"文件体积 {AssetIssueItem.FormatBytes(fileSize)} 超过阈值 {settings.LargeFileThresholdMB:0.#} MB",
                "请人工确认是否需要压缩、拆分或精简源文件",
                fileSize,
                canAutoFix: false,
                defaultSelected: false));
        }

        private static void CheckTexture(string assetPath, long fileSize, AssetOptimizeSettings settings, List<AssetIssueItem> issues)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.GetSourceTextureWidthAndHeight(out int srcWidth, out int srcHeight);
            int maxSrcDim = Mathf.Max(srcWidth, srcHeight);

            if (settings.CheckTextureReadWrite && importer.isReadable)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Texture,
                    AssetIssueSeverity.Error,
                    AssetOptimizeRuleId.TextureReadWrite,
                    "开启了 Read/Write Enabled，运行时会额外占用一倍内存",
                    "关闭 Read/Write Enabled",
                    fileSize));
            }

            bool isUiTexture = importer.textureType == TextureImporterType.Sprite || importer.textureType == TextureImporterType.GUI;
            if (settings.CheckTextureUiMipMap && isUiTexture && importer.mipmapEnabled)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Texture,
                    AssetIssueSeverity.Warning,
                    AssetOptimizeRuleId.TextureUiMipMap,
                    "UI/Sprite 纹理开启了 MipMap，会增加约 33% 显存占用",
                    "关闭 Generate Mip Maps",
                    fileSize));
            }

            if (importer.maxTextureSize > settings.MaxTextureSizeLimit && maxSrcDim > settings.MaxTextureSizeLimit)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Texture,
                    AssetIssueSeverity.Warning,
                    AssetOptimizeRuleId.TextureMaxSize,
                    $"纹理尺寸 {srcWidth}x{srcHeight} 且 MaxSize={importer.maxTextureSize}，超过上限 {settings.MaxTextureSizeLimit}",
                    $"将 MaxSize 调整为 {settings.MaxTextureSizeLimit}",
                    fileSize));
            }

            if (settings.CheckTextureUncompressed && importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Texture,
                    AssetIssueSeverity.Error,
                    AssetOptimizeRuleId.TextureUncompressed,
                    "纹理未启用压缩 (Uncompressed)，显存与包体占用过高",
                    "改为 Normal Quality 压缩",
                    fileSize));
            }

            if (settings.CheckTextureUnusedAlpha &&
                importer.textureType != TextureImporterType.NormalMap &&
                !importer.DoesSourceTextureHaveAlpha() &&
                importer.alphaSource != TextureImporterAlphaSource.None)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Texture,
                    AssetIssueSeverity.Info,
                    AssetOptimizeRuleId.TextureUnusedAlpha,
                    "源图不包含透明通道，但 Alpha Source 未设为 None",
                    "将 Alpha Source 设为 None",
                    fileSize));
            }

            if (settings.CheckTextureNpot &&
                !isUiTexture &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                srcWidth > 0 && srcHeight > 0 &&
                (!Mathf.IsPowerOfTwo(srcWidth) || !Mathf.IsPowerOfTwo(srcHeight)))
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Texture,
                    AssetIssueSeverity.Info,
                    AssetOptimizeRuleId.TextureNonPowerOfTwo,
                    $"非 UI 纹理尺寸 {srcWidth}x{srcHeight} 为非 2 的幂次且 NPOT=None",
                    "将 Non-Power of 2 设为 ToNearest",
                    fileSize));
            }
        }

        private static void CheckAudio(string assetPath, long fileSize, AssetOptimizeSettings settings, List<AssetIssueItem> issues)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (importer == null || clip == null) return;

            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            float length = clip.length;

            if (length >= settings.LongAudioSeconds)
            {
                if (sample.loadType != AudioClipLoadType.Streaming)
                {
                    issues.Add(CreateIssue(
                        assetPath,
                        AssetOptimizeCategory.Audio,
                        AssetIssueSeverity.Warning,
                        AssetOptimizeRuleId.AudioLongNotStreaming,
                        $"长音频 ({length:0.1}s) 当前为 {sample.loadType}，易造成内存峰值",
                        "Load Type 改为 Streaming",
                        fileSize));
                }

                if (sample.compressionFormat == AudioCompressionFormat.PCM)
                {
                    issues.Add(CreateIssue(
                        assetPath,
                        AssetOptimizeCategory.Audio,
                        AssetIssueSeverity.Error,
                        AssetOptimizeRuleId.AudioLongPcmCompression,
                        $"长音频 ({length:0.1}s) 使用未压缩 PCM 格式",
                        "Compression Format 改为 Vorbis",
                        fileSize));
                }
            }
            else if (length <= settings.ShortAudioSeconds)
            {
                if (sample.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    issues.Add(CreateIssue(
                        assetPath,
                        AssetOptimizeCategory.Audio,
                        AssetIssueSeverity.Info,
                        AssetOptimizeRuleId.AudioShortNotDecompressOnLoad,
                        $"短音效 ({length:0.2}s) 当前为 {sample.loadType}，播放时存在解码开销",
                        "Load Type 改为 DecompressOnLoad",
                        fileSize));
                }
            }
            else
            {
                if (sample.loadType != AudioClipLoadType.CompressedInMemory)
                {
                    issues.Add(CreateIssue(
                        assetPath,
                        AssetOptimizeCategory.Audio,
                        AssetIssueSeverity.Info,
                        AssetOptimizeRuleId.AudioMediumNotCompressedInMemory,
                        $"中等时长音频 ({length:0.1}s) 当前为 {sample.loadType}",
                        "Load Type 改为 CompressedInMemory",
                        fileSize));
                }
            }

            if (settings.CheckAudioForceToMono && clip.channels > 1 && length < settings.LongAudioSeconds && !importer.forceToMono)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Audio,
                    AssetIssueSeverity.Info,
                    AssetOptimizeRuleId.AudioForceToMono,
                    $"短/中音效为 {clip.channels} 声道，未开启 Force To Mono",
                    "开启 Force To Mono 减少约 50% 音频体积",
                    fileSize));
            }
        }

        private static void CheckModel(string assetPath, long fileSize, AssetOptimizeSettings settings, List<AssetIssueItem> issues)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            if (settings.CheckModelReadWrite && importer.isReadable)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Model,
                    AssetIssueSeverity.Error,
                    AssetOptimizeRuleId.ModelReadWrite,
                    "模型开启了 Read/Write Enabled，运行时额外占用一倍网格内存",
                    "关闭 Read/Write Enabled",
                    fileSize));
            }

            if (settings.CheckModelMeshCompression && importer.meshCompression == ModelImporterMeshCompression.Off)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Model,
                    AssetIssueSeverity.Warning,
                    AssetOptimizeRuleId.ModelMeshCompression,
                    "模型网格压缩 (Mesh Compression) 为 Off",
                    "Mesh Compression 改为 Medium",
                    fileSize));
            }

            if (settings.CheckModelCamerasAndLights && (importer.importCameras || importer.importLights))
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Model,
                    AssetIssueSeverity.Info,
                    AssetOptimizeRuleId.ModelImportCamerasAndLights,
                    "模型开启了 Import Cameras 或 Import Lights",
                    "关闭 Import Cameras 和 Import Lights",
                    fileSize));
            }

            if (settings.CheckModelOptimizeMesh && (!importer.optimizeMeshPolygons || !importer.optimizeMeshVertices))
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Model,
                    AssetIssueSeverity.Info,
                    AssetOptimizeRuleId.ModelOptimizeMesh,
                    "模型未完整开启网格顶点/多边形顺序优化",
                    "开启 Optimize Mesh Polygons & Vertices",
                    fileSize));
            }

            if (settings.CheckModelAnimationCompression &&
                importer.importAnimation &&
                importer.animationCompression == ModelImporterAnimationCompression.Off)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.Model,
                    AssetIssueSeverity.Warning,
                    AssetOptimizeRuleId.ModelAnimationCompression,
                    "模型导入动画但 Animation Compression 为 Off",
                    "Animation Compression 改为 Optimal",
                    fileSize));
            }
        }

        private static void CheckPrefab(string assetPath, long fileSize, AssetOptimizeSettings settings, List<AssetIssueItem> issues)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) return;

            if (settings.CheckPrefabMissingScripts)
            {
                int missingCount = CountMissingScripts(prefab);
                if (missingCount > 0)
                {
                    issues.Add(CreateIssue(
                        assetPath,
                        AssetOptimizeCategory.PrefabAndMaterial,
                        AssetIssueSeverity.Error,
                        AssetOptimizeRuleId.PrefabMissingScript,
                        $"预制体包含 {missingCount} 个 Missing Script 组件",
                        "清理全部 Missing Script（默认不勾选，请确认后再执行）",
                        fileSize,
                        canAutoFix: true,
                        defaultSelected: false));
                }
            }

            if (settings.CheckPrefabUnusedRaycast)
            {
                int redundantRaycasts = FindRedundantRaycastGraphics(prefab).Count;
                if (redundantRaycasts > 0)
                {
                    issues.Add(CreateIssue(
                        assetPath,
                        AssetOptimizeCategory.PrefabAndMaterial,
                        AssetIssueSeverity.Info,
                        AssetOptimizeRuleId.PrefabUnusedRaycastTarget,
                        $"预制体中有 {redundantRaycasts} 个非交互 UI Graphic 开启了 Raycast Target",
                        "关闭非交互 Graphic 的 Raycast Target 降低 UI 射线开销",
                        fileSize));
                }
            }
        }

        private static void CheckMaterial(string assetPath, long fileSize, AssetOptimizeSettings settings, List<AssetIssueItem> issues)
        {
            if (!settings.CheckMaterialUnusedProperties) return;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                return;
            }

            int unusedCount = CountOrCleanUnusedMaterialProperties(mat, remove: false);
            if (unusedCount > 0)
            {
                issues.Add(CreateIssue(
                    assetPath,
                    AssetOptimizeCategory.PrefabAndMaterial,
                    AssetIssueSeverity.Info,
                    AssetOptimizeRuleId.MaterialUnusedProperties,
                    $"材质残留 {unusedCount} 个当前 Shader 未使用的序列化属性（可能残留贴图引用）",
                    "清理无效的 m_SavedProperties 属性",
                    fileSize));
            }
        }

        private static int ApplyFixesForAsset(string assetPath, List<AssetIssueItem> issues, AssetOptimizeSettings settings, out bool modifiedAssetDatabase)
        {
            modifiedAssetDatabase = false;
            int fixedCount = 0;

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer is TextureImporter texImporter)
            {
                bool dirty = false;
                foreach (var issue in issues)
                {
                    switch (issue.RuleId)
                    {
                        case AssetOptimizeRuleId.TextureReadWrite:
                            texImporter.isReadable = false;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.TextureUiMipMap:
                            texImporter.mipmapEnabled = false;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.TextureMaxSize:
                            texImporter.maxTextureSize = settings.MaxTextureSizeLimit;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.TextureUncompressed:
                            texImporter.textureCompression = TextureImporterCompression.Compressed;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.TextureUnusedAlpha:
                            texImporter.alphaSource = TextureImporterAlphaSource.None;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.TextureNonPowerOfTwo:
                            texImporter.npotScale = TextureImporterNPOTScale.ToNearest;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                    }
                }

                if (dirty) texImporter.SaveAndReimport();
                return fixedCount;
            }

            if (importer is AudioImporter audioImporter)
            {
                bool dirty = false;
                AudioImporterSampleSettings sample = audioImporter.defaultSampleSettings;
                bool sampleDirty = false;

                foreach (var issue in issues)
                {
                    switch (issue.RuleId)
                    {
                        case AssetOptimizeRuleId.AudioLongNotStreaming:
                            sample.loadType = AudioClipLoadType.Streaming;
                            sampleDirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.AudioLongPcmCompression:
                            sample.compressionFormat = AudioCompressionFormat.Vorbis;
                            sample.quality = Mathf.Clamp(sample.quality <= 0f ? 0.5f : sample.quality, 0.3f, 0.7f);
                            sampleDirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.AudioShortNotDecompressOnLoad:
                            sample.loadType = AudioClipLoadType.DecompressOnLoad;
                            sampleDirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.AudioMediumNotCompressedInMemory:
                            sample.loadType = AudioClipLoadType.CompressedInMemory;
                            sampleDirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.AudioForceToMono:
                            audioImporter.forceToMono = true;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                    }
                }

                if (sampleDirty)
                {
                    audioImporter.defaultSampleSettings = sample;
                    dirty = true;
                }

                if (dirty) audioImporter.SaveAndReimport();
                return fixedCount;
            }

            if (importer is ModelImporter modelImporter)
            {
                bool dirty = false;
                foreach (var issue in issues)
                {
                    switch (issue.RuleId)
                    {
                        case AssetOptimizeRuleId.ModelReadWrite:
                            modelImporter.isReadable = false;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.ModelMeshCompression:
                            modelImporter.meshCompression = ModelImporterMeshCompression.Medium;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.ModelImportCamerasAndLights:
                            modelImporter.importCameras = false;
                            modelImporter.importLights = false;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.ModelOptimizeMesh:
                            modelImporter.optimizeMeshPolygons = true;
                            modelImporter.optimizeMeshVertices = true;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                        case AssetOptimizeRuleId.ModelAnimationCompression:
                            modelImporter.animationCompression = ModelImporterAnimationCompression.Optimal;
                            dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                            break;
                    }
                }

                if (dirty) modelImporter.SaveAndReimport();
                return fixedCount;
            }

            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    bool dirty = false;
                    foreach (var issue in issues)
                    {
                        if (issue.RuleId == AssetOptimizeRuleId.PrefabMissingScript)
                        {
                            int removed = RemoveMissingScripts(root);
                            if (removed > 0) dirty = true;
                            issue.IsFixed = true;
                            fixedCount++;
                        }
                        else if (issue.RuleId == AssetOptimizeRuleId.PrefabUnusedRaycastTarget)
                        {
                            var graphics = FindRedundantRaycastGraphics(root);
                            foreach (var g in graphics)
                            {
                                g.raycastTarget = false;
                                dirty = true;
                            }

                            issue.IsFixed = true;
                            fixedCount++;
                        }
                    }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                        modifiedAssetDatabase = true;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return fixedCount;
            }

            if (assetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat != null && mat.shader != null && mat.shader.name != "Hidden/InternalErrorShader")
                {
                    foreach (var issue in issues)
                    {
                        if (issue.RuleId == AssetOptimizeRuleId.MaterialUnusedProperties)
                        {
                            int removed = CountOrCleanUnusedMaterialProperties(mat, remove: true);
                            if (removed > 0)
                            {
                                EditorUtility.SetDirty(mat);
                                modifiedAssetDatabase = true;
                            }

                            issue.IsFixed = true;
                            fixedCount++;
                        }
                    }
                }

                return fixedCount;
            }

            return fixedCount;
        }

        private static int CountMissingScripts(GameObject root)
        {
            int count = 0;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
            }

            return count;
        }

        private static int RemoveMissingScripts(GameObject root)
        {
            int removed = 0;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
            }

            return removed;
        }

        private static List<Graphic> FindRedundantRaycastGraphics(GameObject root)
        {
            var result = new List<Graphic>();
            var referenced = new HashSet<Graphic>();

            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.targetGraphic != null)
                {
                    referenced.Add(selectable.targetGraphic);
                }
            }

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget || referenced.Contains(graphic))
                {
                    continue;
                }

                GameObject go = graphic.gameObject;
                if (go.GetComponent<Selectable>() != null ||
                    go.GetComponent<IEventSystemHandler>() != null ||
                    go.GetComponent<ScrollRect>() != null ||
                    go.GetComponent<Mask>() != null ||
                    go.GetComponent<RectMask2D>() != null)
                {
                    continue;
                }

                result.Add(graphic);
            }

            return result;
        }

        private static int CountOrCleanUnusedMaterialProperties(Material mat, bool remove)
        {
            var so = new SerializedObject(mat);
            SerializedProperty savedProps = so.FindProperty("m_SavedProperties");
            if (savedProps == null) return 0;

            string[] arrayNames = { "m_TexEnvs", "m_Floats", "m_Colors", "m_Ints" };
            int unusedCount = 0;

            foreach (string arrayName in arrayNames)
            {
                SerializedProperty propArray = savedProps.FindPropertyRelative(arrayName);
                if (propArray == null || !propArray.isArray) continue;

                for (int i = propArray.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty element = propArray.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp = element.FindPropertyRelative("first");
                    if (nameProp == null) continue;

                    string propName = nameProp.stringValue;
                    if (!mat.HasProperty(propName))
                    {
                        unusedCount++;
                        if (remove)
                        {
                            propArray.DeleteArrayElementAtIndex(i);
                        }
                    }
                }
            }

            if (remove && unusedCount > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return unusedCount;
        }

        private static AssetIssueItem CreateIssue(
            string assetPath,
            AssetOptimizeCategory category,
            AssetIssueSeverity severity,
            AssetOptimizeRuleId ruleId,
            string summary,
            string fixDescription,
            long fileSizeBytes,
            bool canAutoFix = true,
            bool defaultSelected = true)
        {
            return new AssetIssueItem
            {
                AssetPath = assetPath,
                AssetName = Path.GetFileName(assetPath),
                Category = category,
                Severity = severity,
                RuleId = ruleId,
                IssueSummary = summary,
                FixDescription = fixDescription,
                FileSizeBytes = fileSizeBytes,
                CanAutoFix = canAutoFix,
                Selected = canAutoFix && defaultSelected,
                IsFixed = false
            };
        }

        private static bool IsExcluded(string assetPath, string scanRoot, string[] excludes)
        {
            if (excludes == null || excludes.Length == 0) return false;

            string relative = assetPath.StartsWith(scanRoot, StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(scanRoot.Length).TrimStart('/')
                : assetPath;

            string[] segments = relative.Split('/');
            for (int i = 0; i < segments.Length - 1; i++)
            {
                for (int j = 0; j < excludes.Length; j++)
                {
                    string ex = excludes[j].Trim();
                    if (!string.IsNullOrEmpty(ex) && segments[i].Equals(ex, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static long GetFileSizeBytes(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(assetPath);
                if (File.Exists(fullPath))
                {
                    return new FileInfo(fullPath).Length;
                }
            }
            catch
            {
                // Ignore IO errors for virtual or missing assets.
            }

            return 0;
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
