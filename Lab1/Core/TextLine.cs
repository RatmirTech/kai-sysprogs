using System.Text;

namespace Core;

public static class TextLine
{
    public static string[] Split(string text) => text.ReplaceLineEndings("\n").Split('\n');

    public static (string Code, string Comment) SplitComment(string line)
    {
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
                quoted = !quoted;
            if (line[i] == ';' && !quoted)
                return (line[..i], line[i..]);
        }

        return (line, "");
    }

    public static string WithoutComment(string line)
    {
        var (code, comment) = SplitComment(line);
        return comment.Length > 0 ? code.TrimEnd(' ') : code;
    }

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
                throw fail(i + 1, "табуляция недопустима, используйте пробел");
            if (c == '"')
                quoted = !quoted;
            if (!quoted && c is < ' ' or > '~')
                throw fail(i + 1, $"недопустимый символ «{c}» (U+{(int)c:X4}): вне кавычек разрешены только латинские буквы, цифры и знаки ASCII");

            result.Append(quoted || c == '"' ? c : char.ToUpperInvariant(c));
        }

        if (quoted)
            throw fail(line.LastIndexOf('"') + 1, "не закрыта кавычка");
        if (line.EndsWith(' '))
            throw fail(line.TrimEnd().Length + 1, "лишние пробелы в конце строки");

        return result.ToString();
    }

    public static List<(string Text, int Position)> Parts(string line, int start, Func<int, string, AsmException> fail)
    {
        var parts = new List<(string Text, int Position)>();
        var quoted = false;
        var begin = start;

        for (var i = start; i < line.Length; i++)
        {
            if (line[i] == '"')
                quoted = !quoted;
            if (quoted || line[i] != ' ')
                continue;

            if (line[i + 1] == ' ')
                throw fail(i + 2, "между частями строки должен быть ровно один пробел");

            parts.Add((line[begin..i], begin + 1));
            begin = i + 1;
        }

        parts.Add((line[begin..], begin + 1));
        return parts;
    }

    public static (int Line, int Column) Position(string text, int index)
    {
        var before = text[..Math.Clamp(index, 0, text.Length)];
        return (before.Count(c => c == '\n') + 1, before.Length - before.LastIndexOf('\n'));
    }
}
