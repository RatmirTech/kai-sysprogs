using System.Text;

namespace Core;

public static class Tokenizer
{
    public static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');

    public static bool TrySplit(string line, out List<string> tokens)
    {
        tokens = [];
        var current = new StringBuilder();
        var quoted = false;

        foreach (var c in line)
        {
            if (c == '"')
                quoted = !quoted;

            if (!quoted && char.IsWhiteSpace(c))
            {
                Flush(current, tokens);
                continue;
            }

            current.Append(quoted || c == '"' ? c : char.ToUpperInvariant(c));
        }

        Flush(current, tokens);
        return !quoted;
    }

    static void Flush(StringBuilder current, List<string> tokens)
    {
        if (current.Length == 0)
            return;

        tokens.Add(current.ToString());
        current.Clear();
    }
}
