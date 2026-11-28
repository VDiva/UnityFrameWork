using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor.AssetOptimizer
{
    public sealed class AssetOptimizeWindow : EditorWindow
    {
        private const int PageSize = 50;
        private static readonly int[] TextureSizeOptions = { 256, 512, 1024, 2048, 4096 };
        private static readonly string[] TextureSizeLabels = { "256", "512", "1024", "2048", "4096" };

        private static readonly string[] CategoryLabels =
        {
            "全部", "纹理", "音频", "模型", "预制体/材质", "大文件"
        };

        private static readonly string[] SeverityFilterLabels =
        {
            "全部级别", "仅 Error", "Warning+", "仅 Info"
        };

        private AssetOptimizeSettings _settings;
        private AssetScanReport _report;
        private readonly List<AssetIssueItem> _filteredIssues = new List<AssetIssueItem>();

        private AssetOptimizeCategory _selectedCategory = AssetOptimizeCategory.All;
        private int _severityFilterIndex;
        private bool _onlyAutoFixable;
        private bool _hideFixed = true;
        private string _searchText = string.Empty;

        private bool _showSettings;
        private Vector2 _scrollPos;
        private int _currentPage;
        private string _statusMessage = "点击「开始扫描」检查项目资源。";

        private GUIStyle _badgeStyle;
        private GUIStyle _pathStyle;
        private GUIStyle _richWrapStyle;

        [MenuItem("FrameWork/资源优化面板", priority = 20)]
        public static void OpenWindow()
        {
            var window = GetWindow<AssetOptimizeWindow>("资源优化面板");
            window.minSize = new Vector2(920f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = AssetOptimizeSettings.Load();
        }

        private void EnsureStyles()
        {
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 20f
                };
            }

            if (_pathStyle == null)
            {
                _pathStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
                };
            }

            if (_richWrapStyle == null)
            {
                _richWrapStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true
                };
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_settings == null) _settings = AssetOptimizeSettings.Load();

            DrawTopToolbar();
            DrawSettingsPanel();
            DrawSummaryBar();
            DrawFilterBar();
            DrawIssueList();
            DrawFooterBar();
        }

        private void DrawTopToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("扫描目录", GUILayout.Width(58f));
            string newRoot = EditorGUILayout.TextField(_settings.ScanRootFolder);
            if (newRoot != _settings.ScanRootFolder)
            {
                _settings.ScanRootFolder = newRoot;
                _settings.Save();
            }

            if (GUILayout.Button("选择...", GUILayout.Width(58f)))
            {
                string initialDir = Path.GetFullPath(
                    AssetDatabase.IsValidFolder(_settings.ScanRootFolder) ? _settings.ScanRootFolder : "Assets");
                string picked = EditorUtility.OpenFolderPanel("选择要扫描的 Assets 目录", initialDir, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    string assetRelative = ToAssetRelativePath(picked);
                    if (!string.IsNullOrEmpty(assetRelative))
                    {
                        _settings.ScanRootFolder = assetRelative;
                        _settings.Save();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("目录无效", "请选择当前 Unity 工程 Assets 目录下的文件夹。", "确定");
                    }
                }
            }

            if (GUILayout.Button("FrameWork/Asset", GUILayout.Width(112f)))
            {
                _settings.ScanRootFolder = "Assets/FrameWork/Asset";
                _settings.Save();
            }

            if (GUILayout.Button("全项目 Assets", GUILayout.Width(90f)))
            {
                _settings.ScanRootFolder = "Assets";
                _settings.Save();
            }

            GUILayout.Space(8f);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
            if (GUILayout.Button("开始扫描", GUILayout.Width(86f), GUILayout.Height(22f)))
            {
                RunScan();
            }

            int selectedFixableCount = CountSelectedFixableIssues();
            GUI.backgroundColor = selectedFixableCount > 0
                ? new Color(0.95f, 0.72f, 0.32f)
                : oldBg;
            using (new EditorGUI.DisabledScope(selectedFixableCount == 0))
            {
                if (GUILayout.Button($"一键优化 ({selectedFixableCount})", GUILayout.Width(112f), GUILayout.Height(22f)))
                {
                    RunBatchOptimize();
                }
            }

            GUI.backgroundColor = oldBg;

            using (new EditorGUI.DisabledScope(_report == null || _report.Issues.Count == 0))
            {
                if (GUILayout.Button("导出 CSV", GUILayout.Width(72f), GUILayout.Height(22f)))
                {
                    ExportReportCsv();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSettingsPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            _showSettings = EditorGUILayout.Foldout(_showSettings, "检查规则与阈值配置", true);
            GUILayout.FlexibleSpace();
            if (_showSettings && GUILayout.Button("恢复默认", GUILayout.Width(72f)))
            {
                _settings.ResetToDefaults();
            }
            EditorGUILayout.EndHorizontal();

            if (_showSettings)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.Space(2f);

                _settings.ExcludeFolders = EditorGUILayout.TextField("排除目录 (分号分隔)", _settings.ExcludeFolders);

                EditorGUILayout.BeginHorizontal();
                // Left column: Texture & Audio
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _settings.CheckTextures = EditorGUILayout.ToggleLeft("检查纹理 (Texture)", _settings.CheckTextures, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!_settings.CheckTextures))
                {
                    int sizeIdx = Array.IndexOf(TextureSizeOptions, _settings.MaxTextureSizeLimit);
                    if (sizeIdx < 0) sizeIdx = 2;
                    sizeIdx = EditorGUILayout.Popup("最大尺寸上限", sizeIdx, TextureSizeLabels);
                    _settings.MaxTextureSizeLimit = TextureSizeOptions[sizeIdx];

                    _settings.CheckTextureReadWrite = EditorGUILayout.Toggle("检查 Read/Write Enabled", _settings.CheckTextureReadWrite);
                    _settings.CheckTextureUiMipMap = EditorGUILayout.Toggle("检查 UI/Sprite MipMap", _settings.CheckTextureUiMipMap);
                    _settings.CheckTextureUncompressed = EditorGUILayout.Toggle("检查未压缩纹理", _settings.CheckTextureUncompressed);
                    _settings.CheckTextureUnusedAlpha = EditorGUILayout.Toggle("检查无效 Alpha 通道", _settings.CheckTextureUnusedAlpha);
                    _settings.CheckTextureNpot = EditorGUILayout.Toggle("检查非 2 的幂次 (非 UI)", _settings.CheckTextureNpot);
                }

                EditorGUILayout.Space(4f);
                _settings.CheckAudio = EditorGUILayout.ToggleLeft("检查音频 (Audio)", _settings.CheckAudio, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!_settings.CheckAudio))
                {
                    _settings.ShortAudioSeconds = EditorGUILayout.FloatField("短音效阈值 (秒)", _settings.ShortAudioSeconds);
                    _settings.LongAudioSeconds = EditorGUILayout.FloatField("长音频阈值 (秒)", _settings.LongAudioSeconds);
                    _settings.CheckAudioForceToMono = EditorGUILayout.Toggle("短/中音效建议单声道", _settings.CheckAudioForceToMono);
                }
                EditorGUILayout.EndVertical();

                // Right column: Model, Prefab/Material, Large File
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _settings.CheckModels = EditorGUILayout.ToggleLeft("检查模型 (Model)", _settings.CheckModels, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!_settings.CheckModels))
                {
                    _settings.CheckModelReadWrite = EditorGUILayout.Toggle("检查 Read/Write Enabled", _settings.CheckModelReadWrite);
                    _settings.CheckModelMeshCompression = EditorGUILayout.Toggle("检查 Mesh Compression", _settings.CheckModelMeshCompression);
                    _settings.CheckModelCamerasAndLights = EditorGUILayout.Toggle("检查导入相机与灯光", _settings.CheckModelCamerasAndLights);
                    _settings.CheckModelOptimizeMesh = EditorGUILayout.Toggle("检查 Optimize Mesh", _settings.CheckModelOptimizeMesh);
                    _settings.CheckModelAnimationCompression = EditorGUILayout.Toggle("检查动画压缩", _settings.CheckModelAnimationCompression);
                }

                EditorGUILayout.Space(4f);
                _settings.CheckPrefabsAndMaterials = EditorGUILayout.ToggleLeft("检查预制体与材质", _settings.CheckPrefabsAndMaterials, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!_settings.CheckPrefabsAndMaterials))
                {
                    _settings.CheckPrefabMissingScripts = EditorGUILayout.Toggle("检查 Missing Script", _settings.CheckPrefabMissingScripts);
                    _settings.CheckPrefabUnusedRaycast = EditorGUILayout.Toggle("检查冗余 Raycast Target", _settings.CheckPrefabUnusedRaycast);
                    _settings.CheckMaterialUnusedProperties = EditorGUILayout.Toggle("检查材质无效属性", _settings.CheckMaterialUnusedProperties);
                }

                EditorGUILayout.Space(4f);
                _settings.CheckLargeFiles = EditorGUILayout.ToggleLeft("检查大体积资源", _settings.CheckLargeFiles, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!_settings.CheckLargeFiles))
                {
                    _settings.LargeFileThresholdMB = EditorGUILayout.FloatField("大文件阈值 (MB)", _settings.LargeFileThresholdMB);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    _settings.Save();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSummaryBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (_report == null)
            {
                EditorGUILayout.LabelField(_statusMessage);
            }
            else
            {
                int err = _report.Issues.Count(x => !x.IsFixed && x.Severity == AssetIssueSeverity.Error);
                int warn = _report.Issues.Count(x => !x.IsFixed && x.Severity == AssetIssueSeverity.Warning);
                int info = _report.Issues.Count(x => !x.IsFixed && x.Severity == AssetIssueSeverity.Info);
                int fixedCount = _report.Issues.Count(x => x.IsFixed);

                EditorGUILayout.LabelField(
                    $"已扫描: {_report.ScannedAssetCount} 个资源  |  耗时: {_report.ElapsedSeconds:0.2}s  |  " +
                    $"<color=#ff6b6b><b>Error: {err}</b></color>   " +
                    $"<color=#ffd166><b>Warning: {warn}</b></color>   " +
                    $"<color=#64b5f6><b>Info: {info}</b></color>   " +
                    $"<color=#81c784><b>已优化: {fixedCount}</b></color>",
                    _richWrapStyle);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Category tabs with live counts
            string[] tabLabels = new string[CategoryLabels.Length];
            for (int i = 0; i < CategoryLabels.Length; i++)
            {
                int count = CountCategoryIssues((AssetOptimizeCategory)i);
                tabLabels[i] = count > 0 ? $"{CategoryLabels[i]} ({count})" : CategoryLabels[i];
            }

            EditorGUI.BeginChangeCheck();
            _selectedCategory = (AssetOptimizeCategory)GUILayout.Toolbar((int)_selectedCategory, tabLabels);

            EditorGUILayout.BeginHorizontal();
            _severityFilterIndex = EditorGUILayout.Popup(_severityFilterIndex, SeverityFilterLabels, GUILayout.Width(95f));
            _onlyAutoFixable = GUILayout.Toggle(_onlyAutoFixable, "仅可自动优化", GUILayout.Width(95f));
            _hideFixed = GUILayout.Toggle(_hideFixed, "隐藏已优化", GUILayout.Width(82f));

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(36f));
            _searchText = EditorGUILayout.TextField(_searchText);
            if (!string.IsNullOrEmpty(_searchText) && GUILayout.Button("×", GUILayout.Width(22f)))
            {
                _searchText = string.Empty;
                GUI.FocusControl(null);
            }

            if (EditorGUI.EndChangeCheck())
            {
                _currentPage = 0;
                RebuildFilteredList();
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("全选当前", GUILayout.Width(66f)))
            {
                SetSelectionForFiltered(true);
            }
            if (GUILayout.Button("取消全选", GUILayout.Width(66f)))
            {
                SetSelectionForFiltered(false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawIssueList()
        {
            if (_report == null)
            {
                GUILayout.FlexibleSpace();
                return;
            }

            if (_filteredIssues.Count == 0)
            {
                EditorGUILayout.HelpBox("当前筛选条件下没有待优化项。", UnityEditor.MessageType.Info);
                GUILayout.FlexibleSpace();
                return;
            }

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(_filteredIssues.Count / (float)PageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, totalPages - 1);
            int startIndex = _currentPage * PageSize;
            int endIndex = Mathf.Min(startIndex + PageSize, _filteredIssues.Count);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            for (int i = startIndex; i < endIndex; i++)
            {
                DrawIssueRow(_filteredIssues[i]);
            }
            EditorGUILayout.EndScrollView();

            if (totalPages > 1)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_currentPage <= 0))
                {
                    if (GUILayout.Button("上一页", GUILayout.Width(68f))) _currentPage--;
                }
                EditorGUILayout.LabelField($"第 {_currentPage + 1} / {totalPages} 页 (共 {_filteredIssues.Count} 条)", GUILayout.Width(160f));
                using (new EditorGUI.DisabledScope(_currentPage >= totalPages - 1))
                {
                    if (GUILayout.Button("下一页", GUILayout.Width(68f))) _currentPage++;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawIssueRow(AssetIssueItem item)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Checkbox
            using (new EditorGUI.DisabledScope(!item.CanAutoFix || item.IsFixed))
            {
                item.Selected = EditorGUILayout.Toggle(item.Selected, GUILayout.Width(18f));
            }

            // Severity badge
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = GetSeverityColor(item.Severity, item.IsFixed);
            GUILayout.Label(item.IsFixed ? "FIXED" : item.Severity.ToString().ToUpperInvariant(), _badgeStyle, GUILayout.Width(62f));
            GUI.backgroundColor = oldBg;

            // Asset info & issue details
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(item.AssetName, EditorStyles.boldLabel, GUILayout.MaxWidth(260f));
            EditorGUILayout.LabelField(item.FileSizeText, EditorStyles.miniLabel, GUILayout.Width(72f));
            EditorGUILayout.LabelField(item.AssetPath, _pathStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"问题: {item.IssueSummary}   |   建议: {item.FixDescription}", _richWrapStyle);
            EditorGUILayout.EndVertical();

            // Actions
            EditorGUILayout.BeginVertical(GUILayout.Width(124f));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位", GUILayout.Width(52f), GUILayout.Height(24f)))
            {
                PingAsset(item.AssetPath);
            }

            if (item.IsFixed)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("已优化", GUILayout.Width(64f), GUILayout.Height(24f));
                }
            }
            else if (item.CanAutoFix)
            {
                if (GUILayout.Button("优化", GUILayout.Width(64f), GUILayout.Height(24f)))
                {
                    RunSingleOptimize(item);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("人工处理", GUILayout.Width(64f), GUILayout.Height(24f));
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(_statusMessage, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"当前显示: {_filteredIssues.Count} 条", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void RunScan()
        {
            _settings.Save();
            if (!AssetDatabase.IsValidFolder(_settings.ScanRootFolder))
            {
                EditorUtility.DisplayDialog("目录不存在", $"扫描目录无效：{_settings.ScanRootFolder}", "确定");
                return;
            }

            _report = AssetOptimizeScanner.Scan(_settings);
            _currentPage = 0;
            RebuildFilteredList();

            _statusMessage = _report.WasCancelled
                ? $"扫描已取消：已检查 {_report.ScannedAssetCount} 个资源，发现 {_report.Issues.Count} 条问题。"
                : $"扫描完成：共检查 {_report.ScannedAssetCount} 个资源，发现 {_report.Issues.Count} 条优化项（耗时 {_report.ElapsedSeconds:0.2}s）。";
        }

        private void RunBatchOptimize()
        {
            if (_report == null) return;

            var targets = _report.Issues.Where(x => x.CanAutoFix && x.Selected && !x.IsFixed).ToList();
            if (targets.Count == 0) return;

            int assetCount = targets.Select(x => x.AssetPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            bool confirmed = EditorUtility.DisplayDialog(
                "确认批量优化",
                $"即将优化 {targets.Count} 条问题（涉及 {assetCount} 个资源文件）。\n建议在执行前确保版本控制（SVN/Git）工作区干净。\n\n是否继续？",
                "执行优化",
                "取消");

            if (!confirmed) return;

            var result = AssetOptimizeScanner.Optimize(targets, _settings);
            RebuildFilteredList();

            _statusMessage = $"批量优化完成：已修复 {result.FixedIssueCount} 条问题（修改 {result.ModifiedAssetCount} 个资源，失败 {result.FailedAssetCount} 个）。";
            if (result.Errors.Count > 0)
            {
                Debug.LogWarning("[AssetOptimizer] 部分资源优化失败：\n" + string.Join("\n", result.Errors));
            }
        }

        private void RunSingleOptimize(AssetIssueItem item)
        {
            if (item == null || !item.CanAutoFix || item.IsFixed) return;

            bool prevSelected = item.Selected;
            item.Selected = true;
            var result = AssetOptimizeScanner.Optimize(new[] { item }, _settings);
            if (!item.IsFixed) item.Selected = prevSelected;

            RebuildFilteredList();
            _statusMessage = result.FailedAssetCount == 0
                ? $"已优化: {item.AssetPath}"
                : $"优化失败: {item.AssetPath} ({string.Join("; ", result.Errors)})";
        }

        private void ExportReportCsv()
        {
            if (_report == null || _report.Issues.Count == 0) return;

            string defaultName = $"AssetOptimizeReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string savePath = EditorUtility.SaveFilePanel("导出资源优化报告", Application.dataPath, defaultName, "csv");
            if (string.IsNullOrEmpty(savePath)) return;

            AssetOptimizeScanner.ExportCsv(_report, savePath);
            _statusMessage = $"报告已导出：{savePath}";
            EditorUtility.RevealInFinder(savePath);
        }

        private void RebuildFilteredList()
        {
            _filteredIssues.Clear();
            if (_report == null) return;

            string query = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText.Trim();
            foreach (var item in _report.Issues)
            {
                if (_hideFixed && item.IsFixed) continue;
                if (_onlyAutoFixable && !item.CanAutoFix) continue;
                if (_selectedCategory != AssetOptimizeCategory.All && item.Category != _selectedCategory) continue;

                if (_severityFilterIndex == 1 && item.Severity != AssetIssueSeverity.Error) continue;
                if (_severityFilterIndex == 2 && item.Severity < AssetIssueSeverity.Warning) continue;
                if (_severityFilterIndex == 3 && item.Severity != AssetIssueSeverity.Info) continue;

                if (query != null &&
                    item.AssetPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    item.IssueSummary.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    item.FixDescription.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _filteredIssues.Add(item);
            }
        }

        private void SetSelectionForFiltered(bool selected)
        {
            foreach (var item in _filteredIssues)
            {
                if (item.CanAutoFix && !item.IsFixed)
                {
                    item.Selected = selected;
                }
            }
        }

        private int CountSelectedFixableIssues()
        {
            if (_report == null) return 0;
            int count = 0;
            foreach (var item in _report.Issues)
            {
                if (item.CanAutoFix && item.Selected && !item.IsFixed) count++;
            }
            return count;
        }

        private int CountCategoryIssues(AssetOptimizeCategory category)
        {
            if (_report == null) return 0;
            int count = 0;
            foreach (var item in _report.Issues)
            {
                if (_hideFixed && item.IsFixed) continue;
                if (category == AssetOptimizeCategory.All || item.Category == category) count++;
            }
            return count;
        }

        private static Color GetSeverityColor(AssetIssueSeverity severity, bool isFixed)
        {
            if (isFixed) return new Color(0.45f, 0.82f, 0.50f);
            switch (severity)
            {
                case AssetIssueSeverity.Error: return new Color(0.95f, 0.42f, 0.42f);
                case AssetIssueSeverity.Warning: return new Color(0.96f, 0.78f, 0.35f);
                default: return new Color(0.42f, 0.72f, 0.96f);
            }
        }

        private static void PingAsset(string assetPath)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        private static string ToAssetRelativePath(string fullPath)
        {
            string normalizedFull = Path.GetFullPath(fullPath).Replace('\\', '/').TrimEnd('/');
            string normalizedAssets = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');

            if (normalizedFull.Equals(normalizedAssets, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (normalizedFull.StartsWith(normalizedAssets + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + normalizedFull.Substring(normalizedAssets.Length);
            }

            return null;
        }
    }
}
