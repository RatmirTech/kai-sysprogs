namespace Core;

public static class SourceParser
{
    public static SourceLine? Parse(int number, string raw, OpCodeTable opCodes)
    {
        var code = TextLine.WithoutComment(raw);
        return code.Length == 0 ? null : ParseLine(number, code, opCodes);
    }

    static SourceLine ParseLine(int number, string raw, OpCodeTable opCodes)
    {
        AsmException Fail(int position, string message) => AsmException.AtSource(number, position, message);
        bool IsReserved(string word) => Rules.IsDirective(word) || opCodes.Contains(word);

        var line = TextLine.Normalize(raw, Fail);
        var hasLabel = line[0] != ' ';
        if (!hasLabel && line[1] == ' ')
            throw Fail(2, "строка без метки начинается ровно с одного пробела");

        var parts = TextLine.Parts(line, hasLabel ? 0 : 1, Fail);

        string? label = null;
        if (hasLabel)
        {
            label = parts[0].Text;
            parts.RemoveAt(0);

            if (IsReserved(label))
                throw Fail(1, $"метка {label} совпадает с зарезервированным словом (строка без метки начинается с пробела)");
            if (Rules.IsRegister(label))
                throw Fail(1, $"метка {label} совпадает с именем регистра");
            if (!Rules.IsName(label))
                throw Fail(1, $"метка «{label}» должна начинаться с латинской буквы и состоять из латинских букв и цифр");
            if (label.Length > Rules.MaxNameLength)
                throw Fail(1, $"метка длиннее {Rules.MaxNameLength} символов");
        }

        if (parts.Count == 0)
            throw Fail(line.Length + 1, $"после метки {label} нет команды");

        var (command, commandPosition) = parts[0];
        var operands = parts.Skip(1).ToList();

        if (!IsReserved(command))
            throw Fail(commandPosition, $"команды «{command}» нет ни в ТКО, ни среди директив");

        if (operands.Count > 2)
            throw Fail(operands[2].Position, $"у {command} {operands.Count} операнда, допускается не более 2");

        foreach (var (operand, position) in operands)
            if (operand.Length > Rules.MaxOperandLength)
                throw Fail(position, $"операнд длиннее {Rules.MaxOperandLength} символов");

        return new SourceLine(number, label, command, [.. operands.Select(o => o.Text)], [.. operands.Select(o => o.Position)]);
    }
}
