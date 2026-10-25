namespace Core;

public static class Printer
{
    /// <summary>ТСИ: имя, адрес (------ пока не определена), адреса команд, ждущих метку.</summary>
    public static string Symbols(IEnumerable<Symbol> symbols) =>
        Table(symbols.Select(s => new[]
        {
            s.Name,
            s.Address is { } a ? Rules.Address(a) : new string('-', Rules.AddressDigits),
            string.Join(" ", s.Pending.Select(p => Rules.Address(p.Address))),
        }));

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
