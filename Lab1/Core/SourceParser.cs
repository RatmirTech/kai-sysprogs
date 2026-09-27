namespace Core;

public static class SourceParser
{
    public static List<SourceLine> Parse(string text, OpCodeTable opCodes)
    {
        var result = new List<SourceLine>();
        var lines = Tokenizer.Lines(text);

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;
            if (!Tokenizer.TrySplit(lines[i], out var tokens))
                throw AsmException.AtSource(number, "не закрыта кавычка");
            if (tokens.Count > 0)
                result.Add(ParseLine(number, tokens, opCodes));
        }

        return result;
    }

    static SourceLine ParseLine(int number, List<string> tokens, OpCodeTable opCodes)
    {
        bool IsCommand(string token) => Rules.IsDirective(token) || opCodes.Contains(token);

        string? label = null;
        if (!IsCommand(tokens[0]))
        {
            if (tokens.Count == 1)
                throw AsmException.AtSource(number, $"«{tokens[0]}» нет ни в ТКО, ни среди директив");
            if (!IsCommand(tokens[1]))
                throw AsmException.AtSource(number, $"не найдена команда: ни «{tokens[0]}», ни «{tokens[1]}» нет в ТКО и среди директив");

            label = tokens[0];
            CheckLabel(number, label);
        }

        var commandIndex = label is null ? 0 : 1;
        var command = tokens[commandIndex];
        var operands = tokens.Skip(commandIndex + 1).ToArray();

        if (operands.Length > 2)
            throw AsmException.AtSource(number, $"у {command} {operands.Length} операнда, допускается не более 2");

        foreach (var operand in operands)
            if (IsCommand(operand))
                throw AsmException.AtSource(number, $"{operand} - зарезервированное слово, его нельзя использовать как операнд");

        return new SourceLine(number, label, command, operands);
    }

    static void CheckLabel(int number, string label)
    {
        if (!Rules.IsName(label))
            throw AsmException.AtSource(number, $"метка «{label}» должна начинаться с латинской буквы и состоять из латинских букв и цифр");
        if (label.Length > Rules.MaxNameLength)
            throw AsmException.AtSource(number, $"метка {label} длиннее {Rules.MaxNameLength} символов");
        if (Rules.IsRegister(label))
            throw AsmException.AtSource(number, $"метка {label} совпадает с именем регистра");
    }
}
