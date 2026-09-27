namespace Core;

public static class SourceParser
{
    public static List<SourceLine> Parse(string text, OpCodeTable opCodes)
    {
        var result = new List<SourceLine>();
        var lines = TextLine.Split(text);

        for (var i = 0; i < lines.Length; i++)
            if (lines[i].Length > 0)
                result.Add(ParseLine(i + 1, lines[i], opCodes));

        return result;
    }

    static SourceLine ParseLine(int number, string raw, OpCodeTable opCodes)
    {
        AsmException Fail(int position, string message) => AsmException.AtSource(number, position, message);
        bool IsReserved(string word) => Rules.IsDirective(word) || opCodes.Contains(word);

        var line = TextLine.Normalize(raw, Fail);
        var label = ReadLabel(line, Fail);
        var command = ReadCommand(line, Fail);
        var operands = ReadOperands(line, Fail);

        if (label is not null)
        {
            if (!Rules.IsName(label))
                throw Fail(1, $"метка «{label}» должна начинаться с латинской буквы и состоять из латинских букв и цифр");
            if (Rules.IsRegister(label))
                throw Fail(1, $"метка {label} совпадает с именем регистра");
            if (IsReserved(label))
                throw Fail(1, $"метка {label} совпадает с зарезервированным словом");
        }

        if (!IsReserved(command))
            throw Fail(Layout.CommandPosition, $"команды «{command}» нет ни в ТКО, ни среди директив");

        if (operands.Count > 2)
            throw Fail(operands[2].Position, $"у {command} {operands.Count} операнда, допускается не более 2");

        foreach (var (operand, position) in operands)
            if (IsReserved(operand))
                throw Fail(position, $"{operand} - зарезервированное слово, его нельзя использовать как операнд");

        return new SourceLine(number, label, command, [.. operands.Select(o => o.Text)]);
    }

    static string? ReadLabel(string line, Func<int, string, AsmException> fail)
    {
        var field = TextLine.Field(line, 0, Rules.MaxNameLength);
        var label = field.Trim();

        if (label.Length > 0 && field[0] == ' ')
            throw fail(TextLine.FirstNonSpace(line, 0) + 1, $"метка пишется с позиции 1, команда - с позиции {Layout.CommandPosition}");
        if (label.Contains(' '))
            throw fail(label.IndexOf(' ') + 1, $"после метки команда пишется с позиции {Layout.CommandPosition}");
        if (line.Length > Rules.MaxNameLength && line[Rules.MaxNameLength] != ' ')
            throw fail(Rules.MaxNameLength + 1, label.Length > 0
                ? $"метка длиннее {Rules.MaxNameLength} символов"
                : $"команда пишется с позиции {Layout.CommandPosition}");

        return label.Length > 0 ? label : null;
    }

    static string ReadCommand(string line, Func<int, string, AsmException> fail)
    {
        var start = Layout.CommandPosition - 1;
        if (line.Length <= start)
            throw fail(line.Length + 1, $"нет команды: она пишется с позиции {Layout.CommandPosition}");

        var field = TextLine.Field(line, start, Rules.MaxMnemonicLength);
        if (field[0] == ' ')
            throw fail(TextLine.FirstNonSpace(line, start) + 1, $"команда пишется с позиции {Layout.CommandPosition}");

        var command = field.TrimEnd();
        if (command.Contains(' '))
            throw fail(start + command.IndexOf(' ') + 1, $"операнды пишутся с позиции {Layout.OperandPosition}");

        var separator = Layout.OperandPosition - 2;
        if (line.Length > separator && line[separator] != ' ')
            throw fail(separator + 1, $"команда длиннее {Rules.MaxMnemonicLength} символов");

        return command;
    }

    static List<(string Text, int Position)> ReadOperands(string line, Func<int, string, AsmException> fail)
    {
        var start = Layout.OperandPosition - 1;
        var operands = new List<(string Text, int Position)>();
        if (line.Length <= start)
            return operands;

        if (line[start] == ' ')
            throw fail(TextLine.FirstNonSpace(line, start) + 1, $"операнд пишется с позиции {Layout.OperandPosition}");

        var quoted = false;
        for (var i = start; i < line.Length; i++)
        {
            if (line[i] == '"')
                quoted = !quoted;
            if (quoted || line[i] != ' ')
                continue;

            if (line[i + 1] == ' ')
                throw fail(i + 2, "между операндами должен быть ровно один пробел");

            operands.Add((line[start..i], start + 1));
            start = i + 1;
        }

        operands.Add((line[start..], start + 1));
        return operands;
    }
}
