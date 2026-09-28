namespace Core;

public static class Printer
{
    public static string SupportTable(FirstPassResult program) =>
        Table([
            [Rules.Address(program.Start), "START", Rules.Address(program.Start)],
            .. program.Rows.Select(r => new[] { Rules.Address(r.Address), r.OpCode is null ? r.Source.Command : r.Code[..2] }.Concat(r.Source.Operands).ToArray()),
            [Rules.Address(program.Start + program.Length), "END", Rules.Address(program.Entry)],
        ]);

    public static string Symbols(FirstPassResult program) =>
        Table(program.Symbols.Select(s => new[] { s.Name, Rules.Address(s.Address) }));

    public static string Header(FirstPassResult program) =>
        Table([[program.Name, Rules.Address(program.Length), Rules.Address(program.Start)]]);

    public static string Lines(IEnumerable<string> lines) => string.Join(Environment.NewLine, lines);

    static string Table(IEnumerable<string[]> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
            return "";

        var widths = Enumerable.Range(0, list.Max(r => r.Length))
            .Select(i => list.Max(r => i < r.Length ? r[i].Length : 0))
            .ToArray();

        return string.Join(Environment.NewLine,
            list.Select(r => string.Join("  ", r.Select((cell, i) => cell.PadRight(widths[i]))).TrimEnd()));
    }
}
