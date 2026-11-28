using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor.SteamDeploy
{
    /// <summary>
    /// Steam 打包与上传编辑器面板：
    /// 支持单独打包、从历史打包列表中选择指定版本上传、以及一键打包并上传。
    /// </summary>
    public sealed class SteamDeployWindow : EditorWindow
    {
        private static readonly BuildTarget[] SupportedTargets =
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneLinux64,
            BuildTarget.StandaloneOSX
        };

        private static readonly string[] SupportedTargetLabels =
        {
            "Windows 64-bit",
            "Linux 64-bit",
            "macOS"
        };

        private readonly SteamDeployService _service = new SteamDeployService();
        private readonly ConcurrentQueue<string> _pendingLogs = new ConcurrentQueue<string>();
        private readonly StringBuilder _logBuilder = new StringBuilder(4096);

        private SteamDeployConfig _config;
        private List<SteamBuildEntry> _builds = new List<SteamBuildEntry>();
        private string _selectedBuildPath;
        private string _password = string.Empty;
        private string _guardCode = string.Empty;
        private bool _rememberPassword;
        private bool _accountVerified;
        private bool _showConfigFoldout = true;
        private bool _autoScrollLog = true;
        private string _statusMessage = "就绪";
        private bool _statusIsError;
        private Vector2 _buildsScroll;
        private Vector2 _logScroll;

        private Action _pendingMainThreadAction;
        private readonly object _actionGate = new object();

        [MenuItem("FrameWork/Steam 打包上传面板", priority = 21)]
        public static void OpenWindow()
        {
            var window = GetWindow<SteamDeployWindow>("Steam 打包上传");
            window.minSize = new Vector2(680f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            _config = SteamDeployConfig.Load();
            _rememberPassword = SteamDeployConfig.RememberPassword;
            _password = SteamDeployConfig.LoadSavedPassword();
            RefreshBuilds(selectLatestIfEmpty: true);
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _config?.Save();
            SteamDeployConfig.RememberPassword = _rememberPassword;
            SteamDeployConfig.SavePassword(_password);
        }

        private void OnEditorUpdate()
        {
            bool dirty = false;
            while (_pendingLogs.TryDequeue(out string line))
            {
                if (_logBuilder.Length > 32000)
                    _logBuilder.Remove(0, 8000);
                _logBuilder.AppendLine(line);
                dirty = true;
            }

            Action mainAction = null;
            lock (_actionGate)
            {
                if (_pendingMainThreadAction != null)
                {
                    mainAction = _pendingMainThreadAction;
                    _pendingMainThreadAction = null;
                }
            }
            if (mainAction != null)
            {
                mainAction.Invoke();
                dirty = true;
            }

            if (dirty)
            {
                if (_autoScrollLog)
                    _logScroll.y = float.MaxValue;
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (_config == null)
                _config = SteamDeployConfig.Load();

            DrawTopActionBar();
            EditorGUILayout.Space(4f);
            DrawConfigSection();
            EditorGUILayout.Space(4f);
            DrawBuildsListSection();
            EditorGUILayout.Space(4f);
            DrawLogSection();
        }

        private void DrawTopActionBar()
        {
            bool uploading = _service.IsUploading;
            SteamBuildEntry selected = GetSelectedBuild();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(uploading))
                    {
                        if (GUILayout.Button("仅打包 (Build)", GUILayout.Height(30f)))
                            RunBuildOnly();

                        using (new EditorGUI.DisabledScope(selected == null))
                        {
                            string uploadLabel = selected != null
                                ? $"上传选中包 ({selected.FolderName})"
                                : "上传选中包 (请先选择)";
                            if (GUILayout.Button(uploadLabel, GUILayout.Height(30f)))
                                RunUploadSelected(selected);
                        }

                        Color prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
                        if (GUILayout.Button("一键打包并上传", GUILayout.Height(30f)))
                            RunBuildAndUpload();
                        GUI.backgroundColor = prevBg;
                    }

                    if (uploading)
                    {
                        Color prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f);
                        if (GUILayout.Button("终止任务", GUILayout.Width(90f), GUILayout.Height(30f)))
                            _service.CancelUpload(EnqueueLog);
                        GUI.backgroundColor = prevBg;
                    }
                }

                EditorGUILayout.Space(2f);
                Color prevColor = GUI.contentColor;
                GUI.contentColor = uploading
                    ? new Color(1f, 0.85f, 0.35f)
                    : (_statusIsError ? new Color(1f, 0.45f, 0.45f) : new Color(0.75f, 0.95f, 0.75f));
                EditorGUILayout.LabelField($"状态：{(uploading ? "SteamCMD 任务执行中..." : _statusMessage)}", EditorStyles.miniBoldLabel);
                GUI.contentColor = prevColor;
            }
        }

        private void DrawConfigSection()
        {
            _showConfigFoldout = EditorGUILayout.Foldout(_showConfigFoldout, "SteamCMD 与打包配置", true, EditorStyles.foldoutHeader);
            if (!_showConfigFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.DisabledScope(_service.IsUploading))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _config.steamCmdPath = EditorGUILayout.TextField("SteamCMD 路径", _config.steamCmdPath);
                    if (GUILayout.Button("浏览", GUILayout.Width(54f)))
                    {
                        string picked = EditorUtility.OpenFilePanel("选择 steamcmd.exe", "", "exe");
                        if (!string.IsNullOrEmpty(picked))
                        {
                            _config.steamCmdPath = picked;
                            _config.Save();
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    string prevAcc = _config.steamAccount;
                    _config.steamAccount = EditorGUILayout.TextField("Steam 账号", _config.steamAccount);
                    if (_config.steamAccount != prevAcc) _accountVerified = false;

                    string prevPwd = _password;
                    _password = EditorGUILayout.PasswordField("密码(可留空)", _password);
                    if (_password != prevPwd) _accountVerified = false;

                    bool nextRemember = EditorGUILayout.ToggleLeft("本机记住", _rememberPassword, GUILayout.Width(76f));
                    if (nextRemember != _rememberPassword)
                    {
                        _rememberPassword = nextRemember;
                        SteamDeployConfig.RememberPassword = _rememberPassword;
                        SteamDeployConfig.SavePassword(_password);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _guardCode = EditorGUILayout.TextField(
                        new GUIContent("Steam Guard 码", "首次登录或需要令牌时填写 5 位验证码；已缓存令牌可留空"),
                        _guardCode);

                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = _accountVerified ? new Color(0.55f, 0.9f, 0.55f) : prevBg;
                    if (GUILayout.Button(_accountVerified ? "已验证 ✓" : "验证账号", GUILayout.Width(86f)))
                        RunVerifyAccount();
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button(new GUIContent("弹窗登录", "弹出 SteamCMD 黑框进行首次密码/手机令牌授权并缓存登录态"), GUILayout.Width(76f)))
                        RunInteractiveLogin();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _config.appId = EditorGUILayout.TextField("AppID", _config.appId);
                    _config.depotId = EditorGUILayout.TextField("DepotID", _config.depotId);
                    _config.branchName = EditorGUILayout.TextField(new GUIContent("SetLive 分支", "留空则仅上传到 Steam 后台，不自动设为线上分支"), _config.branchName);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _config.buildsRootDir = EditorGUILayout.TextField("打包输出目录", _config.buildsRootDir);
                    if (GUILayout.Button("浏览", GUILayout.Width(54f)))
                    {
                        string picked = EditorUtility.OpenFolderPanel("选择打包输出根目录", _config.GetAbsoluteBuildsRoot(), "");
                        if (!string.IsNullOrEmpty(picked))
                        {
                            _config.buildsRootDir = picked;
                            _config.Save();
                            RefreshBuilds(selectLatestIfEmpty: true);
                        }
                    }
                    if (GUILayout.Button("打开", GUILayout.Width(54f)))
                    {
                        string root = _config.GetAbsoluteBuildsRoot();
                        Directory.CreateDirectory(root);
                        EditorUtility.RevealInFinder(root);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    int targetIdx = Array.IndexOf(SupportedTargets, _config.buildTarget);
                    if (targetIdx < 0) targetIdx = 0;
                    int nextIdx = EditorGUILayout.Popup("目标平台", targetIdx, SupportedTargetLabels);
                    _config.buildTarget = SupportedTargets[nextIdx];

                    string nextVersion = EditorGUILayout.TextField("游戏版本号", PlayerSettings.bundleVersion);
                    if (nextVersion != PlayerSettings.bundleVersion)
                        PlayerSettings.bundleVersion = nextVersion;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _config.executableName = EditorGUILayout.TextField("可执行文件名", _config.executableName);
                    _config.buildDescription = EditorGUILayout.TextField("本次上传备注", _config.buildDescription);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _config.developmentBuild = EditorGUILayout.ToggleLeft("Development Build", _config.developmentBuild, GUILayout.Width(145f));
                    _config.writeSteamAppIdInBuild = EditorGUILayout.ToggleLeft(
                        new GUIContent("包内写入 steam_appid.txt", "本地脱机测试可开启；正式上传 Steam 建议关闭"),
                        _config.writeSteamAppIdInBuild, GUILayout.Width(190f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("保存配置", GUILayout.Width(80f)))
                    {
                        SaveAllConfig();
                        SetStatus("配置已保存。", false);
                    }
                }
            }
        }

        private void DrawBuildsListSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"已打包列表 ({_builds.Count}) — 点击选择要上传的版本", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新列表", GUILayout.Width(76f)))
                    RefreshBuilds(selectLatestIfEmpty: true);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(150f), GUILayout.MaxHeight(240f)))
            {
                if (_builds.Count == 0)
                {
                    EditorGUILayout.HelpBox($"目录 {_config.GetAbsoluteBuildsRoot()} 下暂无打包记录。点击上方「仅打包」或「一键打包并上传」生成首个版本。", UnityEditor.MessageType.Info);
                    return;
                }

                _buildsScroll = EditorGUILayout.BeginScrollView(_buildsScroll);
                for (int i = 0; i < _builds.Count; i++)
                {
                    SteamBuildEntry entry = _builds[i];
                    bool isSelected = string.Equals(_selectedBuildPath, entry.FullPath, StringComparison.OrdinalIgnoreCase);

                    Color prevBg = GUI.backgroundColor;
                    if (isSelected)
                        GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        GUI.backgroundColor = prevBg;

                        bool toggled = EditorGUILayout.Toggle(isSelected, GUILayout.Width(18f));
                        if (toggled && !isSelected)
                            _selectedBuildPath = entry.FullPath;

                        using (new EditorGUILayout.VerticalScope())
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button($"{entry.FolderName}  [v{entry.DisplayVersion} | {entry.DisplayTarget}]",
                                        EditorStyles.label))
                                    _selectedBuildPath = entry.FullPath;

                                GUILayout.FlexibleSpace();
                                EditorGUILayout.LabelField(entry.FormatSize(), EditorStyles.miniLabel, GUILayout.Width(68f));
                                EditorGUILayout.LabelField(entry.CreatedTime.ToString("MM-dd HH:mm"), EditorStyles.miniLabel, GUILayout.Width(80f));
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                Color prevContent = GUI.contentColor;
                                if (entry.HasUploaded)
                                {
                                    GUI.contentColor = new Color(0.55f, 0.95f, 0.55f);
                                    EditorGUILayout.LabelField(
                                        $"已上传 [{entry.Metadata.lastUploadedBranch}] {entry.Metadata.lastUploadedLocalTime}",
                                        EditorStyles.miniLabel);
                                }
                                else
                                {
                                    GUI.contentColor = new Color(0.75f, 0.75f, 0.75f);
                                    EditorGUILayout.LabelField("未上传", EditorStyles.miniLabel, GUILayout.Width(50f));
                                }
                                GUI.contentColor = prevContent;

                                if (!string.IsNullOrWhiteSpace(entry.Metadata?.description))
                                    EditorGUILayout.LabelField($"备注: {entry.Metadata.description}", EditorStyles.miniLabel);
                            }
                        }

                        if (GUILayout.Button("打开", GUILayout.Width(44f), GUILayout.Height(28f)))
                            EditorUtility.RevealInFinder(entry.FullPath);

                        using (new EditorGUI.DisabledScope(_service.IsUploading))
                        {
                            if (GUILayout.Button("删除", GUILayout.Width(44f), GUILayout.Height(28f)))
                            {
                                if (EditorUtility.DisplayDialog("删除历史包",
                                        $"确认彻底删除打包目录？\n{entry.FullPath}", "删除", "取消"))
                                {
                                    if (_service.DeleteBuild(entry, out string delErr))
                                    {
                                        if (isSelected) _selectedBuildPath = null;
                                        RefreshBuilds(selectLatestIfEmpty: true);
                                        SetStatus($"已删除：{entry.FolderName}", false);
                                    }
                                    else
                                    {
                                        SetStatus(delErr, true);
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawLogSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("实时日志 (Build / SteamCMD)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                _autoScrollLog = EditorGUILayout.ToggleLeft("自动滚动", _autoScrollLog, GUILayout.Width(72f));
                if (GUILayout.Button("清空日志", GUILayout.Width(72f)))
                    _logBuilder.Clear();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
                EditorGUILayout.SelectableLabel(_logBuilder.ToString(), EditorStyles.wordWrappedMiniLabel,
                    GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void RunBuildOnly()
        {
            SaveAllConfig();
            if (_service.BuildPlayer(_config, EnqueueLog, out SteamBuildEntry created, out string err))
            {
                _selectedBuildPath = created.FullPath;
                RefreshBuilds(selectLatestIfEmpty: false);
                SetStatus($"打包成功：{created.FolderName}（已自动选中）", false);
            }
            else
            {
                SetStatus(err, true);
            }
        }

        private void RunUploadSelected(SteamBuildEntry target)
        {
            if (target == null)
            {
                SetStatus("请先在列表中选择一个要上传的打包版本。", true);
                return;
            }
            SaveAllConfig();
            StartUploadInternal(target);
        }

        private void RunBuildAndUpload()
        {
            SaveAllConfig();
            if (!_service.BuildPlayer(_config, EnqueueLog, out SteamBuildEntry created, out string err))
            {
                SetStatus(err, true);
                return;
            }
            _selectedBuildPath = created.FullPath;
            RefreshBuilds(selectLatestIfEmpty: false);
            StartUploadInternal(created);
        }

        private void RunVerifyAccount()
        {
            SaveAllConfig();
            _accountVerified = false;
            SetStatus($"正在验证 Steam 账号 ({_config.steamAccount})...", false);
            _service.StartVerifyAccount(_config, _password, _guardCode, EnqueueLog, (ok, msg) =>
            {
                EnqueueLog($"[Verify] {msg}");
                ScheduleMainThread(() =>
                {
                    _accountVerified = ok;
                    if (ok) _guardCode = string.Empty;
                    SetStatus(msg, !ok);
                });
            });
        }

        private void RunInteractiveLogin()
        {
            SaveAllConfig();
            if (_service.OpenInteractiveLogin(_config, EnqueueLog, out string err))
                SetStatus("已打开 SteamCMD 登录窗口，完成授权并输入 quit 后点击「验证账号」即可。", false);
            else
                SetStatus(err, true);
        }

        private void StartUploadInternal(SteamBuildEntry target)
        {
            SetStatus($"正在上传 {target.FolderName}...", false);
            _service.StartUpload(_config, _password, target, _config.buildDescription, EnqueueLog, (ok, msg) =>
            {
                EnqueueLog($"[Result] {msg}");
                ScheduleMainThread(() =>
                {
                    RefreshBuilds(selectLatestIfEmpty: false);
                    SetStatus(msg, !ok);
                });
            }, _guardCode);
        }

        private void RefreshBuilds(bool selectLatestIfEmpty)
        {
            _builds = _service.ScanBuilds(_config);
            if (!string.IsNullOrEmpty(_selectedBuildPath))
            {
                bool stillExists = _builds.Exists(b =>
                    string.Equals(b.FullPath, _selectedBuildPath, StringComparison.OrdinalIgnoreCase));
                if (!stillExists)
                    _selectedBuildPath = null;
            }
            if (selectLatestIfEmpty && string.IsNullOrEmpty(_selectedBuildPath) && _builds.Count > 0)
                _selectedBuildPath = _builds[0].FullPath;
        }

        private SteamBuildEntry GetSelectedBuild()
        {
            if (string.IsNullOrEmpty(_selectedBuildPath)) return null;
            for (int i = 0; i < _builds.Count; i++)
            {
                if (string.Equals(_builds[i].FullPath, _selectedBuildPath, StringComparison.OrdinalIgnoreCase))
                    return _builds[i];
            }
            return null;
        }

        private void SaveAllConfig()
        {
            _config.Save();
            SteamDeployConfig.RememberPassword = _rememberPassword;
            SteamDeployConfig.SavePassword(_password);
        }

        private void EnqueueLog(string line)
        {
            _pendingLogs.Enqueue($"[{DateTime.Now:HH:mm:ss}] {line}");
        }

        private void ScheduleMainThread(Action action)
        {
            lock (_actionGate)
                _pendingMainThreadAction += action;
        }

        private void SetStatus(string message, bool isError)
        {
            _statusMessage = message;
            _statusIsError = isError;
            Repaint();
        }
    }
}
