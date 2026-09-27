namespace Core;

public static class FirstPass
{
    public static FirstPassResult Run(string sourceText, string opCodesText)
    {
        var opCodes = OpCodeTable.Parse(opCodesText);
        var lines = SourceParser.Parse(sourceText, opCodes);
        if (lines.Count == 0)
            throw new AsmException("Исходный текст пуст");

        var (name, start) = ReadStart(lines[0]);
        var rows = new List<SupportRow>();
        var symbols = new List<Symbol>();
        var address = start;

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.Command == "END")
            {
                if (i < lines.Count - 1)
                    throw AsmException.AtSource(lines[i + 1].Number, "после директивы END не должно быть строк");
                return new FirstPassResult(name, start, address - start, ReadEnd(line, start, rows), rows, symbols, opCodes);
            }

            if (line.Label is not null)
                AddSymbol(symbols, name, line.Label, address, line.Number);

            var row = Measure(line, opCodes, address);
            if (address + row.Size > Rules.MaxAddress + 1)
                throw AsmException.AtSource(line.Number, $"программа выходит за границу памяти {Rules.MaxAddress:X6}");

            rows.Add(row);
            address += row.Size;
        }

        throw new AsmException("Не найдена директива END");
    }

    static (string Name, int Start) ReadStart(SourceLine line)
    {
        if (line.Command != "START")
            throw AsmException.AtSource(line.Number, "программа должна начинаться с директивы START");
        if (line.Label is null)
            throw AsmException.AtSource(line.Number, "не задано имя программы перед START");
        if (line.Operands.Length != 1)
            throw AsmException.AtSource(line.Number, "у START должен быть ровно один операнд - адрес загрузки");

        var operand = line.Operands[0];
        if (!Rules.IsAddress(operand))
            throw AsmException.AtSource(line.Number, $"адрес загрузки «{operand}» должен быть 16-ричным числом не длиннее {Rules.MaxAddressDigits} цифр");

        var start = Convert.ToInt32(operand, 16);
        if (start == 0)
            throw AsmException.AtSource(line.Number, "адрес загрузки не может быть равен 0");

        return (line.Label, start);
    }

    static int ReadEnd(SourceLine line, int start, List<SupportRow> rows)
    {
        if (line.Label is not null)
            throw AsmException.AtSource(line.Number, "у директивы END не может быть метки");
        if (line.Operands.Length > 1)
            throw AsmException.AtSource(line.Number, "у END допускается не более одного операнда - адрес точки входа");

        var entry = start;
        if (line.Operands.Length == 1)
        {
            var operand = line.Operands[0];
            if (!Rules.IsAddress(operand))
                throw AsmException.AtSource(line.Number, $"адрес точки входа «{operand}» должен быть 16-ричным числом не длиннее {Rules.MaxAddressDigits} цифр");
            entry = Convert.ToInt32(operand, 16);
        }

        if (!rows.Any(r => r.OpCode is not null && r.Address == entry))
            throw AsmException.AtSource(line.Number, $"точка входа {entry:X6} не совпадает с адресом ни одной команды");

        return entry;
    }

    static void AddSymbol(List<Symbol> symbols, string programName, string label, int address, int line)
    {
        if (label == programName)
            throw AsmException.AtSource(line, $"метка {label} совпадает с именем программы");
        if (symbols.Find(s => s.Name == label) is { } existing)
            throw AsmException.AtSource(line, $"метка {label} уже объявлена в строке {existing.Line}");

        symbols.Add(new Symbol(label, address, line));
    }

    static SupportRow Measure(SourceLine line, OpCodeTable opCodes, int address)
    {
        if (opCodes.Find(line.Command) is { } op)
        {
            CheckCommand(line, op);
            return new SupportRow(line.Number, address, op.Length, op.MachineCode.ToString("X2"), op, line.Operands);
        }

        return new SupportRow(line.Number, address, DirectiveSize(line), line.Command, null, line.Operands);
    }

    static void CheckCommand(SourceLine line, OpCode op)
    {
        var operands = line.Operands;

        switch (op.Length)
        {
            case 1:
                if (operands.Length > 0)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} не должно быть операндов");
                break;

            case 2:
                if (operands.Length == 0)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} нет операндов: нужно число 0..255 или два регистра");
                if (operands.Length == 1 && !IsByte(operands[0]))
                    throw AsmException.AtSource(line.Number, $"операнд «{operands[0]}» команды {op.Name} должен быть числом 0..255 (или укажите два регистра)");
                if (operands.Length == 2 && operands.FirstOrDefault(o => !Rules.IsRegister(o)) is { } wrong)
                    throw AsmException.AtSource(line.Number, $"операнд «{wrong}» команды {op.Name} должен быть регистром R0..R15");
                break;

            default:
                if (operands.Length != 1)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} должен быть ровно один операнд - метка");
                break;
        }
    }

    static bool IsByte(string operand) => Rules.IsDecimal(operand) && int.TryParse(operand, out var value) && value <= 255;

    static int DirectiveSize(SourceLine line)
    {
        if (line.Command == "START")
            throw AsmException.AtSource(line.Number, "директива START допускается только в первой строке");
        if (line.Operands.Length != 1)
            throw AsmException.AtSource(line.Number, $"у директивы {line.Command} должен быть ровно один операнд");

        var operand = line.Operands[0];
        return line.Command switch
        {
            "WORD" => WordSize(line.Number, operand),
            "BYTE" => ByteOperand.Size(line.Number, operand),
            "RESB" => Reserve(line, operand, 1),
            _ => Reserve(line, operand, Rules.WordSize),
        };
    }

    static int WordSize(int line, string operand)
    {
        if (operand == "?" || Rules.IsDecimal(operand) && int.TryParse(operand, out var value) && value <= Rules.MaxAddress)
            return Rules.WordSize;

        throw AsmException.AtSource(line, $"операнд WORD «{operand}» должен быть числом от 0 до {Rules.MaxAddress} или ?");
    }

    static int Reserve(SourceLine line, string operand, int unit)
    {
        if (Rules.IsDecimal(operand) && int.TryParse(operand, out var count) && count > 0 && count <= Rules.MaxAddress)
            return count * unit;

        throw AsmException.AtSource(line.Number, $"операнд {line.Command} «{operand}» должен быть целым числом от 1 до {Rules.MaxAddress}");
    }
}
