namespace Core;

public static class LoadAddress
{
    sealed record StartLine(int Index, string? Label, string Command, string Operand, string[] Rest, string Tail);

    public static string? Read(string source) => Find(TextLine.Split(source))?.Operand;

    public static string Write(string source, string address)
    {
        var lines = TextLine.Split(source);
        if (Find(lines) is not { } start)
            return source;

        var parts = new List<string> { start.Label ?? "", start.Command };
        var clean = address.Replace(" ", "");
        if (clean.Length > 0)
            parts.Add(clean);
        parts.AddRange(start.Rest);

        lines[start.Index] = string.Join(' ', parts) + start.Tail;
        return string.Join('\n', lines);
    }

    static StartLine? Find(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var (code, comment) = TextLine.SplitComment(lines[i]);
            if (code.Trim().Length == 0)
                continue;

            var tokens = code.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var hasLabel = code[0] != ' ';
            var index = hasLabel ? 1 : 0;
            if (tokens.Length <= index || !tokens[index].Equals("START", StringComparison.OrdinalIgnoreCase))
                return null;

            return new StartLine(
                i,
                hasLabel ? tokens[0] : null,
                tokens[index],
                tokens.Length > index + 1 ? tokens[index + 1] : "",
                tokens[Math.Min(index + 2, tokens.Length)..],
                code[code.TrimEnd().Length..] + comment);
        }

        return null;
    }
}
