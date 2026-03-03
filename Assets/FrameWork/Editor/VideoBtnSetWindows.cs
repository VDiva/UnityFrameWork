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
        [OnValueChanged("OnSliderDragging")]
        [ShowIf("videoClip")]
        public float videoTime;
        
        private List<ButtonNode> _buttonNode = new List<ButtonNode>();
        
        private List<QteData> _qteDatas = new List<QteData>();
        
        private readonly Vector2 REFERENCE_RES = new Vector2(1920, 1080);
        private readonly Vector2 NODE_SIZE = new Vector2(200, 60);

        private GameObject _videoPlayerGO;
        private VideoPlayer _videoPlayer;
        private RenderTexture _renderTexture;

        private int _draggingIndex = -1;
        private Vector2 _dragOffset;
        
        private Rect _videoDrawRect; 
        private float _currentScale;
        
        private bool _isPlaying = false; 
        
        public static void Init(List<ButtonNode> buttonNode, VideoClip videoClip, float videoTime)
        {
            var window = GetWindow<VideoBtnSetWindows>();
            window.videoClip = videoClip;
            window._buttonNode = buttonNode;
            window.videoTime = videoTime;
            window.titleContent = new GUIContent("视频按钮位置编辑");
            window.Show();
            window.OnVideoClipChanged();
        }
        
        
        public static void InitAsQte(List<QteData> qteDatas, VideoClip videoClip, float videoTime)
        {
            var window = GetWindow<VideoBtnSetWindows>();
            window.videoClip = videoClip;
            window._qteDatas = qteDatas;
            window.videoTime = videoTime;
            window.titleContent = new GUIContent("视频按钮位置编辑");
            window.Show();
            window.OnVideoClipChanged();
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

        private void OnEditorUpdate()
        {
            if (_isPlaying && _videoPlayer != null)
            {
                if (!_videoPlayer.isPlaying) _videoPlayer.Play(); 
                videoTime = (float)_videoPlayer.time;
                Repaint();
            }
        }

        private void OnSliderDragging()
        {
            if (_videoPlayer == null || !_videoPlayer.isPrepared) return;

            if (_isPlaying) StopPlayback();
            
            _videoPlayer.Pause(); 
            _videoPlayer.time = videoTime;
            _videoPlayer.Play();
            
            EditorApplication.delayCall += () => 
            {
                if (_videoPlayer != null)
                {
                    _videoPlayer.Pause();
                    Repaint(); 
                }
            };
        }

        private void StartPlayback() => _isPlaying = true;
        private void StopPlayback() => _isPlaying = false;

        protected override void OnImGUI()
        {
            Rect fullScreenRect = new Rect(0, 0, position.width, position.height);

            // 1. 底层：视频铺满
            CalculateScaleToFill(fullScreenRect);
            DrawVideoBackground(fullScreenRect);

            // 2. 中层：Odin 属性面板
            GUILayout.BeginArea(fullScreenRect); 
            {
                GUILayout.BeginVertical();
                {
                    GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
                    bgStyle.normal.background = Texture2D.whiteTexture; 
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0, 0, 0, 0.3f); 
                    
                    GUILayout.BeginVertical(bgStyle, GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = oldColor;
                    base.OnImGUI(); 
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();

            // 3. 顶层：按钮 (处理 Y 轴反转)
            DrawDraggableNodesTopLayer();
            DrawDraggableNodesTopLayerAsQte();
            if (Event.current.type == EventType.MouseUp)
            {
                _draggingIndex = -1;
            }
        }

        private void CalculateScaleToFill(Rect windowRect)
        {
            // 关键修复：统一使用 REFERENCE_RES 进行比例计算
            // 这样按钮逻辑空间（1920x1080）就能完美匹配视频绘制区域
            float refWidth = REFERENCE_RES.x;
            float refHeight = REFERENCE_RES.y;

            float widthRatio = windowRect.width / refWidth;
            float heightRatio = windowRect.height / refHeight;

            // 计算缩放：使用 Max 确保视频填满窗口
            _currentScale = Mathf.Max(widthRatio, heightRatio);

            float drawWidth = refWidth * _currentScale;
            float drawHeight = refHeight * _currentScale;

            // 居中偏移
            float x = windowRect.x + (windowRect.width - drawWidth) * 0.5f;
            float y = windowRect.y + (windowRect.height - drawHeight) * 0.5f;

            _videoDrawRect = new Rect(x, y, drawWidth, drawHeight);
        }

        private void DrawVideoBackground(Rect windowRect)
        {
            if (_renderTexture != null && videoClip != null)
            {
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
            
            // 鼠标在 1920x1080 空间下的 GUI 坐标 (左上角原点)
            Vector2 mousePosInVideoTopLeft = (e.mousePosition - _videoDrawRect.position) / _currentScale;
            Vector2 centerOffset = REFERENCE_RES * 0.5f;

            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(_videoDrawRect.x, _videoDrawRect.y, 0f), Quaternion.identity, new Vector3(_currentScale, _currentScale, 1f));

            

            for (int i = 0; i < _buttonNode.Count; i++)
            {
                var node = _buttonNode[i];
                if (node == null) continue;

                float size = _buttonNode[i].buttonSize;
                GUIStyle nodeStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = (int)size, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                GUIStyle selectedStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = (int)size, fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } };
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };
                
                // 【核心修复：Y轴反转转换】 
                // 数据坐标 -> GUI坐标：Y 坐标需要用 中心点 减去 偏移量
                Vector2 drawPos = new Vector2(
                    centerOffset.x + node.buttonPosition.x, 
                    centerOffset.y - node.buttonPosition.y 
                );

                Rect nodeRectRef = new Rect(drawPos.x - NODE_SIZE.x / 2f, drawPos.y - NODE_SIZE.y / 2f, NODE_SIZE.x, NODE_SIZE.y);
                bool isSelected = (i == _draggingIndex);
                
                GUI.Label(nodeRectRef, node.buttonName, isSelected ? selectedStyle : nodeStyle);

                if (isSelected) 
                {
                    // 显示逻辑坐标 (中心原点坐标)
                    GUI.Label(new Rect(nodeRectRef.x, nodeRectRef.y - 30, 300, 30), 
                        $"X:{(int)node.buttonPosition.x} Y:{(int)node.buttonPosition.y}", labelStyle);
                }
            }

            GUI.matrix = originalMatrix;

            // --- 事件处理 ---

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (int i = _buttonNode.Count - 1; i >= 0; i--)
                {
                    var node = _buttonNode[i];
                    if (node == null) continue;

                    // 点击判定也使用相同的反转逻辑计算位置
                    Vector2 drawPos = new Vector2(centerOffset.x + node.buttonPosition.x, centerOffset.y - node.buttonPosition.y);
                    Rect nodeRectRef = new Rect(drawPos.x - NODE_SIZE.x / 2f, drawPos.y - NODE_SIZE.y / 2f, NODE_SIZE.x, NODE_SIZE.y);
                    
                    if (nodeRectRef.Contains(mousePosInVideoTopLeft))
                    {
                        _draggingIndex = i;
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
                    Vector2 newDrawPos = mousePosInVideoTopLeft - _dragOffset;

                    // 【核心修复：Y轴反转存回】
                    // GUI坐标 -> 数据坐标：Y 坐标用 中心点 减去 GUI位置
                    Vector2 newCenterPos = new Vector2(
                        newDrawPos.x - centerOffset.x,
                        centerOffset.y - newDrawPos.y
                    );

                    newCenterPos.x = Mathf.Clamp(newCenterPos.x, -centerOffset.x, centerOffset.x);
                    newCenterPos.y = Mathf.Clamp(newCenterPos.y, -centerOffset.y, centerOffset.y);

                    _buttonNode[_draggingIndex].buttonPosition = newCenterPos;
                    
                    GUI.changed = true;
                    e.Use();
                }
            }
        }
        
        
        
        private void DrawDraggableNodesTopLayerAsQte()
        {
            if (_qteDatas == null) return;
            Event e = Event.current;
            
            // 鼠标在 1920x1080 空间下的 GUI 坐标 (左上角原点)
            Vector2 mousePosInVideoTopLeft = (e.mousePosition - _videoDrawRect.position) / _currentScale;
            Vector2 centerOffset = REFERENCE_RES * 0.5f;

            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(_videoDrawRect.x, _videoDrawRect.y, 0f), Quaternion.identity, new Vector3(_currentScale, _currentScale, 1f));

            float size = 24;
            GUIStyle nodeStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = (int)size, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            GUIStyle selectedStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = (int)size, fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } };
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };

            for (int i = 0; i < _qteDatas.Count; i++)
            {
                var node = _qteDatas[i];
                if (node == null) continue;

                // 【核心修复：Y轴反转转换】 
                // 数据坐标 -> GUI坐标：Y 坐标需要用 中心点 减去 偏移量
                Vector2 drawPos = new Vector2(
                    centerOffset.x + node.qteLoc.x, 
                    centerOffset.y - node.qteLoc.y 
                );

                Rect nodeRectRef = new Rect(drawPos.x - NODE_SIZE.x / 2f, drawPos.y - NODE_SIZE.y / 2f, NODE_SIZE.x, NODE_SIZE.y);
                bool isSelected = (i == _draggingIndex);
                
                GUI.Label(nodeRectRef, node.qteName, isSelected ? selectedStyle : nodeStyle);

                if (isSelected) 
                {
                    // 显示逻辑坐标 (中心原点坐标)
                    GUI.Label(new Rect(nodeRectRef.x, nodeRectRef.y - 30, 300, 30), 
                        $"X:{(int)node.qteLoc.x} Y:{(int)node.qteLoc.y}", labelStyle);
                }
            }

            GUI.matrix = originalMatrix;

            // --- 事件处理 ---

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (int i = _qteDatas.Count - 1; i >= 0; i--)
                {
                    var node = _qteDatas[i];
                    if (node == null) continue;

                    // 点击判定也使用相同的反转逻辑计算位置
                    Vector2 drawPos = new Vector2(centerOffset.x + node.qteLoc.x, centerOffset.y - node.qteLoc.y);
                    Rect nodeRectRef = new Rect(drawPos.x - NODE_SIZE.x / 2f, drawPos.y - NODE_SIZE.y / 2f, NODE_SIZE.x, NODE_SIZE.y);
                    
                    if (nodeRectRef.Contains(mousePosInVideoTopLeft))
                    {
                        _draggingIndex = i;
                        _dragOffset = mousePosInVideoTopLeft - drawPos; 
                        GUI.changed = true;
                        e.Use();
                        break;
                    }
                }
            }

            if (e.type == EventType.MouseDrag && _draggingIndex != -1)
            {
                if (_draggingIndex < _qteDatas.Count)
                {
                    Vector2 newDrawPos = mousePosInVideoTopLeft - _dragOffset;

                    // 【核心修复：Y轴反转存回】
                    // GUI坐标 -> 数据坐标：Y 坐标用 中心点 减去 GUI位置
                    Vector2 newCenterPos = new Vector2(
                        newDrawPos.x - centerOffset.x,
                        centerOffset.y - newDrawPos.y
                    );

                    newCenterPos.x = Mathf.Clamp(newCenterPos.x, -centerOffset.x, centerOffset.x);
                    newCenterPos.y = Mathf.Clamp(newCenterPos.y, -centerOffset.y, centerOffset.y);

                    _qteDatas[_draggingIndex].qteLoc = newCenterPos;
                    
                    GUI.changed = true;
                    e.Use();
                }
            }
        }
        

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
    
            _videoPlayer.prepareCompleted += (source) => 
            {
                source.time = videoTime;
                source.Play();
                EditorApplication.delayCall += () => 
                {
                    if (source != null)
                    {
                        source.Pause();
                        Repaint(); 
                    }
                };
            };
            _videoPlayer.Prepare();
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