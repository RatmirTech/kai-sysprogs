using System.Text;

namespace Core;

public static class TextLine
{
    public static string[] Split(string text) => text.ReplaceLineEndings("\n").Split('\n');

    public static string Normalize(string line, Func<int, string, AsmException> fail)
    {
        if (line.Length > 0 && line.Trim().Length == 0)
            throw fail(1, "строка состоит только из пробелов");

        var result = new StringBuilder(line.Length);
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\t')
                throw fail(i + 1, "табуляция недопустима, используйте пробелы");
            if (c == '"')
                quoted = !quoted;

            result.Append(quoted || c == '"' ? c : char.ToUpperInvariant(c));
        }

        if (quoted)
            throw fail(line.LastIndexOf('"') + 1, "не закрыта кавычка");
        if (line.EndsWith(' '))
            throw fail(line.TrimEnd().Length + 1, "лишние пробелы в конце строки");

        return result.ToString();
    }

    public static string Field(string line, int start, int width) =>
        start >= line.Length ? "" : line.Substring(start, Math.Min(width, line.Length - start));

    public static int FirstNonSpace(string line, int start) => line.Length - line[start..].TrimStart().Length;

    public static (int Line, int Column) Position(string text, int index)
    {
        var before = text[..Math.Clamp(index, 0, text.Length)];
        return (before.Count(c => c == '\n') + 1, before.Length - before.LastIndexOf('\n'));
    }
}
