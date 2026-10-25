namespace Core;

public static class FirstPass
{
    public static FirstPassResult Run(string sourceText, string opCodesText)
    {
        var errors = new List<AsmException>();
        var opCodes = OpCodeTable.Parse(opCodesText, errors);
        if (errors.Count > 0)
            return new FirstPassResult("", 0, 0, 0, [], [], opCodes, AsmException.Sorted(errors));

        var rows = new List<SupportRow>();
        var symbols = new OrderedDictionary<string, Symbol>();
        var name = "";
        var start = 0;
        var address = 0;
        var started = false;
        var overflow = false;
        SourceLine? end = null;
        var lines = TextLine.Split(sourceText);

        for (var i = 0; i < lines.Length; i++)
        {
            SourceLine? line;
            try
            {
                line = SourceParser.Parse(i + 1, lines[i], opCodes);
            }
            catch (AsmException e)
            {
                errors.Add(e);
                started = true;
                continue;
            }

            if (line is null)
                continue;

            if (end is not null)
            {
                errors.Add(AsmException.AtSource(line.Number, "после директивы END не должно быть строк"));
                continue;
            }

            if (!started)
            {
                started = true;
                if (line.Command == "START")
                {
                    (name, start) = ReadStart(line, errors);
                    address = start;
                    continue;
                }

                errors.Add(AsmException.AtSource(line.Number, "программа должна начинаться с директивы START"));
            }
            else if (line.Command == "START")
            {
                errors.Add(AsmException.AtSource(line.Number, "директива START допускается только в первой строке"));
                continue;
            }

            if (line.Command == "END")
            {
                end = line;
                continue;
            }

            if (line.Label is not null)
                Try(errors, () => AddSymbol(symbols, name, line.Label, address, line.Number));

            SupportRow? row = null;
            Try(errors, () => row = Encode(line, opCodes, address));
            var size = row?.Size ?? KnownSize(line, opCodes);

            if (address + size > Rules.MaxAddress + 1)
            {
                errors.Add(AsmException.AtSource(line.Number, $"программа выходит за границу памяти {Rules.Address(Rules.MaxAddress)}"));
                overflow = true;
                break;
            }

            if (row is not null)
                rows.Add(row);
            address += size;
        }

        if (!started)
            errors.Add(new AsmException("Исходный текст пуст"));
        else if (end is null && !overflow)
            errors.Add(new AsmException("Не найдена директива END"));

        var entry = end is null ? start : ReadEnd(end, start, rows, errors);
        return new FirstPassResult(name, start, address - start, entry, rows, [.. symbols.Values], opCodes, AsmException.Sorted(errors));
    }

    static void Try(List<AsmException> errors, Action action)
    {
        try
        {
            action();
        }
        catch (AsmException e)
        {
            errors.Add(e);
        }
    }

    static AsmException Fail(SourceLine line, int operand, string message) =>
        AsmException.AtSource(line.Number, line.Positions[operand], message);

    static (string Name, int Start) ReadStart(SourceLine line, List<AsmException> errors)
    {
        var start = 0;

        if (line.Label is null)
            errors.Add(AsmException.AtSource(line.Number, "не задано имя программы перед START"));

        if (line.Operands.Length == 0)
            errors.Add(AsmException.AtSource(line.Number, "у START не задан адрес загрузки"));
        else if (!Rules.TryAddress(line.Operands[0], out start))
            errors.Add(Fail(line, 0, $"адрес загрузки «{line.Operands[0]}» должен быть 16-ричным числом не длиннее {Rules.AddressInputDigits} цифр, не больше {Rules.Address(Rules.MaxAddress)}"));
        else if (start == 0)
            errors.Add(Fail(line, 0, "адрес загрузки не может быть равен 0"));

        if (line.Operands.Length > 1)
            errors.Add(Fail(line, 1, "у START только один операнд - адрес загрузки"));

        return (line.Label ?? "", start);
    }

    static int ReadEnd(SourceLine line, int start, List<SupportRow> rows, List<AsmException> errors)
    {
        if (line.Label is not null)
            errors.Add(AsmException.AtSource(line.Number, 1, "у директивы END не может быть метки"));
        if (line.Operands.Length > 1)
            errors.Add(Fail(line, 1, "у END допускается не более одного операнда - адрес точки входа"));

        var entry = start;
        if (line.Operands.Length > 0 && !Rules.TryAddress(line.Operands[0], out entry))
        {
            errors.Add(Fail(line, 0, $"адрес точки входа «{line.Operands[0]}» должен быть 16-ричным числом не длиннее {Rules.AddressInputDigits} цифр"));
            return start;
        }

        // Пустая программа (START и сразу END) корректна, если точка входа - адрес загрузки
        var emptyProgramEntryIsValid = rows.Count == 0 && entry == start;
        if (errors.Count == 0 && !emptyProgramEntryIsValid && !rows.Any(r => r.OpCode is not null && r.Address == entry))
            errors.Add(AsmException.AtSource(line.Number, $"точка входа {Rules.Address(entry)} не совпадает с адресом ни одной команды"));

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

    static int KnownSize(SourceLine line, OpCodeTable opCodes) =>
        opCodes.Find(line.Command)?.Length ?? (line.Command == "WORD" ? Rules.WordSize : 0);

    static SupportRow Encode(SourceLine line, OpCodeTable opCodes, int address) =>
        opCodes.Find(line.Command) is { } op ? EncodeCommand(line, op, address) : EncodeDirective(line, address);

    static SupportRow EncodeCommand(SourceLine line, OpCode op, int address)
    {
        var operands = line.Operands;
        var code = op.MachineCode.ToString("X2");
        SupportRow Row(string rest, string? label = null) => new(line, address, op.Length, op, code + rest, label);

        switch (op.Length)
        {
            case 1:
                if (operands.Length > 0)
                    throw Fail(line, 0, $"у команды {op.Name} не должно быть операндов");
                return Row("");

            case 2:
                if (operands.Length == 0)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} нет операндов: нужно число 0..{Rules.MaxByte} или два регистра");
                if (operands.Length == 1)
                    return Rules.TryNumber(operands[0], Rules.MaxByte, out var value)
                        ? Row(value.ToString("X2"))
                        : throw Fail(line, 0, $"операнд «{operands[0]}» команды {op.Name} должен быть числом 0..{Rules.MaxByte} (или укажите два регистра)");
                for (var i = 0; i < 2; i++)
                    if (!Rules.IsRegister(operands[i]))
                        throw Fail(line, i, $"операнд «{operands[i]}» команды {op.Name} должен быть регистром R0..R15");
                return Row($"{Rules.RegisterNumber(operands[0]):X}{Rules.RegisterNumber(operands[1]):X}");

            default:
                if (operands.Length != 1)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} должен быть ровно один операнд - метка");
                return Row("", operands[0]);
        }
    }

    static SupportRow EncodeDirective(SourceLine line, int address)
    {
        if (line.Operands.Length != 1)
            throw AsmException.AtSource(line.Number, $"у директивы {line.Command} должен быть ровно один операнд");

        var operand = line.Operands[0];
        var (size, code) = line.Command switch
        {
            "WORD" => Word(line, operand),
            "BYTE" => ByteOperand.Encode(operand, message => Fail(line, 0, message)),
            "RESB" => (Reserve(line, operand, 1), ""),
            _ => (Reserve(line, operand, Rules.WordSize), ""),
        };

        return new SupportRow(line, address, size, null, code, null);
    }

    static (int Size, string Code) Word(SourceLine line, string operand)
    {
        if (operand == "?")
            return (Rules.WordSize, "");
        if (Rules.TryNumber(operand, Rules.MaxAddress, out var value))
            return (Rules.WordSize, value.ToString("X6"));

        throw Fail(line, 0, $"операнд WORD «{operand}» должен быть числом от 0 до {Rules.MaxAddress} (не более {Rules.MaxAddress.ToString().Length} цифр) или ?");
    }

    static int Reserve(SourceLine line, string operand, int unit)
    {
        if (Rules.TryNumber(operand, Rules.MaxAddress, out var count) && count > 0)
            return count * unit;

        throw Fail(line, 0, $"операнд {line.Command} «{operand}» должен быть числом от 1 до {Rules.MaxAddress} (не более {Rules.MaxAddress.ToString().Length} цифр)");
    }
}
