namespace Core;

public static class Printer
{
    /// <summary>ТСИ по секциям: секция, имя, адрес (------ пока не определена), EXTDEF, адреса команд, ждущих метку.</summary>
    public static string Symbols(IEnumerable<SectionState> sections) =>
        Table(sections.SelectMany(sec => sec.Symbols.Values.Select(s => new[]
        {
            sec.Name,
            s.Name,
            s.Address is { } a ? Rules.Address(a) : new string('-', Rules.AddressDigits),
            s.External ? "EXTDEF" : "",
            string.Join(" ", s.Pending.Select(p => Rules.Address(p.Address))),
        })));

    /// <summary>Внешние имена и ссылки по секциям: секция, тип, имя, адрес (только для EXTDEF).</summary>
    public static string Externals(IEnumerable<SectionState> sections) =>
        Table(sections.SelectMany(sec =>
            sec.ExtDefs.Keys.Select(n => new[]
            {
                sec.Name,
                "EXTDEF",
                n,
                sec.Symbols.TryGetValue(n, out var s) && s.Address is { } a ? Rules.Address(a) : new string('-', Rules.AddressDigits),
            })
            .Concat(sec.ExtRefs.Keys.Select(n => new[] { sec.Name, "EXTREF", n, "" }))));

    /// <summary>Таблица настройки по секциям: секция, адрес, внешняя ссылка (если адрес подставит загрузчик).</summary>
    public static string Relocations(IEnumerable<SectionState> sections) =>
        Table(sections.SelectMany(sec => sec.Relocations.Select(r => new[]
        {
            sec.Name,
            Rules.Address(r.Address),
            r.Reference ?? "",
        })));

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
