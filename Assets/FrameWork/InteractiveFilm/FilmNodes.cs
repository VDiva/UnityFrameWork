using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;
using UnityEngine.Video;

namespace FrameWork.InteractiveFilm
{
    public enum FilmLink { Flow }

    [Serializable]
    public abstract class FilmNode : BaseNode
    {
        [LabelText("标题")] [ShowAsDrawer] public string title;
        public string DisplayTitle => string.IsNullOrWhiteSpace(title) ? name : title;
        internal virtual int OutputCount => 0;
        internal virtual int GetOutputIndex(string field, string id) => -1;
    }

    [Serializable]
    public abstract class FilmContentNode : FilmNode
    {
        [Input("进入", true), NonSerialized] public FilmLink previous;
        [LabelText("字幕")] [ShowAsDrawer, TextArea(2, 4)] public string caption;
    }

    [Serializable, NodeMenuItem("影游/入口", typeof(FilmGraph))]
    public sealed class FilmStartNode : FilmNode
    {
        [Output("开始", false), NonSerialized] public FilmLink next;
        public override string name => "入口";
        public override Color color => new Color(0.27f, 0.66f, 0.83f);
        internal override int OutputCount => 1;
        internal override int GetOutputIndex(string field, string id) => field == nameof(next) ? 0 : -1;
    }

    [Serializable, NodeMenuItem("影游/视频片段", typeof(FilmGraph))]
    public sealed class FilmSegmentNode : FilmContentNode
    {
        [LabelText("视频片段")] [ShowAsDrawer] public VideoClip video;
        [LabelText("封面图片")] [ShowAsDrawer] public Texture2D poster;
        [LabelText("播放结束后自动前进")] [ShowAsDrawer] public bool autoAdvance = true;
        [Output("下一段", false), NonSerialized] public FilmLink next;
        public override string name => "视频片段";
        public override Color color => new Color(0.34f, 0.58f, 0.80f);
        internal override int OutputCount => 1;
        internal override int GetOutputIndex(string field, string id) => field == nameof(next) ? 0 : -1;
    }

    [Serializable, NodeMenuItem("影游/选项分支", typeof(FilmGraph))]
    public sealed class FilmChoiceNode : FilmContentNode
    {
        [Serializable]
        private sealed class Option
        {
            [LabelText("选项标识")] public string id;
            [LabelText("选项文字")] public string label;

            public Option(string id, string label)
            {
                this.id = id;
                this.label = label;
            }
        }

        [SerializeField, HideInInspector] private List<Option> options = new List<Option>();
        [SerializeField, HideInInspector] private bool optionsMigrated;
        [SerializeField, HideInInspector] private string firstLabel;
        [SerializeField, HideInInspector] private string secondLabel;
        [SerializeField, HideInInspector] private string thirdLabel;
        [Output("选项", false), NonSerialized] public FilmLink optionsOutput;

        public override string name => "选项分支";
        public override Color color => new Color(0.84f, 0.62f, 0.34f);
        public int OptionCount => options.Count;
        internal override int OutputCount => OptionCount;

        public string GetLabel(int index) => index >= 0 && index < options.Count ? options[index].label : null;

        public string GetOptionId(int index) => index >= 0 && index < options.Count ? options[index].id : null;

        public string AddOption(string label)
        {
            optionsMigrated = true;
            var option = new Option(Guid.NewGuid().ToString("N"), label);
            options.Add(option);
            UpdatePortsForField(nameof(optionsOutput));
            return option.id;
        }

        public void RenameOption(string id, string label)
        {
            var option = options.Find(value => value.id == id);
            if (option != null) option.label = label;
        }

        public void RemoveOption(string id)
        {
            int index = options.FindIndex(option => option.id == id);
            if (index < 0) return;

            var port = GetPort(nameof(optionsOutput), id);
            if (port != null)
                foreach (var edge in new List<SerializableEdge>(port.GetEdges()))
                    graph.Disconnect(edge);
            options.RemoveAt(index);
            UpdatePortsForField(nameof(optionsOutput));
        }

        internal override int GetOutputIndex(string field, string id) =>
            field == nameof(optionsOutput) ? options.FindIndex(option => option.id == id) : -1;

        internal void MigrateLegacyOptions(bool hasLegacyEdges)
        {
            if (optionsMigrated) return;
            optionsMigrated = true;
            if (options.Count != 0 || (!hasLegacyEdges && string.IsNullOrEmpty(firstLabel) &&
                string.IsNullOrEmpty(secondLabel) && string.IsNullOrEmpty(thirdLabel))) return;

            options.Add(new Option(LegacyPortId("first"), firstLabel));
            options.Add(new Option(LegacyPortId("second"), secondLabel));
            options.Add(new Option(LegacyPortId("third"), thirdLabel));
        }

        internal static string LegacyPortId(string field) => field switch
        {
            "first" => "legacy-first",
            "second" => "legacy-second",
            "third" => "legacy-third",
            _ => null
        };

        [CustomPortBehavior(nameof(optionsOutput))]
        private IEnumerable<PortData> BuildOptionPorts(List<SerializableEdge> edges)
        {
            for (int i = 0; i < options.Count; i++)
            {
                yield return new PortData
                {
                    identifier = options[i].id,
                    displayName = $"选项 {i + 1}",
                    displayType = typeof(FilmLink),
                    acceptMultipleEdges = false
                };
            }
        }
    }

    [Serializable, NodeMenuItem("影游/结局", typeof(FilmGraph))]
    public sealed class FilmEndingNode : FilmContentNode
    {
        public override string name => "结局";
        public override Color color => new Color(0.39f, 0.70f, 0.52f);
    }
}
