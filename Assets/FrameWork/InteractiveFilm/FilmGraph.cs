using System.Collections.Generic;
using GraphProcessor;

namespace FrameWork.InteractiveFilm
{
    public sealed class FilmGraph : BaseGraph
    {
        protected override void OnEnable()
        {
            bool hasLegacyEdges = false;
            foreach (var edge in edges)
            {
                if (edge == null) continue;
                string id = FilmChoiceNode.LegacyPortId(edge.outputFieldName);
                if (id == null) continue;

                hasLegacyEdges = true;
                edge.outputFieldName = nameof(FilmChoiceNode.optionsOutput);
                edge.outputPortIdentifier = id;
            }
            foreach (var node in nodes)
                if (node is FilmChoiceNode choice) choice.MigrateLegacyOptions(hasLegacyEdges);

            base.OnEnable();
        }

        public string ValidateGraph()
        {
            TryBuild(out _, out var error);
            return error;
        }

        internal bool TryBuild(out FilmRoute route, out string error)
        {
            route = null;
            error = null;
            FilmStartNode entry = null;
            var routes = new Dictionary<FilmNode, FilmContentNode[]>();
            foreach (var source in nodes)
            {
                if (!(source is FilmNode node) || routes.ContainsKey(node))
                {
                    error = "存在无效或重复节点。";
                    return false;
                }
                routes.Add(node, new FilmContentNode[node.OutputCount]);
                if (node is FilmStartNode start)
                {
                    if (entry != null)
                    {
                        error = "影游只能有一个入口。";
                        return false;
                    }
                    entry = start;
                }
            }

            if (entry == null)
            {
                error = "缺少影游入口。";
                return false;
            }

            foreach (var edge in edges)
            {
                if (edge == null || !(edge.outputNode is FilmNode source) ||
                    !(edge.inputNode is FilmContentNode target) || !routes.TryGetValue(source, out var options) ||
                    !routes.ContainsKey(target) || edge.inputFieldName != nameof(FilmContentNode.previous))
                {
                    error = "存在无效连线。";
                    return false;
                }

                int index = source.GetOutputIndex(edge.outputFieldName, edge.outputPortIdentifier);
                if (index < 0 || index >= options.Length || options[index] != null)
                {
                    error = source.DisplayTitle + " 的每个输出端口只能连接一个节点。";
                    return false;
                }
                options[index] = target;
            }

            foreach (var pair in routes)
            {
                var node = pair.Key;
                var options = pair.Value;
                if (node is FilmChoiceNode choice)
                {
                    bool hasChoice = false;
                    for (int i = 0; i < options.Length; i++)
                    {
                        bool hasText = !string.IsNullOrWhiteSpace(choice.GetLabel(i));
                        if (hasText != (options[i] != null))
                        {
                            error = $"{choice.DisplayTitle} 的选项 {i + 1} 需要同时填写文字和连接目标。";
                            return false;
                        }
                        hasChoice |= hasText;
                    }
                    if (hasChoice) continue;
                    error = choice.DisplayTitle + " 至少需要一个有连线的选项。";
                    return false;
                }

                if (options.Length > 0 && options[0] == null)
                {
                    error = node.DisplayTitle + " 缺少下一段连线。";
                    return false;
                }
            }

            var reached = new HashSet<FilmNode>();
            var pending = new Stack<FilmNode>();
            pending.Push(entry);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!reached.Add(node)) continue;
                foreach (var neighbor in routes[node])
                    if (neighbor != null) pending.Push(neighbor);
            }
            if (reached.Count != routes.Count)
            {
                error = "存在无法从入口到达的节点。";
                return false;
            }

            route = new FilmRoute(entry, routes);
            return true;
        }
    }

    internal sealed class FilmRoute
    {
        private readonly Dictionary<FilmNode, FilmContentNode[]> paths;
        public FilmContentNode Start { get; }

        public FilmRoute(FilmStartNode entry, Dictionary<FilmNode, FilmContentNode[]> paths)
        {
            this.paths = paths;
            Start = paths[entry][0];
        }

        public FilmContentNode GetNext(FilmNode node, int option)
        {
            return node != null && paths.TryGetValue(node, out var choices) && option >= 0 && option < choices.Length
                ? choices[option] : null;
        }
    }
}
