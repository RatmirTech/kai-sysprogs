namespace Core;

public static class Printer
{
    public static string SupportTable(FirstPassResult program) =>
        Table(program.Rows.Select(r => new[] { r.Address.ToString("X6"), r.Code }.Concat(r.Source.Operands).ToArray()));

    public static string Symbols(FirstPassResult program) =>
        Table(program.Symbols.Select(s => new[] { s.Name, s.Address.ToString("X6") }));

    public static string Header(FirstPassResult program) =>
        Table([[program.Name, program.Start.ToString("X6"), program.Length.ToString("X6")]]);

    public static string ObjectModule(IEnumerable<string> records) => string.Join(Environment.NewLine, records);

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
