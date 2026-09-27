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
        var lines = TextLine.Split(text);

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;
            if (lines[i].Length == 0)
                continue;

            var op = ParseLine(number, lines[i]);

            var sameName = items.Find(x => x.Op.Name == op.Name);
            if (sameName.Op is not null)
                throw AsmException.AtOpCodes(number, 1, $"мнемоника {op.Name} уже задана в строке {sameName.Line}");

            var sameCode = items.Find(x => x.Op.Code == op.Code);
            if (sameCode.Op is not null)
                throw AsmException.AtOpCodes(number, Layout.CodePosition, $"код {op.Code:X2} уже занят командой {sameCode.Op.Name} (строка {sameCode.Line})");

            items.Add((op, number));
        }

        if (items.Count == 0)
            throw new AsmException("ТКО пуста");

        return new OpCodeTable([.. items.Select(x => x.Op)]);
    }

    static OpCode ParseLine(int number, string raw)
    {
        AsmException Fail(int position, string message) => AsmException.AtOpCodes(number, position, message);

        var line = TextLine.Normalize(raw, Fail);
        var name = ReadName(line, Fail);
        var code = ReadCode(line, Fail);
        var length = ReadLength(line, Fail);

        return new OpCode(name, code, length);
    }

    static string ReadName(string line, Func<int, string, AsmException> fail)
    {
        var field = TextLine.Field(line, 0, Rules.MaxMnemonicLength);
        var name = field.TrimEnd();

        if (field[0] == ' ')
            throw fail(TextLine.FirstNonSpace(line, 0) + 1, "мнемоника пишется с позиции 1");
        if (name.Contains(' '))
            throw fail(name.IndexOf(' ') + 1, $"код операции пишется с позиции {Layout.CodePosition}");
        if (line.Length > Rules.MaxMnemonicLength && line[Rules.MaxMnemonicLength] != ' ')
            throw fail(Rules.MaxMnemonicLength + 1, $"мнемоника длиннее {Rules.MaxMnemonicLength} символов");

        if (!Rules.IsName(name))
            throw fail(1, $"мнемоника «{name}» должна начинаться с латинской буквы и состоять из латинских букв и цифр");
        if (Rules.IsDirective(name))
            throw fail(1, $"мнемоника {name} совпадает с директивой");
        if (Rules.IsRegister(name))
            throw fail(1, $"мнемоника {name} совпадает с именем регистра");

        return name;
    }

    static int ReadCode(string line, Func<int, string, AsmException> fail)
    {
        var start = Layout.CodePosition - 1;
        var field = TextLine.Field(line, start, Rules.OpCodeDigits);

        if (field.Length == 0)
            throw fail(line.Length + 1, $"нет кода операции: две 16-ричные цифры в позициях {Layout.CodePosition}-{Layout.CodePosition + 1}");
        if (field[0] == ' ')
            throw fail(TextLine.FirstNonSpace(line, start) + 1, $"код операции пишется с позиции {Layout.CodePosition}");
        if (field.Length < Rules.OpCodeDigits || !Rules.IsHex(field))
            throw fail(Layout.CodePosition, $"код операции «{field.TrimEnd()}» должен состоять из двух 16-ричных цифр");

        var separator = start + Rules.OpCodeDigits;
        if (line.Length > separator && line[separator] != ' ')
            throw fail(separator + 1, "код операции длиннее двух цифр");

        var code = Convert.ToInt32(field, 16);
        if (code > Rules.MaxOpCode)
            throw fail(Layout.CodePosition, $"код {code:X2} больше {Rules.MaxOpCode:X2}");

        return code;
    }

    static int ReadLength(string line, Func<int, string, AsmException> fail)
    {
        var index = Layout.LengthPosition - 1;

        if (line.Length <= index)
            throw fail(line.Length + 1, $"нет длины команды: цифра от 1 до 4 в позиции {Layout.LengthPosition}");
        if (line[index] == ' ')
            throw fail(TextLine.FirstNonSpace(line, index) + 1, $"длина команды пишется в позиции {Layout.LengthPosition}");
        if (line.Length > index + 1)
            throw fail(index + 2, "после длины команды ничего не должно быть");
        if (line[index] is < '1' or > '4')
            throw fail(Layout.LengthPosition, $"длина команды «{line[index]}» должна быть от 1 до 4");

        return line[index] - '0';
    }
}
