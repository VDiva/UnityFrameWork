using System;
using UnityEngine;
using UnityEngine.Video;

namespace FrameWork.InteractiveFilm
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class FilmPlayer : MonoBehaviour
    {
        [SerializeField] private FilmGraph graph;
        [SerializeField] private bool showBuiltInUi = true;
        [SerializeField] private Font uiFont;

        private VideoPlayer video;
        private FilmRoute route;
        private bool preparing;
        private bool advanceQueued;
        private Vector2 choiceScroll;
        private GUIStyle titleStyle;
        private GUIStyle captionStyle;
        private GUIStyle buttonStyle;
        private Font generatedFont;

        public FilmContentNode CurrentNode { get; private set; }
        public string PlaybackError { get; private set; }
        public bool IsPreparing => preparing;
        public Texture VideoTexture => video != null && video.isPrepared ? video.texture : null;

        public event Action<FilmContentNode> NodeChanged;

        private void Awake()
        {
            ConfigureVideo();
        }

        private void ConfigureVideo()
        {
            if (video != null) return;
            video = GetComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.source = VideoSource.VideoClip;
            video.renderMode = VideoRenderMode.APIOnly;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.isLooping = false;
        }

        private void OnEnable()
        {
            video.prepareCompleted += OnPrepared;
            video.loopPointReached += OnFinished;
            video.errorReceived += OnVideoError;
            if (CurrentNode != null) Enter(CurrentNode);
        }

        private void Start()
        {
            if (route == null && graph != null) Play(graph);
        }

        private void Update()
        {
            if (!advanceQueued) return;
            advanceQueued = false;
            Advance();
        }

        private void OnDisable()
        {
            video.prepareCompleted -= OnPrepared;
            video.loopPointReached -= OnFinished;
            video.errorReceived -= OnVideoError;
            advanceQueued = false;
            preparing = false;
            video.Stop();
        }

        private void OnDestroy()
        {
            if (generatedFont != null) Destroy(generatedFont);
        }

        public bool Play(FilmGraph story)
        {
            ConfigureVideo();
            graph = story;
            string error = null;
            if (story == null || !story.TryBuild(out route, out error))
            {
                if (video != null) video.Stop();
                route = null;
                CurrentNode = null;
                advanceQueued = false;
                preparing = false;
                PlaybackError = error ?? "请绑定影游资源。";
                NodeChanged?.Invoke(null);
                return false;
            }

            Enter(route.Start);
            return true;
        }

        public bool Restart() => Play(graph);

        public bool Advance() => CurrentNode is FilmSegmentNode && Follow(0);

        public bool Choose(int option) => CurrentNode is FilmChoiceNode && Follow(option);

        private bool Follow(int option)
        {
            var next = route?.GetNext(CurrentNode, option);
            if (next == null) return false;
            Enter(next);
            return true;
        }

        private void Enter(FilmContentNode node)
        {
            video.Stop();
            video.clip = null;
            advanceQueued = false;
            preparing = false;
            CurrentNode = node;
            choiceScroll = Vector2.zero;
            PlaybackError = null;
            if (node is FilmSegmentNode segment && segment.video != null && isActiveAndEnabled)
            {
                video.clip = segment.video;
                preparing = true;
                video.Prepare();
            }
            NodeChanged?.Invoke(node);
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!(CurrentNode is FilmSegmentNode segment) || source.clip != segment.video) return;
            preparing = false;
            source.Play();
        }

        private void OnFinished(VideoPlayer source)
        {
            if (CurrentNode is FilmSegmentNode segment && segment.autoAdvance && source.clip == segment.video)
                advanceQueued = true;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            if (!(CurrentNode is FilmSegmentNode segment) || segment.video == null || source.clip != segment.video)
                return;
            preparing = false;
            PlaybackError = message;
        }

        private void OnGUI()
        {
            if (!showBuiltInUi) return;
            var viewport = new Rect(0f, 0f, Screen.width, Screen.height);
            var previous = GUI.color;
            GUI.color = new Color(0.055f, 0.075f, 0.11f);
            GUI.DrawTexture(viewport, Texture2D.whiteTexture);
            GUI.color = previous;
            if (CurrentNode is FilmSegmentNode clip)
            {
                Texture image = VideoTexture != null ? VideoTexture : clip.poster;
                if (image != null) GUI.DrawTexture(viewport, image, ScaleMode.ScaleToFit, true);
            }

            EnsureUiStyles();

            float width = Mathf.Max(1f, Mathf.Min(620f, Screen.width - 40f));
            float height = Mathf.Max(1f, Mathf.Min(265f, Screen.height - 36f));
            using (new GUILayout.AreaScope(new Rect((Screen.width - width) * 0.5f,
                Screen.height - height - 18f, width, height), GUIContent.none, GUI.skin.box))
            {
                GUILayout.Label(CurrentNode != null ? CurrentNode.DisplayTitle : "影游播放器", titleStyle);
                if (CurrentNode != null && !string.IsNullOrWhiteSpace(CurrentNode.caption))
                    GUILayout.Label(CurrentNode.caption, captionStyle);
                if (preparing) GUILayout.Label("正在准备视频…", captionStyle);
                if (!string.IsNullOrEmpty(PlaybackError)) GUILayout.Label(PlaybackError, captionStyle);

                GUILayout.FlexibleSpace();
                if (CurrentNode is FilmChoiceNode choice)
                {
                    using var scroll = new GUILayout.ScrollViewScope(choiceScroll, GUILayout.MaxHeight(145f));
                    choiceScroll = scroll.scrollPosition;
                    for (int i = 0; i < choice.OptionCount; i++)
                    {
                        string label = choice.GetLabel(i);
                        if (route.GetNext(choice, i) != null && GUILayout.Button(label, buttonStyle, GUILayout.Height(30f)))
                        {
                            Choose(i);
                            break;
                        }
                    }
                }
                else if (CurrentNode is FilmEndingNode)
                {
                    if (GUILayout.Button("重新开始", buttonStyle, GUILayout.Height(30f))) Restart();
                }
                else if (CurrentNode is FilmSegmentNode segment &&
                    GUILayout.Button(segment.video == null ? "继续" : "下一段", buttonStyle, GUILayout.Height(30f)))
                {
                    Advance();
                }
            }
        }

        private void EnsureUiStyles()
        {
            if (titleStyle != null) return;
            if (uiFont == null)
                generatedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "PingFang SC", "Noto Sans CJK SC", "SimHei" }, 16);
            Font font = uiFont != null ? uiFont : generatedFont;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true
            };
            captionStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15 };
        }
    }
}
