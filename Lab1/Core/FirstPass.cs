namespace Core;

public static class FirstPass
{
    public static FirstPassResult Run(string sourceText, string opCodesText)
    {
        var opCodes = OpCodeTable.Parse(opCodesText);
        var rows = new List<SupportRow>();
        var symbols = new OrderedDictionary<string, Symbol>();
        string? name = null;
        var start = 0;
        var address = 0;
        SourceLine? end = null;

        foreach (var line in SourceParser.Parse(sourceText, opCodes))
        {
            if (end is not null)
                throw AsmException.AtSource(line.Number, "после директивы END не должно быть строк");

            if (name is null)
            {
                (name, start) = ReadStart(line);
                address = start;
                continue;
            }

            if (line.Command == "END")
            {
                end = line;
                continue;
            }

            if (line.Label is not null)
                AddSymbol(symbols, name, line.Label, address, line.Number);

            var row = Measure(line, opCodes, address);
            if (address + row.Size > Rules.MaxAddress + 1)
                throw AsmException.AtSource(line.Number, $"программа выходит за границу памяти {Rules.MaxAddress:X6}");

            rows.Add(row);
            address += row.Size;
        }

        if (name is null)
            throw new AsmException("Исходный текст пуст");
        if (end is null)
            throw new AsmException("Не найдена директива END");

        return new FirstPassResult(name, start, address - start, ReadEnd(end, start, rows), rows, [.. symbols.Values], opCodes);
    }

    static AsmException Fail(SourceLine line, int operand, string message) =>
        AsmException.AtSource(line.Number, line.Positions[operand], message);

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
            throw Fail(line, 0, $"адрес загрузки «{operand}» должен быть 16-ричным числом не длиннее {Rules.MaxAddressDigits} цифр");

        var start = Convert.ToInt32(operand, 16);
        if (start == 0)
            throw Fail(line, 0, "адрес загрузки не может быть равен 0");

        return (line.Label, start);
    }

    static int ReadEnd(SourceLine line, int start, List<SupportRow> rows)
    {
        if (line.Label is not null)
            throw AsmException.AtSource(line.Number, 1, "у директивы END не может быть метки");
        if (line.Operands.Length > 1)
            throw Fail(line, 1, "у END допускается не более одного операнда - адрес точки входа");

        var entry = start;
        if (line.Operands.Length == 1)
        {
            var operand = line.Operands[0];
            if (!Rules.IsAddress(operand))
                throw Fail(line, 0, $"адрес точки входа «{operand}» должен быть 16-ричным числом не длиннее {Rules.MaxAddressDigits} цифр");
            entry = Convert.ToInt32(operand, 16);
        }

        if (!rows.Any(r => r.OpCode is not null && r.Address == entry))
            throw AsmException.AtSource(line.Number, $"точка входа {entry:X6} не совпадает с адресом ни одной команды");

        return entry;
    }

    static void AddSymbol(OrderedDictionary<string, Symbol> symbols, string programName, string label, int address, int line)
    {
        if (label == programName)
            throw AsmException.AtSource(line, 1, $"метка {label} совпадает с именем программы");
        if (symbols.TryGetValue(label, out var existing))
            throw AsmException.AtSource(line, 1, $"метка {label} уже объявлена в строке {existing.Line}");

        symbols.Add(label, new Symbol(label, address, line));
    }

    static SupportRow Measure(SourceLine line, OpCodeTable opCodes, int address)
    {
        if (opCodes.Find(line.Command) is { } op)
        {
            CheckCommand(line, op);
            return new SupportRow(line, address, op.Length, op.MachineCode.ToString("X2"), op);
        }

        return new SupportRow(line, address, DirectiveSize(line), line.Command, null);
    }

    static void CheckCommand(SourceLine line, OpCode op)
    {
        var operands = line.Operands;

        switch (op.Length)
        {
            case 1:
                if (operands.Length > 0)
                    throw Fail(line, 0, $"у команды {op.Name} не должно быть операндов");
                break;

            case 2:
                if (operands.Length == 0)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} нет операндов: нужно число 0..{Rules.MaxByte} или два регистра");
                if (operands.Length == 1 && !Rules.TryNumber(operands[0], Rules.MaxByte, out _))
                    throw Fail(line, 0, $"операнд «{operands[0]}» команды {op.Name} должен быть числом 0..{Rules.MaxByte} (или укажите два регистра)");
                for (var i = 0; operands.Length == 2 && i < 2; i++)
                    if (!Rules.IsRegister(operands[i]))
                        throw Fail(line, i, $"операнд «{operands[i]}» команды {op.Name} должен быть регистром R0..R15");
                break;

            default:
                if (operands.Length != 1)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} должен быть ровно один операнд - метка");
                break;
        }
    }

    static int DirectiveSize(SourceLine line)
    {
        if (line.Command == "START")
            throw AsmException.AtSource(line.Number, "директива START допускается только в первой строке");
        if (line.Operands.Length != 1)
            throw AsmException.AtSource(line.Number, $"у директивы {line.Command} должен быть ровно один операнд");

        var operand = line.Operands[0];
        return line.Command switch
        {
            "WORD" => WordSize(line, operand),
            "BYTE" => ByteOperand.Size(operand, message => Fail(line, 0, message)),
            "RESB" => Reserve(line, operand, 1),
            _ => Reserve(line, operand, Rules.WordSize),
        };
    }

    static int WordSize(SourceLine line, string operand)
    {
        if (operand == "?" || Rules.TryNumber(operand, Rules.MaxAddress, out _))
            return Rules.WordSize;

        throw Fail(line, 0, $"операнд WORD «{operand}» должен быть числом от 0 до {Rules.MaxAddress} (не более {Rules.MaxAddress.ToString().Length} цифр) или ?");
    }

    static int Reserve(SourceLine line, string operand, int unit)
    {
        if (Rules.TryNumber(operand, Rules.MaxAddress, out var count) && count > 0)
            return count * unit;

        throw Fail(line, 0, $"операнд {line.Command} «{operand}» должен быть числом от 1 до {Rules.MaxAddress} (не более {Rules.MaxAddress.ToString().Length} цифр)");
    }
}
