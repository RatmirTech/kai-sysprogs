namespace Core;

/// <summary>Ссылка вперёд: команда по адресу Address ждёт метку; её запись T лежит в объектном модуле под индексом Record.
/// Relative - вид адресации команды: true - относительная (исправлять смещением), false - прямая (абсолютным адресом + запись M).</summary>
public sealed record PendingReference(int Address, int Record, int Line, int Position, OpCode OpCode, bool Relative);

/// <summary>Строка ТСИ секции. Address = null, пока метка не определена; Pending - команды, которые её ждут;
/// External - метка объявлена в EXTDEF своей секции.</summary>
public sealed class Symbol(string name)
{
    public string Name { get; } = name;
    public int? Address { get; set; }
    public int Line { get; set; }
    public bool External { get; set; }
    public List<PendingReference> Pending { get; } = [];
}

/// <summary>Строка таблицы настройки секции. Reference - имя внешней ссылки, если адрес подставит загрузчик.</summary>
public sealed record Relocation(int Address, string? Reference);

/// <summary>
/// Управляющая секция: от START/CSECT до следующей CSECT или END. Адреса в каждой секции идут от 0.
/// Однопросмотровый вариант: запись H (с длиной-заглушкой) и записи D/R создаются при открытии секции,
/// записи T - по мере обработки строк, записи M и E - при закрытии секции (CSECT/END).
/// </summary>
public sealed class SectionState(string name, string directive)
{
    public string Name { get; } = name;
    public string Directive { get; } = directive;
    public int Address { get; internal set; }
    public bool BodyStarted { get; internal set; }

    /// <summary>Индекс записи H этой секции в общем списке записей (длина дописывается при закрытии).</summary>
    public int HRecord { get; internal set; }

    public OrderedDictionary<string, Symbol> Symbols { get; } = [];
    public OrderedDictionary<string, int> ExtDefs { get; } = [];
    public OrderedDictionary<string, int> ExtRefs { get; } = [];

    /// <summary>Имя EXTDEF -> индекс его записи D (адрес дописывается, когда метка определится).</summary>
    public Dictionary<string, int> ExtDefRecords { get; } = [];

    public List<Relocation> Relocations { get; } = [];
}

/// <summary>
/// Однопросмотровый ассемблер для программы в полностью перемещаемом формате (логика двухпросмотрового
/// варианта, но каждая строка обрабатывается один раз: код сразу пишется в объектный модуль).
/// Программа делится на управляющие секции (START/CSECT ... CSECT/END), каждая ассемблируется от адреса 0
/// и даёт свой объектный модуль H/D/R/T/M/E. Поддерживаются прямая и относительная адресация ([метка]),
/// внешние имена (EXTDEF) и внешние ссылки (EXTREF): операнд-внешняя ссылка кодируется нулями,
/// адрес подставит загрузчик по записи M с именем ссылки.
/// Если метка ещё не определена (ссылка вперёд), в адресную часть пишется FF..F, а адрес команды
/// запоминается в ТСИ секции; когда метка определяется, ждущие записи T исправляются:
/// для прямой адресации - абсолютным адресом (запись M), для относительной - смещением в дополнительном коде.
/// Step() обрабатывает ровно одну строку - это и есть трассировка.
/// </summary>
public sealed class OnePassAssembler
{
    public const char Unresolved = 'F';

    readonly string[] lines;
    readonly OpCodeTable opCodes;
    readonly AddressingMode mode;
    readonly List<SectionState> closedSections = [];
    readonly List<string> records = [];
    SectionState? current;
    int next;
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

    /// <summary>Закрытые секции и текущая (последней) - для отображения таблиц.</summary>
    public IReadOnlyList<SectionState> Sections =>
        current is null ? closedSections : [.. closedSections, current];

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
            throw AsmException.AtSource(line.Number, "директива START допускается только в первой строке, новая секция начинается с CSECT");
        if (line.Command == "END")
        {
            var closed = CloseSection(line);
            Finished = true;
            return $"{closed}; программа завершена";
        }

        if (line.Command == "CSECT")
        {
            var closed = CloseSection(line);
            return $"{closed}; {OpenSection(ReadSectionName(line))}";
        }

        if (line.Command is "EXTDEF" or "EXTREF")
            return ReadExternal(line);

        Section.BodyStarted = true;

        var defined = line.Label is null ? "" : DefineSymbol(line.Label, line.Number);
        var action = opCodes.Find(line.Command) is { } op ? EncodeCommand(line, op) : EncodeDirective(line);
        return defined.Length == 0 ? action : $"{defined}; {action}";
    }

    SectionState Section =>
        current ?? throw new InvalidOperationException("внутренняя ошибка: нет текущей секции");

    string ProcessStart(SourceLine line)
    {
        if (line.Label is null)
            throw AsmException.AtSource(line.Number, "не задано имя программы перед START");
        if (line.Operands.Length > 1)
            throw Fail(line, 1, "у START допускается не более одного операнда");
        if (line.Operands.Length > 0 && (!Rules.TryAddress(line.Operands[0], out var start) || start != 0))
            throw Fail(line, 0, "программа в перемещаемом формате ассемблируется с адреса 0: операнд START должен отсутствовать или быть равен 0");

        started = true;
        return OpenSection(line.Label);
    }

    /// <summary>Открыть новую секцию: запись H с длиной-заглушкой (допишется при закрытии, как в Лаб. 5 на START).</summary>
    string OpenSection(string name)
    {
        current = new SectionState(name, closedSections.Count == 0 ? "START" : "CSECT");
        current.HRecord = records.Count;
        // Длина секции станет известна только при закрытии (CSECT/END)
        records.Add($"H {name} {Rules.Address(0)} {new string('-', Rules.AddressDigits)}");
        var directive = current.Directive;
        return $"{directive}: открыта секция {name}, адрес загрузки {Rules.Address(0)}";
    }

    /// <summary>Имя новой секции из CSECT: имя обязательно, совпадение с закрытыми секциями запрещено.</summary>
    string ReadSectionName(SourceLine line)
    {
        if (line.Label is null)
            throw AsmException.AtSource(line.Number, "не задано имя управляющей секции перед CSECT");

        if (closedSections.Find(s => s.Name == line.Label) is not null)
            throw AsmException.AtSource(line.Number, 1, $"имя секции {line.Label} уже используется");

        return line.Label;
    }

    /// <summary>CSECT и END закрывают текущую секцию; их операнд - точка входа закрываемой секции.</summary>
    string CloseSection(SourceLine line)
    {
        var section = Section;

        if (line.Command == "END" && line.Label is not null)
            throw AsmException.AtSource(line.Number, 1, "у директивы END не может быть метки");
        if (line.Operands.Length > 1)
            throw Fail(line, 1, $"у {line.Command} допускается не более одного операнда - точка входа секции {section.Name}");

        // Пустой может быть только единственная секция START ... END (как пустая программа в Лаб. 5)
        var onlySection = section.Directive == "START" && line.Command == "END";
        if (section.Address == 0 && !onlySection)
            throw AsmException.AtSource(line.Number, $"секция {section.Name} не содержит ни одной команды или данных");

        var entry = line.Operands.Length > 0 ? ReadEntry(section, line) : 0;

        // К закрытию секции не должно остаться неопределённых ссылок вперёд
        var undefined = section.Symbols.Values.Where(s => s.Address is null).ToList();
        if (undefined.Count > 0)
            throw AsmException.AtSource(line.Number, string.Join("; ", undefined.Select(s => UndefinedMessage(section, s))));

        // Внешнее имя должно быть определено меткой в своей секции
        foreach (var (name, number) in section.ExtDefs)
            if (!section.Symbols.TryGetValue(name, out var symbol) || symbol.Address is null)
                throw AsmException.AtSource(number, $"внешнее имя {name} из EXTDEF не определено в секции {section.Name}: ни одна команда или директива данных не помечена этой меткой");

        // Длина секции в H, затем записи M (прямая адресация / внешние ссылки), затем E - как в двухпросмотровом варианте
        records[section.HRecord] = $"H {section.Name} {Rules.Address(0)} {Rules.Address(section.Address)}";
        foreach (var r in section.Relocations.OrderBy(r => r.Address))
            records.Add(r.Reference is null ? $"M {Rules.Address(r.Address)}" : $"M {Rules.Address(r.Address)} {r.Reference}");
        records.Add($"E {Rules.Address(entry)}");

        current = null;
        closedSections.Add(section);
        return $"{line.Command}: секция {section.Name} закрыта, длина {Rules.Address(section.Address)}, точка входа {Rules.Address(entry)}, записей в таблице настройки: {section.Relocations.Count}";
    }

    /// <summary>Неопределённая метка: с подсказкой, если она объявлена в другой секции (как во 2-м проходе двухпросмотрового варианта).</summary>
    string UndefinedMessage(SectionState section, Symbol symbol)
    {
        var used = $"использована в строках {string.Join(", ", symbol.Pending.Select(p => p.Line))}";
        if (closedSections.FirstOrDefault(s => s.Symbols.TryGetValue(symbol.Name, out var other) && other.Address is not null) is { } owner)
            return $"метка {symbol.Name} объявлена в другой секции ({owner.Name}): чтобы обратиться к ней, объявите её в EXTDEF секции {owner.Name} и в EXTREF секции {section.Name} ({used})";

        return $"метка {symbol.Name} не объявлена в секции {section.Name} ({used})";
    }

    static int ReadEntry(SectionState section, SourceLine line)
    {
        var operand = line.Operands[0];

        if (section.ExtRefs.ContainsKey(operand))
            throw Fail(line, 0, $"точкой входа не может быть внешняя ссылка {operand}: она определена в другой секции");

        int entry;
        // Операнд с буквы - метка, с цифры - 16-ричный адрес (адрес A пишется как 0A)
        if (Rules.IsName(operand))
        {
            if (!section.Symbols.TryGetValue(operand, out var symbol) || symbol.Address is not { } address)
                throw Fail(line, 0, $"метка точки входа {operand} не объявлена в секции {section.Name}");
            entry = address;
        }
        else if (!Rules.TryAddress(operand, out entry))
        {
            throw Fail(line, 0, $"точка входа «{operand}» должна быть меткой секции или 16-ричным адресом не длиннее {Rules.AddressInputDigits} цифр (начинается с цифры)");
        }

        // Как в эталоне: точка входа должна лежать в пределах секции (пустая секция - только адрес 0)
        var end = section.Address;
        var inside = entry < end || entry == 0 && end == 0;
        if (!inside)
            throw Fail(line, 0, $"точка входа {Rules.Address(entry)} должна лежать в пределах секции {section.Name} ({(end > 0 ? $"{Rules.Address(0)}-{Rules.Address(end - 1)}" : Rules.Address(0))})");

        return entry;
    }

    /// <summary>EXTDEF/EXTREF стоят сразу после START или CSECT, до первой команды секции.</summary>
    string ReadExternal(SourceLine line)
    {
        var section = Section;
        var directive = line.Command;
        if (line.Label is not null)
            throw AsmException.AtSource(line.Number, 1, $"у директивы {directive} не может быть метки");
        if (section.BodyStarted)
            throw AsmException.AtSource(line.Number, $"директива {directive} должна стоять сразу после START или CSECT, до первой команды или директивы данных секции");
        if (line.Operands.Length == 0)
            throw AsmException.AtSource(line.Number, $"у директивы {directive} нет операндов: нужно одно или два имени");

        var names = new List<string>();
        for (var i = 0; i < line.Operands.Length; i++)
        {
            var name = line.Operands[i];
            if (!Rules.IsName(name))
                throw Fail(line, i, $"операнд «{name}» директивы {directive} должен быть именем из латинских букв и цифр, начинающимся с буквы");
            if (Rules.IsRegister(name) || Rules.IsDirective(name) || opCodes.Contains(name))
                throw Fail(line, i, $"имя {name} совпадает с зарезервированным словом");
            if (name == section.Name)
                throw Fail(line, i, $"имя {name} совпадает с именем секции");
            if (section.ExtDefs.TryGetValue(name, out var defLine))
                throw Fail(line, i, $"имя {name} уже объявлено в EXTDEF (строка {defLine})");
            if (section.ExtRefs.TryGetValue(name, out var refLine))
                throw Fail(line, i, $"имя {name} уже объявлено в EXTREF (строка {refLine})");

            if (directive == "EXTREF")
                section.ExtRefs.Add(name, line.Number);
            else
                section.ExtDefs.Add(name, line.Number);
            names.Add(name);
        }

        // Записи D/R - сразу после H, как в двухпросмотровом варианте; адрес в D допишется, когда имя определится меткой
        if (directive == "EXTDEF")
            foreach (var name in names)
            {
                records.Add($"D {name} {new string('-', Rules.AddressDigits)}");
                section.ExtDefRecords[name] = records.Count - 1;
            }
        else
            foreach (var name in names)
                records.Add($"R {name}");

        var what = directive == "EXTDEF" ? "внешние имена" : "внешние ссылки";
        return $"{directive}: {what} {string.Join(", ", names)} секции {section.Name}";
    }

    string DefineSymbol(string label, int number)
    {
        var section = Section;

        if (label == section.Name)
            throw AsmException.AtSource(number, 1, $"метка {label} совпадает с именем секции");
        if (section.ExtRefs.ContainsKey(label))
            throw AsmException.AtSource(number, 1, $"метка {label} объявлена в EXTREF: внешняя ссылка определяется в другой секции и не может быть меткой этой");
        if (section.Symbols.TryGetValue(label, out var symbol) && symbol.Address is not null)
            throw AsmException.AtSource(number, 1, $"метка {label} уже объявлена в строке {symbol.Line}");

        if (symbol is null)
        {
            symbol = new Symbol(label);
            section.Symbols.Add(label, symbol);
        }

        symbol.Address = section.Address;
        symbol.Line = number;

        // Внешнее имя определилось: дописываем его адрес в запись D
        if (section.ExtDefs.ContainsKey(label))
        {
            symbol.External = true;
            records[section.ExtDefRecords[label]] = $"D {label} {Rules.Address(section.Address)}";
        }

        if (symbol.Pending.Count == 0)
            return $"метка {label} = {Rules.Address(section.Address)}";

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
                var offset = Offset(section.Address, p.Address, p.OpCode, label, p.Line, p.Position);
                value = ToComplement(offset, p.OpCode);
            }
            else
            {
                // Прямая адресация: записывается абсолютный адрес, команда заносится в таблицу настройки
                anyDirect = true;
                CheckFits(section.Address, p.OpCode, label, p.Line, p.Position);
                section.Relocations.Add(new Relocation(p.Address, null));
                value = section.Address.ToString($"X{p.OpCode.AddressDigits}");
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
        return $"метка {label} = {Rules.Address(section.Address)}, {how} в командах {fixedAt}";
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
        var section = Section;
        var operand = line.Operands[0];
        var position = line.Positions[0];
        var relative = Rules.IsRelative(operand);
        var label = relative ? Rules.Unbracket(operand) : operand;

        // Внешняя ссылка: адрес станет известен только при загрузке - пишем нули и заносим в таблицу настройки
        if (section.ExtRefs.ContainsKey(label))
        {
            if (relative)
                throw Fail(line, 0, $"внешнюю ссылку {label} нельзя адресовать относительно: её адрес станет известен только при загрузке. Запишите операнд без скобок");
            if (mode == AddressingMode.Relative)
                throw Fail(line, 0, $"внешняя ссылка {label} адресуется только прямо, а прямая адресация недоступна в режиме «только относительная адресация»");

            var code = op.MachineCode(false).ToString("X2");
            var commandAddress = section.Address;
            section.Relocations.Add(new Relocation(commandAddress, label));
            return Emit(line, op.Length, code + new string('0', op.AddressDigits)) +
                $", внешняя ссылка {label}: нули, команда занесена в таблицу настройки (M {Rules.Address(commandAddress)} {label})";
        }

        // Проверки режима адресации - как в двухпросмотровом варианте
        if (mode == AddressingMode.Direct && relative)
            throw Fail(line, 0, $"операнд {operand} команды {op.Name}: относительная адресация недоступна в режиме «только прямая адресация»");
        if (mode == AddressingMode.Relative && !relative)
            throw Fail(line, 0, $"операнд «{operand}» команды {op.Name}: прямая адресация недоступна в режиме «только относительная адресация», нужен вид [метка]");

        if (NotLabel(label, op) is { } problem)
            throw AsmException.AtSource(line.Number, position, problem);

        var opCode = op.MachineCode(relative).ToString("X2");
        var at = section.Address;

        if (section.Symbols.TryGetValue(label, out var symbol) && symbol.Address is { } known)
        {
            if (relative)
            {
                var offset = Offset(known, at, op, label, line.Number, position);
                return Emit(line, op.Length, opCode + ToComplement(offset, op)) +
                    $", относительная адресация, смещение {ToComplement(offset, op)}, без настройки";
            }

            CheckFits(known, op, label, line.Number, position);
            section.Relocations.Add(new Relocation(at, null));
            return Emit(line, op.Length, opCode + known.ToString($"X{op.AddressDigits}")) +
                $", прямая адресация, метка {label} уже известна; команда занесена в таблицу настройки (M {Rules.Address(at)})";
        }

        if (symbol is null)
        {
            symbol = new Symbol(label);
            section.Symbols.Add(label, symbol);
        }

        // Ссылка вперёд: FF..F в адресной части, метка - в ТСИ; запись M появится, когда метка определится
        var action = Emit(line, op.Length, opCode + new string(Unresolved, op.AddressDigits));
        symbol.Pending.Add(new PendingReference(at, records.Count - 1, line.Number, position, op, relative));
        return action + (relative
            ? $", относительная адресация, ссылка вперёд на {label}: адрес записан в ТСИ, без настройки"
            : $", прямая адресация, ссылка вперёд на {label}: адрес записан в ТСИ; при определении метки команда будет занесена в таблицу настройки");
    }

    string? NotLabel(string operand, OpCode op)
    {
        var section = Section;
        if (Rules.IsRegister(operand))
            return $"операнд {operand} команды {op.Name} - регистр, а нужна метка";
        if (Rules.IsDirective(operand))
            return $"операнд {operand} команды {op.Name} - директива, а нужна метка";
        if (opCodes.Contains(operand))
            return $"операнд {operand} команды {op.Name} - команда, а нужна метка";
        if (closedSections.Any(s => s.Name == operand))
            return $"операнд {operand} команды {op.Name} - имя секции, а нужна метка";
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
        var section = Section;
        if (section.Address + size > Rules.MaxAddress + 1)
            throw AsmException.AtSource(line.Number, $"секция {section.Name} выходит за границу памяти {Rules.Address(Rules.MaxAddress)}");

        var at = Rules.Address(section.Address);
        records.Add(code.Length == 0 ? $"T {at} {size:X2}" : $"T {at} {size:X2} {code}");
        section.Address += size;
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
