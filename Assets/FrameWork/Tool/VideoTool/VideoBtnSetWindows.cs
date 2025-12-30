using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace FrameWork
{
    public class VideoBtnSetWindows : OdinEditorWindow
    {
        private VideoClip videoClip;
        
        [LabelText("时间定格")]
        [OnValueChanged("OnSliderDragging")] // 改名为 OnSliderDragging 更明确
        [ShowIf("videoClip")]
        public float videoTime;

        
        
        private List<ButtonNode> _buttonNode = new List<ButtonNode>();
        
        
        private readonly Vector2 REFERENCE_RES = new Vector2(1920, 1080);
        private readonly Vector2 NODE_SIZE = new Vector2(200, 60);

        private GameObject _videoPlayerGO;
        private VideoPlayer _videoPlayer;
        private RenderTexture _renderTexture;

        private int _draggingIndex = -1;
        private Vector2 _dragOffset;
        
        private Rect _videoDrawRect; 
        private float _currentScale;
        
        // 关键修复：添加一个显式的播放状态标记
        private bool _isPlaying = false; 
        
        public void Init(List<ButtonNode> buttonNode, VideoClip videoClip,float videoTime)
        {
            var window = GetWindow<VideoBtnSetWindows>();
            window.videoClip = videoClip;
            window._buttonNode = buttonNode;
            window.videoTime = videoTime;
            window.Show();
            window.OnVideoClipChanged();

            EditorApplication.delayCall += OnSliderDragging;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            InitializeVideoPlayer();
            EditorApplication.update += OnEditorUpdate;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CleanupVideoPlayer();
            EditorApplication.update -= OnEditorUpdate;
        }

        // ================= 核心修复逻辑 =================

        private void OnEditorUpdate()
        {
            // 只有在"点击播放按钮"的模式下，才让视频驱动滑块
            if (_isPlaying && _videoPlayer != null)
            {
                if (!_videoPlayer.isPlaying) 
                {
                    // 视频自己播完了（非循环）或者被意外停止
                    _videoPlayer.Play(); 
                }
                // 同步时间到滑块
                videoTime = (float)_videoPlayer.time;
                Repaint();
            }
        }

        // 当用户拖动滑块时触发
        private void OnSliderDragging()
        {
            if (_videoPlayer == null || !_videoPlayer.isPrepared) return;

            // 1. 如果正在自动播放，立刻停止，转为手动预览模式
            if (_isPlaying)
            {
                StopPlayback();
            }

            // 2. 核心：Seek & Freeze
            // 先暂停，确保不会自动跑
            _videoPlayer.Pause(); 
            _videoPlayer.time = videoTime;
            
            // 3. 必须 Play() 一下才能让 VideoPlayer 渲染新的一帧到 RenderTexture
            _videoPlayer.Play();
            
            // 4. 利用 delayCall 在当前帧结束/下一帧开始时立即暂停
            // 这样人眼看到的就是"定格"在了那一帧
            EditorApplication.delayCall += () => 
            {
                if (_videoPlayer != null)
                {
                    _videoPlayer.Pause();
                    Repaint(); // 强制重绘窗口显示新画面
                }
            };
        }

        private void StartPlayback()
        {
            _isPlaying = true;
            _videoPlayer.Play();
        }

        private void StopPlayback()
        {
            _isPlaying = false;
            _videoPlayer.Pause();
        }

        // ================= 渲染逻辑 (保持之前的需求：铺满+分层) =================
        
        protected override void OnImGUI()
        {
            Rect fullScreenRect = new Rect(0, 0, position.width, position.height);

            // 1. 底层：视频铺满
            CalculateScaleToFill(fullScreenRect);
            DrawVideoBackground(fullScreenRect);

            // 2. 中层：Odin 属性面板 (带半透明背景)
            GUILayout.BeginArea(fullScreenRect); 
            {
                GUILayout.BeginVertical();
                {
                    GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
                    bgStyle.normal.background = Texture2D.whiteTexture; 
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0, 0, 0, 0); // 半透明黑底
                    
                    GUILayout.BeginVertical(bgStyle, GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = oldColor;
                    
                    base.OnImGUI(); // Odin 绘制
                    
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();

            // 3. 顶层：按钮
            DrawDraggableNodesTopLayer();

            if (Event.current.type == EventType.MouseUp)
            {
                _draggingIndex = -1;
            }
        }

        private void CalculateScaleToFill(Rect windowRect)
        {
            float refWidth = videoClip ? videoClip.width : REFERENCE_RES.x;
            float refHeight = videoClip ? videoClip.height : REFERENCE_RES.y;

            float widthRatio = windowRect.width / refWidth;
            float heightRatio = windowRect.height / refHeight;

            // Scale to Fill 使用 Max
            _currentScale = Mathf.Max(widthRatio, heightRatio);

            float drawWidth = refWidth * _currentScale;
            float drawHeight = refHeight * _currentScale;

            float x = windowRect.x + (windowRect.width - drawWidth) * 0.5f;
            float y = windowRect.y + (windowRect.height - drawHeight) * 0.5f;

            _videoDrawRect = new Rect(x, y, drawWidth, drawHeight);
        }

        private void DrawVideoBackground(Rect windowRect)
        {
            if (_renderTexture != null && videoClip != null)
            {
                // 绘制纯黑底色防止边缘穿帮
                EditorGUI.DrawRect(windowRect, Color.black);
                GUI.DrawTexture(_videoDrawRect, _renderTexture, ScaleMode.StretchToFill);
            }
            else
            {
                EditorGUI.DrawRect(windowRect, new Color(0.1f, 0.1f, 0.1f));
                GUI.Label(windowRect, "无视频信号", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
            }
        }

        private void DrawDraggableNodesTopLayer()
        {
            if (_buttonNode == null) return;
            Event e = Event.current;
            
            // 1. 获取相对于视频左上角的鼠标坐标 (范围: 0~1920, 0~1080)
            Vector2 mousePosInVideoTopLeft = (e.mousePosition - _videoDrawRect.position) / _currentScale;
            
            // 2. 定义中心点偏移量 (例如: 960, 540)
            Vector2 centerOffset = REFERENCE_RES * 0.5f;

            // 设置 GUI 矩阵 (保持不变，用于缩放适配)
            Matrix4x4 originalMatrix = GUI.matrix;
            Vector3 scaleVec = new Vector3(_currentScale, _currentScale, 1f);
            Vector3 posVec = new Vector3(_videoDrawRect.x, _videoDrawRect.y, 0f);
            GUI.matrix = Matrix4x4.TRS(posVec, Quaternion.identity, scaleVec);

            GUIStyle nodeStyle = new GUIStyle("flow node 0") { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            GUIStyle selectedStyle = new GUIStyle("flow node 1") { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } };
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };

            for (int i = 0; i < _buttonNode.Count; i++)
            {
                var node = _buttonNode[i];
                if (node == null) continue;

                // 【核心修改 A】：将存储的中心坐标 -> 转换为 GUI 绘制用的左上角坐标
                // 假设 node.buttonPosition 是以中心为 (0,0)
                Vector2 drawPos = node.buttonPosition + centerOffset;

                Rect nodeRectRef = new Rect(drawPos.x - NODE_SIZE.x / 2f, drawPos.y - NODE_SIZE.y / 2f, NODE_SIZE.x, NODE_SIZE.y);
                bool isSelected = (i == _draggingIndex);
                
                GUI.Box(nodeRectRef, node.buttonName, isSelected ? selectedStyle : nodeStyle);
                
                // 显示坐标 (此时显示的即为中心坐标)
                if (isSelected) 
                    GUI.Label(new Rect(nodeRectRef.x, nodeRectRef.y - 40, 300, 40), 
                        $"({(int)node.buttonPosition.x}, {(int)node.buttonPosition.y})", labelStyle);
            }

            GUI.matrix = originalMatrix;

            // --- 事件处理 ---

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (int i = _buttonNode.Count - 1; i >= 0; i--)
                {
                    var node = _buttonNode[i];
                    // 【核心修改 B】：碰撞检测时，同样要先把中心坐标转为 GUI 坐标
                    Vector2 drawPos = node.buttonPosition + centerOffset;
                    
                    Rect nodeRectRef = new Rect(drawPos.x - NODE_SIZE.x / 2f, drawPos.y - NODE_SIZE.y / 2f, NODE_SIZE.x, NODE_SIZE.y);
                    
                    if (nodeRectRef.Contains(mousePosInVideoTopLeft))
                    {
                        _draggingIndex = i;
                        // 计算鼠标相对于按钮中心的偏移
                        // 注意：这里 mousePosInVideoTopLeft 和 drawPos 都是“左上角坐标系”，相减没问题
                        _dragOffset = mousePosInVideoTopLeft - drawPos; 
                        GUI.changed = true;
                        e.Use();
                        break;
                    }
                }
            }

            if (e.type == EventType.MouseDrag && _draggingIndex != -1)
            {
                if (_draggingIndex < _buttonNode.Count)
                {
                    // 1. 计算新的 GUI 坐标 (左上角基准)
                    Vector2 newDrawPos = mousePosInVideoTopLeft - _dragOffset;

                    // 2. 【核心修改 C】：将 GUI 坐标 -> 转换为中心坐标进行存储
                    Vector2 newCenterPos = newDrawPos - centerOffset;

                    // 3. 限制范围 (现在范围变成了 -960 到 960)
                    newCenterPos.x = Mathf.Clamp(newCenterPos.x, -centerOffset.x, centerOffset.x);
                    newCenterPos.y = Mathf.Clamp(newCenterPos.y, -centerOffset.y, centerOffset.y);

                    _buttonNode[_draggingIndex].buttonPosition = newCenterPos;
                    
                    GUI.changed = true;
                    e.Use();
                }
            }
        }

        // ================= 视频底层管理 =================

        private void InitializeVideoPlayer()
        {
            if (videoClip == null) return;
    
            if (_videoPlayerGO == null)
            {
                _videoPlayerGO = new GameObject("Editor_Video_Preview") { hideFlags = HideFlags.HideAndDontSave };
                _videoPlayer = _videoPlayerGO.AddComponent<VideoPlayer>();
                _videoPlayer.playOnAwake = false;
                _videoPlayer.isLooping = true;
                _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            }

            if (_renderTexture == null || _renderTexture.width != (int)videoClip.width || _renderTexture.height != (int)videoClip.height)
            {
                if (_renderTexture != null) _renderTexture.Release();
                _renderTexture = new RenderTexture((int)videoClip.width, (int)videoClip.height, 0, RenderTextureFormat.ARGB32);
            }

            _videoPlayer.clip = videoClip;
            _videoPlayer.targetTexture = _renderTexture;
    
            // ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼ 修改重点开始 ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
    
            // 监听准备完成事件
            _videoPlayer.prepareCompleted += (source) => 
            {
                // 1. 强制设定初始时间 (确保 Init 传进来的 videoTime 生效)
                source.time = videoTime;
        
                // 2. 关键：必须 Play() 触发渲染管线，否则 RenderTexture 依然是黑的
                source.Play();
        
                // 3. 利用 delayCall 在当前帧逻辑结束后立即暂停
                // 这样既能刷出画面，又不会让视频自动播放下去
                EditorApplication.delayCall += () => 
                {
                    if (source != null)
                    {
                        source.Pause();
                        // 强制重绘窗口，否则用户可能要点一下鼠标界面才会亮
                        Repaint(); 
                    }
                };
            };
    
            // 开始准备
            _videoPlayer.Prepare();
    
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲ 修改重点结束 ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        }

        private void CleanupVideoPlayer()
        {
            if (_videoPlayerGO != null) DestroyImmediate(_videoPlayerGO);
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                DestroyImmediate(_renderTexture);
            }
            _isPlaying = false;
        }

        private void OnVideoClipChanged()
        {
            CleanupVideoPlayer();
            InitializeVideoPlayer();
            _isPlaying = false;
        }
    }

}