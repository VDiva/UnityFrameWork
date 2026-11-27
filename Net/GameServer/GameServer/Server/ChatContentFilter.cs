using System.Text;

namespace WebSocketDemo;

/// <summary>
/// 基于 Aho-Corasick 多模式自动机的服务端敏感词过滤器。
/// 无论词库规模多大，均可在单趟 O(N) 扫描内完成匹配，常规已归一化消息零字符串分配。
/// </summary>
public static class ChatContentFilter
{
    private sealed class Automaton
    {
        public struct Node
        {
            public Dictionary<char, int>? Edges;
            public int Fail;
            public int MatchWordIndex;
        }

        public readonly Node[] Nodes;
        public readonly string[] RawWords;

        public Automaton(Node[] nodes, string[] rawWords)
        {
            Nodes = nodes;
            RawWords = rawWords;
        }
    }

    private static readonly string[] DefaultWords = ["赌博", "代充", "外挂", "私服", "加群", "刷钻"];
    private static readonly Lazy<Automaton> SharedAutomaton = new(BuildAutomaton);

    public static bool TryFindForbiddenWord(string content, out string matchedWord)
    {
        if (string.IsNullOrEmpty(content))
        {
            matchedWord = string.Empty;
            return false;
        }

        var automaton = SharedAutomaton.Value;
        if (automaton.RawWords.Length == 0)
        {
            matchedWord = string.Empty;
            return false;
        }

        // 绝大多数常规 UTF-16 文本已处于 FormKC，免去 Normalize 字符串拷贝与 StringBuilder 分配。
        ReadOnlySpan<char> chars = content.IsNormalized(NormalizationForm.FormKC)
            ? content.AsSpan()
            : content.Normalize(NormalizationForm.FormKC).AsSpan();

        var nodes = automaton.Nodes;
        int state = 0;
        for (int i = 0; i < chars.Length; i++)
        {
            char character = chars[i];
            if (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character))
                continue;

            char ch = char.ToLowerInvariant(character);
            while (state != 0 && !TryStep(ref nodes[state], ch, out _))
                state = nodes[state].Fail;

            if (TryStep(ref nodes[state], ch, out int next))
                state = next;

            int matchIdx = nodes[state].MatchWordIndex;
            if (matchIdx >= 0)
            {
                matchedWord = automaton.RawWords[matchIdx];
                return true;
            }
        }

        matchedWord = string.Empty;
        return false;
    }

    private static bool TryStep(ref Automaton.Node node, char ch, out int next)
    {
        if (node.Edges != null && node.Edges.TryGetValue(ch, out next))
            return true;
        next = 0;
        return false;
    }

    private static Automaton BuildAutomaton()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Config", "forbidden-words.txt");
        IEnumerable<string> lines = File.Exists(path) ? File.ReadLines(path) : DefaultWords;

        var rawList = new List<string>();
        var nodes = new List<Automaton.Node>
        {
            new() { Fail = 0, MatchWordIndex = -1 }
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rawLine in lines)
        {
            string raw = rawLine.Trim();
            if (raw.Length == 0 || raw.StartsWith('#'))
                continue;

            string normalized = Normalize(raw);
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;

            int wordIndex = rawList.Count;
            rawList.Add(raw);

            int current = 0;
            foreach (char ch in normalized)
            {
                var currentNode = nodes[current];
                currentNode.Edges ??= new Dictionary<char, int>();
                if (!currentNode.Edges.TryGetValue(ch, out int next))
                {
                    next = nodes.Count;
                    currentNode.Edges[ch] = next;
                    nodes[current] = currentNode;
                    nodes.Add(new Automaton.Node { Fail = 0, MatchWordIndex = -1 });
                }
                else
                {
                    nodes[current] = currentNode;
                }
                current = next;
            }

            var leaf = nodes[current];
            if (leaf.MatchWordIndex < 0)
            {
                leaf.MatchWordIndex = wordIndex;
                nodes[current] = leaf;
            }
        }

        var nodeArray = nodes.ToArray();
        var rootEdges = nodeArray[0].Edges;
        if (rawList.Count > 0 && rootEdges != null)
        {
            var queue = new Queue<int>();
            foreach (var pair in rootEdges)
            {
                nodeArray[pair.Value].Fail = 0;
                queue.Enqueue(pair.Value);
            }

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                var edges = nodeArray[u].Edges;
                if (edges == null)
                    continue;

                foreach (var pair in edges)
                {
                    char ch = pair.Key;
                    int v = pair.Value;
                    int f = nodeArray[u].Fail;
                    while (f != 0 && (nodeArray[f].Edges == null || !nodeArray[f].Edges!.ContainsKey(ch)))
                        f = nodeArray[f].Fail;

                    if (nodeArray[f].Edges != null && nodeArray[f].Edges!.TryGetValue(ch, out int failTarget) && failTarget != v)
                        f = failTarget;

                    nodeArray[v].Fail = f;
                    if (nodeArray[v].MatchWordIndex < 0 && nodeArray[f].MatchWordIndex >= 0)
                        nodeArray[v].MatchWordIndex = nodeArray[f].MatchWordIndex;

                    queue.Enqueue(v);
                }
            }
        }

        return new Automaton(nodeArray, rawList.ToArray());
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value.Normalize(NormalizationForm.FormKC))
        {
            if (!char.IsWhiteSpace(character) && !char.IsPunctuation(character) && !char.IsSymbol(character))
                result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }
}
