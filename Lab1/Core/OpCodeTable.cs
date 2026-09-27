namespace Core;

public sealed class OpCodeTable
{
    readonly Dictionary<string, OpCode> byName;

    OpCodeTable(List<OpCode> items)
    {
        Items = items;
        byName = items.ToDictionary(o => o.Name);
    }

    public IReadOnlyList<OpCode> Items { get; }

    public OpCode? Find(string name) => byName.GetValueOrDefault(name);

    public bool Contains(string name) => byName.ContainsKey(name);

    public static OpCodeTable Parse(string text, List<AsmException> errors)
    {
        var items = new List<(OpCode Op, int Line)>();
        var lines = TextLine.Split(text);
        var before = errors.Count;

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;
            var code = TextLine.WithoutComment(lines[i]);
            if (code.Length == 0)
                continue;

            try
            {
                var op = ParseLine(number, code);

                var sameName = items.Find(x => x.Op.Name == op.Name);
                if (sameName.Op is not null)
                    throw AsmException.AtOpCodes(number, 1, $"мнемоника {op.Name} уже задана в строке {sameName.Line}");

                var sameCode = items.Find(x => x.Op.Code == op.Code);
                if (sameCode.Op is not null)
                    throw AsmException.AtOpCodes(number, $"код {op.Code:X2} уже занят командой {sameCode.Op.Name} (строка {sameCode.Line})");

                items.Add((op, number));
            }
            catch (AsmException e)
            {
                errors.Add(e);
            }
        }

        if (items.Count == 0 && errors.Count == before)
            errors.Add(new AsmException("ТКО пуста"));

        return new OpCodeTable([.. items.Select(x => x.Op)]);
    }

    static OpCode ParseLine(int number, string raw)
    {
        AsmException Fail(int position, string message) => AsmException.AtOpCodes(number, position, message);

        var line = TextLine.Normalize(raw, Fail);
        if (line[0] == ' ')
            throw Fail(1, "строка ТКО начинается с мнемоники, без пробела");

        var parts = TextLine.Parts(line, 0, Fail);
        if (parts.Count != 3)
            throw Fail(parts.Count > 3 ? parts[3].Position : line.Length + 1, $"ожидается 3 части «мнемоника код длина», найдено {parts.Count}");

        var ((name, namePosition), (code, codePosition), (length, lengthPosition)) = (parts[0], parts[1], parts[2]);

        if (!Rules.IsName(name))
            throw Fail(namePosition, $"мнемоника «{name}» должна начинаться с латинской буквы и состоять из латинских букв и цифр");
        if (name.Length > Rules.MaxMnemonicLength)
            throw Fail(namePosition, $"мнемоника {name} длиннее {Rules.MaxMnemonicLength} символов");
        if (Rules.IsDirective(name))
            throw Fail(namePosition, $"мнемоника {name} совпадает с директивой");
        if (Rules.IsRegister(name))
            throw Fail(namePosition, $"мнемоника {name} совпадает с именем регистра");

        if (!Rules.IsHex(code) || code.Length > Rules.OpCodeDigits)
            throw Fail(codePosition, $"код «{code}» должен быть 16-ричным числом из 1-2 цифр");
        var value = Convert.ToInt32(code, 16);
        if (value > Rules.MaxOpCode)
            throw Fail(codePosition, $"код {value:X2} больше {Rules.MaxOpCode:X2}");

        if (length is not ("1" or "2" or "3" or "4"))
            throw Fail(lengthPosition, $"длина «{length}» должна быть числом от 1 до 4");

        return new OpCode(name, value, int.Parse(length));
    }
}
