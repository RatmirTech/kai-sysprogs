namespace Core;

public static class Printer
{
    public static string SupportTable(FirstPassResult program)
    {
        var rows = new List<string[]>();
        var sections = program.Sections;
        for (var i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            rows.Add([Rules.Address(0), s.Directive, s.Name]);
            if (s.ExtDefs.Count > 0)
                rows.Add(["", "EXTDEF", .. s.ExtDefs]);
            if (s.ExtRefs.Count > 0)
                rows.Add(["", "EXTREF", .. s.ExtRefs]);
            rows.AddRange(s.Rows.Select(r => new[] { Rules.Address(r.Address), r.OpCode is null ? r.Source.Command : r.Code[..2] }.Concat(r.Source.Operands).ToArray()));
            rows.Add([Rules.Address(s.Length), i == sections.Count - 1 ? "END" : "КОНЕЦ", Rules.Address(s.Entry)]);
        }

        return Table(rows);
    }

    public static string Symbols(FirstPassResult program) =>
        Table(program.Sections.SelectMany(sec => sec.Symbols.Select(s => new[] { sec.Name, s.Name, Rules.Address(s.Address), s.External ? "EXTDEF" : "" })));

    public static string Externals(FirstPassResult program) =>
        Table(program.Sections.SelectMany(sec =>
            sec.ExtDefs.Select(n => new[] { sec.Name, "EXTDEF", n, Rules.Address(sec.Symbols.First(s => s.Name == n).Address) })
                .Concat(sec.ExtRefs.Select(n => new[] { sec.Name, "EXTREF", n }))));

    public static string Header(FirstPassResult program) =>
        Table(program.Sections.Select(s => new[] { s.Name, Rules.Address(0), Rules.Address(s.Length) }));

    public static string Relocations(IEnumerable<Relocation> relocations) =>
        Table(relocations.Select(r => new[] { r.Section, Rules.Address(r.Address), r.Reference ?? "" }));

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
