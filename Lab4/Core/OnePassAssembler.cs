namespace Core;

/// <summary>Ссылка вперёд: команда по адресу Address ждёт метку; её запись T лежит в объектном модуле под индексом Record.</summary>
public sealed record PendingReference(int Address, int Record, int Line, int Position, OpCode OpCode);

/// <summary>Строка ТСИ. Address = null, пока метка не определена; Pending - команды, которые её ждут.</summary>
public sealed class Symbol(string name)
{
    public string Name { get; } = name;
    public int? Address { get; set; }
    public int Line { get; set; }
    public List<PendingReference> Pending { get; } = [];
}

/// <summary>
/// Однопросмотровый ассемблер для программы в абсолютном формате.
/// Каждая строка исходника обрабатывается один раз: код сразу пишется в объектный модуль.
/// Если метка ещё не определена (ссылка вперёд), в адресную часть пишется FF..F,
/// а адрес команды запоминается в ТСИ. Когда метка определяется, все ждущие записи T исправляются.
/// Step() обрабатывает ровно одну строку - это и есть трассировка.
/// </summary>
public sealed class OnePassAssembler
{
    public const char Unresolved = 'F';

    readonly string[] lines;
    readonly OpCodeTable opCodes;
    readonly OrderedDictionary<string, Symbol> symbols = new();
    readonly List<string> records = [];
    int next;
    int start;
    int address;
    string name = "";
    bool started;

    public OnePassAssembler(string sourceText, string opCodesText)
    {
        lines = TextLine.Split(sourceText);
        var errors = new List<AsmException>();
        opCodes = OpCodeTable.Parse(opCodesText, errors);
        if (errors.Count > 0)
        {
            Error = Printer.Lines(AsmException.Sorted(errors));
            NextLine = null;
        }
        else
            NextLine = FindNext();
    }

    public IReadOnlyList<string> Records => records;
    public IEnumerable<Symbol> Symbols => symbols.Values;

    /// <summary>Номер следующей строки (с 1), которую обработает Step; null - строк больше нет.</summary>
    public int? NextLine { get; private set; }

    /// <summary>Номер последней обработанной строки.</summary>
    public int? LastLine { get; private set; }

    /// <summary>Что было сделано на последнем шаге - для трассировки.</summary>
    public string LastAction { get; private set; } = "";

    public string? Error { get; private set; }
    public bool Finished { get; private set; }
    public bool Stopped => Finished || Error is not null;

    /// <summary>Обработать ровно одну строку исходного текста.</summary>
    public void Step()
    {
        if (Stopped)
            return;

        if (NextLine is not { } number)
        {
            Error = started ? "Не найдена директива END" : "Исходный текст пуст";
            return;
        }

        LastLine = number;
        try
        {
            var line = SourceParser.Parse(number, lines[number - 1], opCodes)!;
            LastAction = Process(line);
            next = number;
            NextLine = Finished ? null : FindNext();
            if (!Finished && NextLine is null)
                Error = "Не найдена директива END";
        }
        catch (AsmException e)
        {
            Error = e.Message;
        }
    }

    /// <summary>Полный проход: шаги до END или до первой ошибки.</summary>
    public void Run()
    {
        while (!Stopped)
            Step();
    }

    int? FindNext()
    {
        for (var i = next; i < lines.Length; i++)
            if (TextLine.WithoutComment(lines[i]).Length > 0)
                return i + 1;
        return null;
    }

    static AsmException Fail(SourceLine line, int operand, string message) =>
        AsmException.AtSource(line.Number, line.Positions[operand], message);

    string Process(SourceLine line)
    {
        if (!started)
        {
            if (line.Command != "START")
                throw AsmException.AtSource(line.Number, "программа должна начинаться с директивы START");
            return ProcessStart(line);
        }

        if (line.Command == "START")
            throw AsmException.AtSource(line.Number, "директива START допускается только в первой строке");
        if (line.Command == "END")
            return ProcessEnd(line);

        var defined = line.Label is null ? "" : DefineSymbol(line.Label, line.Number);
        var action = opCodes.Find(line.Command) is { } op ? EncodeCommand(line, op) : EncodeDirective(line);
        return defined.Length == 0 ? action : $"{defined}; {action}";
    }

    string ProcessStart(SourceLine line)
    {
        if (line.Label is null)
            throw AsmException.AtSource(line.Number, "не задано имя программы перед START");
        if (line.Operands.Length == 0)
            throw AsmException.AtSource(line.Number, "у START не задан адрес загрузки");
        if (line.Operands.Length > 1)
            throw Fail(line, 1, "у START только один операнд - адрес загрузки");
        if (!Rules.TryAddress(line.Operands[0], out start))
            throw Fail(line, 0, $"адрес загрузки «{line.Operands[0]}» должен быть 16-ричным числом не длиннее {Rules.AddressInputDigits} цифр, не больше {Rules.Address(Rules.MaxAddress)}");
        if (start == 0)
            throw Fail(line, 0, "адрес загрузки не может быть равен 0");

        name = line.Label;
        address = start;
        started = true;
        // Длина программы станет известна только на END
        records.Add($"H {name} {new string('-', Rules.AddressDigits)} {Rules.Address(start)}");
        return $"START: программа {name}, адрес загрузки {Rules.Address(start)}";
    }

    string ProcessEnd(SourceLine line)
    {
        if (line.Label is not null)
            throw AsmException.AtSource(line.Number, 1, "у директивы END не может быть метки");
        if (line.Operands.Length > 1)
            throw Fail(line, 1, "у END допускается не более одного операнда - адрес точки входа");

        var entry = start;
        if (line.Operands.Length > 0 && !Rules.TryAddress(line.Operands[0], out entry))
            throw Fail(line, 0, $"адрес точки входа «{line.Operands[0]}» должен быть 16-ричным числом не длиннее {Rules.AddressInputDigits} цифр");

        var undefined = symbols.Values.Where(s => s.Address is null).ToList();
        if (undefined.Count > 0)
            throw AsmException.AtSource(line.Number, "к директиве END остались неопределёнными метки: " +
                string.Join("; ", undefined.Select(s => $"{s.Name} (строки {string.Join(", ", s.Pending.Select(p => p.Line))})")));

        // Как в эталоне: точка входа должна лежать в пределах программы (пустая программа - только адрес загрузки)
        var inside = entry >= start && entry < address || entry == start && address == start;
        if (!inside)
            throw Fail(line, 0, $"точка входа {Rules.Address(entry)} должна лежать в пределах программы ({(address > start ? $"{Rules.Address(start)}-{Rules.Address(address - 1)}" : Rules.Address(start))})");

        for (var i = line.Number; i < lines.Length; i++)
            if (TextLine.WithoutComment(lines[i]).Length > 0)
                throw AsmException.AtSource(i + 1, "после директивы END не должно быть строк");

        records[0] = $"H {name} {Rules.Address(address - start)} {Rules.Address(start)}";
        records.Add($"E {Rules.Address(entry)}");
        Finished = true;
        return $"END: длина программы {Rules.Address(address - start)}, точка входа {Rules.Address(entry)}";
    }

    string DefineSymbol(string label, int number)
    {
        if (label == name)
            throw AsmException.AtSource(number, 1, $"метка {label} совпадает с именем программы");

        if (symbols.TryGetValue(label, out var symbol) && symbol.Address is not null)
            throw AsmException.AtSource(number, 1, $"метка {label} уже объявлена в строке {symbol.Line}");

        if (symbol is null)
        {
            symbol = new Symbol(label);
            symbols.Add(label, symbol);
        }

        symbol.Address = address;
        symbol.Line = number;
        if (symbol.Pending.Count == 0)
            return $"метка {label} = {Rules.Address(address)}";

        // Метка определилась: исправляем все записи T, которые её ждали
        foreach (var p in symbol.Pending)
        {
            CheckFits(address, p.OpCode, label, p.Line, p.Position);
            var record = records[p.Record];
            records[p.Record] = record[..^p.OpCode.AddressDigits] + address.ToString($"X{p.OpCode.AddressDigits}");
        }

        var fixedAt = string.Join(", ", symbol.Pending.Select(p => Rules.Address(p.Address)));
        symbol.Pending.Clear();
        return $"метка {label} = {Rules.Address(address)}, исправлены ссылки вперёд в командах {fixedAt}";
    }

    static void CheckFits(int target, OpCode op, string label, int line, int position)
    {
        if (target > (1 << (op.AddressDigits * 4)) - 1)
            throw AsmException.AtSource(line, position,
                $"адрес метки {label} ({Rules.Address(target)}) не помещается в {op.AddressDigits / 2} байта команды {op.Name}");
    }

    string EncodeCommand(SourceLine line, OpCode op)
    {
        var operands = line.Operands;
        var code = op.MachineCode.ToString("X2");

        switch (op.Length)
        {
            case 1:
                if (operands.Length > 0)
                    throw Fail(line, 0, $"у команды {op.Name} не должно быть операндов");
                return Emit(line, op.Length, code);

            case 2:
                if (operands.Length == 0)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} нет операндов: нужно число 0..{Rules.MaxByte} или два регистра");
                if (operands.Length == 1)
                    return Rules.TryNumber(operands[0], Rules.MaxByte, out var value)
                        ? Emit(line, op.Length, code + value.ToString("X2"))
                        : throw Fail(line, 0, $"операнд «{operands[0]}» команды {op.Name} должен быть числом 0..{Rules.MaxByte} (или укажите два регистра)");
                for (var i = 0; i < 2; i++)
                    if (!Rules.IsRegister(operands[i]))
                        throw Fail(line, i, $"операнд «{operands[i]}» команды {op.Name} должен быть регистром R0..R15");
                return Emit(line, op.Length, code + $"{Rules.RegisterNumber(operands[0]):X}{Rules.RegisterNumber(operands[1]):X}");

            default:
                if (operands.Length != 1)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} должен быть ровно один операнд - метка");
                return EncodeLabelOperand(line, op, code);
        }
    }

    string EncodeLabelOperand(SourceLine line, OpCode op, string code)
    {
        var label = line.Operands[0];
        var position = line.Positions[0];
        if (NotLabel(label, op) is { } problem)
            throw AsmException.AtSource(line.Number, position, problem);

        if (symbols.TryGetValue(label, out var symbol) && symbol.Address is { } known)
        {
            CheckFits(known, op, label, line.Number, position);
            return Emit(line, op.Length, code + known.ToString($"X{op.AddressDigits}")) + $", метка {label} уже известна";
        }

        if (symbol is null)
        {
            symbol = new Symbol(label);
            symbols.Add(label, symbol);
        }

        var commandAddress = address;
        var action = Emit(line, op.Length, code + new string(Unresolved, op.AddressDigits));
        symbol.Pending.Add(new PendingReference(commandAddress, records.Count - 1, line.Number, position, op));
        return action + $", ссылка вперёд на {label}: адрес записан в ТСИ";
    }

    string? NotLabel(string operand, OpCode op)
    {
        if (Rules.IsRegister(operand))
            return $"операнд {operand} команды {op.Name} - регистр, а нужна метка";
        if (Rules.IsDirective(operand))
            return $"операнд {operand} команды {op.Name} - директива, а нужна метка";
        if (opCodes.Contains(operand))
            return $"операнд {operand} команды {op.Name} - команда, а нужна метка";
        if (operand == name)
            return $"операнд {operand} команды {op.Name} - имя программы, а нужна метка";
        if (operand.StartsWith('[') && operand.EndsWith(']'))
            return $"относительная адресация {operand} не поддерживается в абсолютном формате, нужна метка";
        if (!Rules.IsName(operand))
            return $"операнд «{operand}» команды {op.Name} должен быть меткой";
        return null;
    }

    string EncodeDirective(SourceLine line)
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

        return Emit(line, size, code);
    }

    string Emit(SourceLine line, int size, string code)
    {
        if (address + size > Rules.MaxAddress + 1)
            throw AsmException.AtSource(line.Number, $"программа выходит за границу памяти {Rules.Address(Rules.MaxAddress)}");

        var at = Rules.Address(address);
        records.Add(code.Length == 0 ? $"T {at} {size:X2}" : $"T {at} {size:X2} {code}");
        address += size;
        return $"{line.Command}: записано {size} байт по адресу {at}";
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
