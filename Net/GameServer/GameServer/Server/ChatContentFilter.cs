using System.Text;

namespace WebSocketDemo;

/// <summary>Loads and applies the server-authoritative forbidden-word rules.</summary>
public static class ChatContentFilter
{
    private static readonly string[] DefaultWords = ["赌博", "代充", "外挂", "私服", "加群", "刷钻"];
    private static readonly Lazy<string[]> ForbiddenWords = new(LoadWords);

    public static bool TryFindForbiddenWord(string content, out string matchedWord)
    {
        string normalizedContent = Normalize(content);
        foreach (string word in ForbiddenWords.Value)
        {
            if (normalizedContent.Contains(Normalize(word), StringComparison.OrdinalIgnoreCase))
            {
                matchedWord = word;
                return true;
            }
        }

        matchedWord = string.Empty;
        return false;
    }

    private static string[] LoadWords()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Config", "forbidden-words.txt");
        IEnumerable<string> words = File.Exists(path) ? File.ReadLines(path) : DefaultWords;
        return words.Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
