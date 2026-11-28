using System;
using System.IO;
using FrameWork.Video;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor.Video
{
    /// <summary>
    /// FrameworkVideoPlayer 自定义 Inspector：
    /// 支持在脚本面板直接浏览选择视频、实时显示画面预览、播放/暂停/停止以及拖拽进度条 Seek（编辑模式与运行模式均可用）。
    /// </summary>
    [CustomEditor(typeof(FrameworkVideoPlayer))]
    public sealed class FrameworkVideoPlayerEditor : UnityEditor.Editor
    {
        private SerializedProperty _initialSourceProp;
        private SerializedProperty _loopProp;
        private SerializedProperty _volumeProp;
        private SerializedProperty _muteProp;
        private SerializedProperty _playbackRateProp;

        private bool _editPreviewActive;
        private bool _isPaused;
        private bool _draggingSlider;
        private float _dragProgress;

        private void OnEnable()
        {
            _initialSourceProp = serializedObject.FindProperty("initialSource");
            _loopProp = serializedObject.FindProperty("loop");
            _volumeProp = serializedObject.FindProperty("volume");
            _muteProp = serializedObject.FindProperty("mute");
            _playbackRateProp = serializedObject.FindProperty("playbackRate");
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (_editPreviewActive && !Application.isPlaying && target is FrameworkVideoPlayer player && player != null)
            {
                player.EditorReleasePreview();
                _editPreviewActive = false;
            }
        }

        private void OnEditorUpdate()
        {
            if (!(target is FrameworkVideoPlayer player) || player == null)
                return;

            if (!Application.isPlaying && _editPreviewActive)
            {
                if (player.EditorTick())
                    Repaint();
            }
            else if (Application.isPlaying && player.IsPlaying)
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            var player = (FrameworkVideoPlayer)target;

            // 快捷选择本地视频文件
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("浏览本地视频...", GUILayout.Width(120f)))
                {
                    string picked = EditorUtility.OpenFilePanel("选择视频文件", Application.streamingAssetsPath, "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string sa = Path.GetFullPath(Application.streamingAssetsPath).Replace('\\', '/');
                        string norm = Path.GetFullPath(picked).Replace('\\', '/');
                        if (norm.StartsWith(sa + "/", StringComparison.OrdinalIgnoreCase))
                            norm = norm.Substring(sa.Length + 1);
                        _initialSourceProp.stringValue = norm;
                    }
                }
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                // Inspector 参数改动时实时同步到正在播放的实例
                if (Application.isPlaying || _editPreviewActive)
                {
                    player.SetLoop(_loopProp.boolValue);
                    player.SetVolume(_volumeProp.floatValue);
                    player.SetMute(_muteProp.boolValue);
                    player.SetPlaybackRate(_playbackRateProp.floatValue);
                }
            }

            EditorGUILayout.Space(8f);
            DrawPreviewPanel(player);
        }

        private void DrawPreviewPanel(FrameworkVideoPlayer player)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string sourceToPlay = !string.IsNullOrWhiteSpace(player.CurrentSource)
                    ? player.CurrentSource
                    : _initialSourceProp.stringValue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("视频预览与控制", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    string res = player.VideoWidth > 0 ? $"{player.VideoWidth}x{player.VideoHeight}" : "--";
                    EditorGUILayout.LabelField($"[{player.BackendName}] {res}", EditorStyles.miniLabel, GUILayout.Width(145f));
                }

                // 视频画面预览区域
                Texture tex = player.CurrentTexture;
                float aspect = (player.VideoWidth > 0 && player.VideoHeight > 0)
                    ? (float)player.VideoWidth / player.VideoHeight
                    : 16f / 9f;
                float maxPreviewWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 44f);
                float previewHeight = Mathf.Clamp(maxPreviewWidth / aspect, 120f, 260f);
                Rect previewRect = GUILayoutUtility.GetRect(maxPreviewWidth, previewHeight, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(previewRect, new Color(0.08f, 0.08f, 0.08f, 1f));

                if (tex != null)
                {
                    // 按宽高比居中绘制，避免预览画面拉伸
                    float drawW = previewRect.width;
                    float drawH = drawW / aspect;
                    if (drawH > previewRect.height)
                    {
                        drawH = previewRect.height;
                        drawW = drawH * aspect;
                    }
                    var drawRect = new Rect(
                        previewRect.x + (previewRect.width - drawW) * 0.5f,
                        previewRect.y + (previewRect.height - drawH) * 0.5f,
                        drawW,
                        drawH);
                    Rect uv = player.IsTextureFlippedY ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
                    GUI.DrawTextureWithTexCoords(drawRect, tex, uv, false);
                }
                else
                {
                    var centered = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11 };
                    string hint = string.IsNullOrWhiteSpace(sourceToPlay)
                        ? "请先在 Initial Source 中填写视频路径或点击「浏览本地视频」"
                        : "点击下方「播放」即可在此预览视频";
                    GUI.Label(previewRect, hint, centered);
                }

                EditorGUILayout.Space(4f);

                // 进度条拖拽区
                double duration = player.DurationSeconds;
                double current = player.CurrentTimeSeconds;
                float liveProgress = duration > 0.001 ? Mathf.Clamp01((float)(current / duration)) : 0f;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(FormatTime(_draggingSlider ? _dragProgress * duration : current),
                        EditorStyles.miniLabel, GUILayout.Width(42f));

                    using (new EditorGUI.DisabledScope(duration <= 0.001))
                    {
                        EditorGUI.BeginChangeCheck();
                        float sliderVal = GUILayout.HorizontalSlider(
                            _draggingSlider ? _dragProgress : liveProgress, 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _draggingSlider = true;
                            _dragProgress = sliderVal;
                            player.Seek(sliderVal);
                        }
                        if (_draggingSlider && Event.current.type == EventType.MouseUp)
                            _draggingSlider = false;
                    }

                    EditorGUILayout.LabelField(FormatTime(duration), EditorStyles.miniLabel, GUILayout.Width(42f));
                }

                EditorGUILayout.Space(2f);

                // 播放控制按钮栏
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(sourceToPlay)))
                    {
                        if (GUILayout.Button("播放", GUILayout.Height(24f)))
                        {
                            _isPaused = false;
                            if (!Application.isPlaying)
                            {
                                _editPreviewActive = true;
                                player.EditorPlayPreview(_initialSourceProp.stringValue, _loopProp.boolValue);
                            }
                            else
                            {
                                player.Play(_initialSourceProp.stringValue, _loopProp.boolValue);
                            }
                        }
                    }

                    using (new EditorGUI.DisabledScope(!player.IsPlaying && !_isPaused))
                    {
                        string pauseLabel = _isPaused ? "继续" : "暂停";
                        if (GUILayout.Button(pauseLabel, GUILayout.Height(24f)))
                        {
                            if (_isPaused)
                            {
                                player.Resume();
                                _isPaused = false;
                            }
                            else
                            {
                                player.Pause();
                                _isPaused = true;
                            }
                        }
                    }

                    using (new EditorGUI.DisabledScope(!player.IsPlaying && !_isPaused && !_editPreviewActive))
                    {
                        if (GUILayout.Button("停止", GUILayout.Height(24f)))
                        {
                            _isPaused = false;
                            if (!Application.isPlaying)
                            {
                                player.EditorReleasePreview();
                                _editPreviewActive = false;
                            }
                            else
                            {
                                player.Stop();
                            }
                        }
                    }
                }
            }
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
                return "00:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1.0
                ? $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"
                : $"{ts.Minutes:00}:{ts.Seconds:00}";
        }
    }
}
