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

    public static OpCodeTable Parse(string text)
    {
        var items = new List<(OpCode Op, int Line)>();
        var lines = Tokenizer.Lines(text);

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;
            if (!Tokenizer.TrySplit(lines[i], out var tokens))
                throw AsmException.AtOpCodes(number, "не закрыта кавычка");
            if (tokens.Count == 0)
                continue;

            var op = ParseLine(number, tokens);

            var sameName = items.Find(x => x.Op.Name == op.Name);
            if (sameName.Op is not null)
                throw AsmException.AtOpCodes(number, $"мнемоника {op.Name} уже задана в строке {sameName.Line}");

            var sameCode = items.Find(x => x.Op.Code == op.Code);
            if (sameCode.Op is not null)
                throw AsmException.AtOpCodes(number, $"код {op.Code:X2} уже занят командой {sameCode.Op.Name} (строка {sameCode.Line})");

            items.Add((op, number));
        }

        if (items.Count == 0)
            throw new AsmException("ТКО пуста");

        return new OpCodeTable([.. items.Select(x => x.Op)]);
    }

    static OpCode ParseLine(int number, List<string> tokens)
    {
        if (tokens.Count != 3)
            throw AsmException.AtOpCodes(number, $"ожидается 3 поля «мнемоника код длина», найдено {tokens.Count}");

        var (name, code, length) = (tokens[0], tokens[1], tokens[2]);

        if (!Rules.IsName(name))
            throw AsmException.AtOpCodes(number, $"мнемоника «{name}» должна начинаться с латинской буквы и состоять из латинских букв и цифр");
        if (name.Length > Rules.MaxMnemonicLength)
            throw AsmException.AtOpCodes(number, $"мнемоника {name} длиннее {Rules.MaxMnemonicLength} символов");
        if (Rules.IsDirective(name))
            throw AsmException.AtOpCodes(number, $"мнемоника {name} совпадает с директивой");
        if (Rules.IsRegister(name))
            throw AsmException.AtOpCodes(number, $"мнемоника {name} совпадает с именем регистра");

        if (!Rules.IsHex(code) || code.Length > 2)
            throw AsmException.AtOpCodes(number, $"код «{code}» должен быть 16-ричным числом из 1-2 цифр");
        var value = Convert.ToInt32(code, 16);
        if (value > Rules.MaxOpCode)
            throw AsmException.AtOpCodes(number, $"код {value:X2} больше {Rules.MaxOpCode:X2}");

        if (length is not ("1" or "2" or "3" or "4"))
            throw AsmException.AtOpCodes(number, $"длина «{length}» должна быть числом от 1 до 4");

        return new OpCode(name, value, int.Parse(length));
    }
}
