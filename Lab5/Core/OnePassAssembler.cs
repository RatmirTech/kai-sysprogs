namespace Core;

/// <summary>Ссылка вперёд: команда по адресу Address ждёт метку; её запись T лежит в объектном модуле под индексом Record.
/// Relative - вид адресации команды: true - относительная (исправлять смещением), false - прямая (абсолютным адресом + запись M).</summary>
public sealed record PendingReference(int Address, int Record, int Line, int Position, OpCode OpCode, bool Relative);

/// <summary>Строка ТСИ. Address = null, пока метка не определена; Pending - команды, которые её ждут.</summary>
public sealed class Symbol(string name)
{
    public string Name { get; } = name;
    public int? Address { get; set; }
    public int Line { get; set; }
    public List<PendingReference> Pending { get; } = [];
}

/// <summary>
/// Однопросмотровый ассемблер для программы в перемещаемом формате (как в двухпросмотровом варианте,
/// но каждая строка исходника обрабатывается один раз: код сразу пишется в объектный модуль).
/// Программа ассемблируется с адреса 0. Поддерживаются прямая и относительная адресация ([метка]).
/// Если метка ещё не определена (ссылка вперёд), в адресную часть пишется FF..F,
/// а адрес команды запоминается в ТСИ. Когда метка определяется, ждущие записи T исправляются:
/// для прямой адресации - абсолютным адресом (команда заносится в таблицу настройки, запись M),
/// для относительной - смещением в дополнительном коде (без настройки).
/// Step() обрабатывает ровно одну строку - это и есть трассировка.
/// </summary>
public sealed class OnePassAssembler
{
    public const char Unresolved = 'F';

    readonly string[] lines;
    readonly OpCodeTable opCodes;
    readonly AddressingMode mode;
    readonly OrderedDictionary<string, Symbol> symbols = new();
    readonly List<string> records = [];
    readonly List<int> relocations = [];
    int next;
    int address;
    string name = "";
    bool started;

    public OnePassAssembler(string sourceText, string opCodesText, AddressingMode mode)
    {
        this.mode = mode;
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
        if (line.Operands.Length > 1)
            throw Fail(line, 1, "у START допускается не более одного операнда");
        if (line.Operands.Length > 0 && (!Rules.TryAddress(line.Operands[0], out var start) || start != 0))
            throw Fail(line, 0, "программа в перемещаемом формате ассемблируется с адреса 0: операнд START должен отсутствовать или быть равен 0");

        name = line.Label;
        address = 0;
        started = true;
        // Длина программы станет известна только на END
        records.Add($"H {name} {Rules.Address(0)} {new string('-', Rules.AddressDigits)}");
        return $"START: программа {name}, перемещаемый формат, адрес загрузки {Rules.Address(0)}";
    }

    string ProcessEnd(SourceLine line)
    {
        if (line.Label is not null)
            throw AsmException.AtSource(line.Number, 1, "у директивы END не может быть метки");
        if (line.Operands.Length > 1)
            throw Fail(line, 1, "у END допускается не более одного операнда - адрес точки входа");

        var entry = 0;
        if (line.Operands.Length > 0 && !Rules.TryAddress(line.Operands[0], out entry))
            throw Fail(line, 0, $"адрес точки входа «{line.Operands[0]}» должен быть 16-ричным числом не длиннее {Rules.AddressInputDigits} цифр");

        var undefined = symbols.Values.Where(s => s.Address is null).ToList();
        if (undefined.Count > 0)
            throw AsmException.AtSource(line.Number, string.Join("; ", undefined.Select(s =>
                $"метка {s.Name} не объявлена (использована в строках {string.Join(", ", s.Pending.Select(p => p.Line))})")));

        // Как в эталоне: точка входа должна лежать в пределах программы (пустая программа - только адрес 0)
        var inside = entry < address || entry == 0 && address == 0;
        if (!inside)
            throw Fail(line, 0, $"точка входа {Rules.Address(entry)} должна лежать в пределах программы ({(address > 0 ? $"000000-{Rules.Address(address - 1)}" : Rules.Address(0))})");

        for (var i = line.Number; i < lines.Length; i++)
            if (TextLine.WithoutComment(lines[i]).Length > 0)
                throw AsmException.AtSource(i + 1, "после директивы END не должно быть строк");

        records[0] = $"H {name} {Rules.Address(0)} {Rules.Address(address)}";
        // Записи M - как в двухпросмотровом варианте: после всех записей T, перед E
        foreach (var at in relocations.Order())
            records.Add($"M {Rules.Address(at)}");
        records.Add($"E {Rules.Address(entry)}");
        Finished = true;
        return $"END: длина программы {Rules.Address(address)}, точка входа {Rules.Address(entry)}, записей в таблице настройки: {relocations.Count}";
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
        var anyRelative = false;
        var anyDirect = false;
        foreach (var p in symbol.Pending)
        {
            string value;
            if (p.Relative)
            {
                // Относительная адресация: в команду записывается смещение, настройка не нужна
                anyRelative = true;
                var offset = Offset(address, p.Address, p.OpCode, label, p.Line, p.Position);
                value = ToComplement(offset, p.OpCode);
            }
            else
            {
                // Прямая адресация: записывается абсолютный адрес, команда заносится в таблицу настройки
                anyDirect = true;
                CheckFits(address, p.OpCode, label, p.Line, p.Position);
                relocations.Add(p.Address);
                value = address.ToString($"X{p.OpCode.AddressDigits}");
            }

            var record = records[p.Record];
            records[p.Record] = record[..^p.OpCode.AddressDigits] + value;
        }

        var fixedAt = string.Join(", ", symbol.Pending.Select(p => Rules.Address(p.Address)));
        var how = anyRelative && anyDirect
            ? "исправлены ссылки вперёд: прямая адресация - абсолютные адреса, команды занесены в таблицу настройки; относительная - смещения, без настройки"
            : anyRelative
                ? "исправлены ссылки вперёд: относительная адресация - записаны смещения, без настройки"
                : "исправлены ссылки вперёд: команды занесены в таблицу настройки";
        symbol.Pending.Clear();
        return $"метка {label} = {Rules.Address(address)}, {how} в командах {fixedAt}";
    }

    static void CheckFits(int target, OpCode op, string label, int line, int position)
    {
        if (target > (1 << (op.AddressDigits * 4)) - 1)
            throw AsmException.AtSource(line, position,
                $"адрес метки {label} ({Rules.Address(target)}) не помещается в {op.AddressDigits / 2} байта команды {op.Name}");
    }

    /// <summary>Смещение до метки относительно следующей команды (как во 2-м проходе двухпросмотрового варианта).</summary>
    static int Offset(int target, int commandAddress, OpCode op, string label, int line, int position)
    {
        var offset = target - (commandAddress + op.Length);
        var half = 1 << (op.AddressDigits * 4 - 1);
        if (offset < -half || offset >= half)
            throw AsmException.AtSource(line, position,
                $"смещение до метки {label} ({offset}) не помещается в {op.AddressDigits / 2} байта команды {op.Name}");

        return offset;
    }

    static string ToComplement(int offset, OpCode op)
    {
        var half = 1 << (op.AddressDigits * 4 - 1);
        return (offset & (2 * half - 1)).ToString($"X{op.AddressDigits}");
    }

    string EncodeCommand(SourceLine line, OpCode op)
    {
        var operands = line.Operands;

        switch (op.Length)
        {
            case 1:
                if (operands.Length > 0)
                    throw Fail(line, 0, $"у команды {op.Name} не должно быть операндов");
                return Emit(line, op.Length, op.MachineCode(false).ToString("X2"));

            case 2:
                var code2 = op.MachineCode(false).ToString("X2");
                if (operands.Length == 0)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} нет операндов: нужно число 0..{Rules.MaxByte} или два регистра");
                if (operands.Length == 1)
                    return Rules.TryNumber(operands[0], Rules.MaxByte, out var value)
                        ? Emit(line, op.Length, code2 + value.ToString("X2"))
                        : throw Fail(line, 0, $"операнд «{operands[0]}» команды {op.Name} должен быть числом 0..{Rules.MaxByte} (или укажите два регистра)");
                for (var i = 0; i < 2; i++)
                    if (!Rules.IsRegister(operands[i]))
                        throw Fail(line, i, $"операнд «{operands[i]}» команды {op.Name} должен быть регистром R0..R15");
                return Emit(line, op.Length, code2 + $"{Rules.RegisterNumber(operands[0]):X}{Rules.RegisterNumber(operands[1]):X}");

            default:
                if (operands.Length != 1)
                    throw AsmException.AtSource(line.Number, $"у команды {op.Name} должен быть ровно один операнд - метка");
                return EncodeLabelOperand(line, op);
        }
    }

    string EncodeLabelOperand(SourceLine line, OpCode op)
    {
        var operand = line.Operands[0];
        var position = line.Positions[0];
        var relative = Rules.IsRelative(operand);

        // Проверки режима адресации - как в 1-м проходе двухпросмотрового варианта
        if (mode == AddressingMode.Direct && relative)
            throw Fail(line, 0, $"операнд {operand} команды {op.Name}: относительная адресация недоступна в режиме «только прямая адресация»");
        if (mode == AddressingMode.Relative && !relative)
            throw Fail(line, 0, $"операнд «{operand}» команды {op.Name}: прямая адресация недоступна в режиме «только относительная адресация», нужен вид [метка]");

        var label = relative ? Rules.Unbracket(operand) : operand;
        if (NotLabel(label, op) is { } problem)
            throw AsmException.AtSource(line.Number, position, problem);

        var code = op.MachineCode(relative).ToString("X2");
        var commandAddress = address;

        if (symbols.TryGetValue(label, out var symbol) && symbol.Address is { } known)
        {
            if (relative)
            {
                var offset = Offset(known, commandAddress, op, label, line.Number, position);
                return Emit(line, op.Length, code + ToComplement(offset, op)) +
                    $", относительная адресация, смещение {ToComplement(offset, op)}, без настройки";
            }

            CheckFits(known, op, label, line.Number, position);
            relocations.Add(commandAddress);
            return Emit(line, op.Length, code + known.ToString($"X{op.AddressDigits}")) +
                $", прямая адресация, метка {label} уже известна; команда занесена в таблицу настройки (M {Rules.Address(commandAddress)})";
        }

        if (symbol is null)
        {
            symbol = new Symbol(label);
            symbols.Add(label, symbol);
        }

        // Ссылка вперёд: FF..F в адресной части, метка - в ТСИ; запись M появится, когда метка определится
        var action = Emit(line, op.Length, code + new string(Unresolved, op.AddressDigits));
        symbol.Pending.Add(new PendingReference(commandAddress, records.Count - 1, line.Number, position, op, relative));
        return action + (relative
            ? $", относительная адресация, ссылка вперёд на {label}: адрес записан в ТСИ, без настройки"
            : $", прямая адресация, ссылка вперёд на {label}: адрес записан в ТСИ; при определении метки команда будет занесена в таблицу настройки");
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
