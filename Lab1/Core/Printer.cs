namespace Core;

public static class Printer
{
    public static string SupportTable(FirstPassResult program) =>
        Table([["Адрес", "Команда", "Биты 1-го байта", "Код", "Метка"], .. program.Rows.Select(SupportRow)]);

    public static string Symbols(FirstPassResult program) =>
        Table([["Метка", "Адрес"], .. program.Symbols.Select(s => new[] { s.Name, Rules.Address(s.Address) })]);

    public static string Header(FirstPassResult program) =>
        Table([[program.Name, Rules.Address(program.Length), Rules.Address(program.Start)]]);

    public static string Lines(IEnumerable<string> lines) => string.Join(Environment.NewLine, lines);

    static string[] SupportRow(SupportRow row)
    {
        var address = Rules.Address(row.Address);
        var command = row.Source.Command;

        if (row.OpCode is not { } op)
            return [address, command, "", row.Code.Length > 0 ? row.Code : $"резерв {row.Size:X2}", ""];

        var bits = Convert.ToString(op.MachineCode, 2).PadLeft(8, '0');
        var code = row.Code.Length > 2 ? $"{row.Code[..2]} {row.Code[2..]}" : row.Code;
        if (row.Label is not null)
            code += " " + new string('_', op.AddressDigits);

        return [address, command, $"{bits[..6]} {bits[6..]}", code, row.Label ?? ""];
    }

    static string Table(IEnumerable<string[]> rows)
    {
        var list = rows.ToList();
        var widths = Enumerable.Range(0, list.Max(r => r.Length))
            .Select(i => list.Max(r => i < r.Length ? r[i].Length : 0))
            .ToArray();

        return string.Join(Environment.NewLine,
            list.Select(r => string.Join("  ", r.Select((cell, i) => cell.PadRight(widths[i]))).TrimEnd()));
    }
}
