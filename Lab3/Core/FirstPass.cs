namespace Core;

public static class FirstPass
{
    /// <summary>Текущая управляющая секция: свой счётчик адресов от 0, своя ТСИ, свои EXTDEF и EXTREF.</summary>
    sealed class SectionState(string name, string directive)
    {
        public string Name { get; } = name;
        public string Directive { get; } = directive;
        public int Address;
        public bool BodyStarted;
        public readonly List<SupportRow> Rows = [];
        public readonly OrderedDictionary<string, Symbol> Symbols = new();
        public readonly OrderedDictionary<string, int> ExtDefs = new();
        public readonly OrderedDictionary<string, int> ExtRefs = new();
    }

    public static FirstPassResult Run(string sourceText, string opCodesText, AddressingMode mode)
    {
        var errors = new List<AsmException>();
        var opCodes = OpCodeTable.Parse(opCodesText, errors);
        if (errors.Count > 0)
            return new FirstPassResult([], opCodes, mode, AsmException.Sorted(errors));

        var sections = new List<Section>();
        var exported = new Dictionary<string, string>();
        SectionState? section = null;
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
                    section = new SectionState(ReadStart(line, errors), "START");
                    continue;
                }

                errors.Add(AsmException.AtSource(line.Number, "программа должна начинаться с директивы START"));
                section = new SectionState("", "START");
            }
            else if (line.Command == "START")
            {
                errors.Add(AsmException.AtSource(line.Number, "директива START допускается только в первой строке, новая секция начинается с CSECT"));
                continue;
            }

            section ??= new SectionState("", "START");

            if (overflow && line.Command is not ("CSECT" or "END"))
                continue;

            switch (line.Command)
            {
                case "END":
                    end = line;
                    sections.Add(Finish(section, line, errors));
                    continue;

                case "CSECT":
                    sections.Add(Finish(section, line, errors));
                    section = new SectionState(ReadSectionName(line, sections, errors), "CSECT");
                    overflow = false;
                    continue;

                case "EXTDEF" or "EXTREF":
                    ReadExternal(section, line, exported, opCodes, errors);
                    continue;
            }

            section.BodyStarted = true;

            if (line.Label is not null)
                Try(errors, () => AddSymbol(section, line.Label, line.Number));

            SupportRow? row = null;
            Try(errors, () => row = Encode(line, opCodes, section, mode));
            var size = row?.Size ?? KnownSize(line, opCodes);

            if (section.Address + size > Rules.MaxAddress + 1)
            {
                errors.Add(AsmException.AtSource(line.Number, $"секция {section.Name} выходит за границу памяти {Rules.Address(Rules.MaxAddress)}"));
                overflow = true;
                continue;
            }

            if (row is not null)
                section.Rows.Add(row);
            section.Address += size;
        }

        if (!started)
            errors.Add(new AsmException("Исходный текст пуст"));
        else if (end is null)
            errors.Add(new AsmException("Не найдена директива END"));

        return new FirstPassResult(sections, opCodes, mode, AsmException.Sorted(errors));
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

    static string ReadStart(SourceLine line, List<AsmException> errors)
    {
        if (line.Label is null)
            errors.Add(AsmException.AtSource(line.Number, "не задано имя программы перед START"));

        if (line.Operands.Length > 0 && (!Rules.TryAddress(line.Operands[0], out var start) || start != 0))
            errors.Add(Fail(line, 0, "программа в перемещаемом формате ассемблируется с адреса 0: операнд START должен отсутствовать или быть равен 0"));

        if (line.Operands.Length > 1)
            errors.Add(Fail(line, 1, "у START допускается не более одного операнда"));

        return line.Label ?? "";
    }

    static string ReadSectionName(SourceLine line, List<Section> sections, List<AsmException> errors)
    {
        if (line.Label is null)
        {
            errors.Add(AsmException.AtSource(line.Number, "не задано имя управляющей секции перед CSECT"));
            return "";
        }

        if (sections.Find(s => s.Name == line.Label) is not null)
            errors.Add(AsmException.AtSource(line.Number, 1, $"имя секции {line.Label} уже используется"));

        return line.Label;
    }

    /// <summary>CSECT и END закрывают текущую секцию; их операнд - точка входа закрываемой секции.</summary>
    static Section Finish(SectionState section, SourceLine line, List<AsmException> errors)
    {
        if (line.Command == "END" && line.Label is not null)
            errors.Add(AsmException.AtSource(line.Number, 1, "у директивы END не может быть метки"));
        if (line.Operands.Length > 1)
            errors.Add(Fail(line, 1, $"у {line.Command} допускается не более одного операнда - точка входа секции {section.Name}"));

        // Пустой может быть только единственная секция START ... END (как пустая программа в Лаб. 2)
        var onlySection = section.Directive == "START" && line.Command == "END";
        if (section.Address == 0 && !onlySection)
            errors.Add(AsmException.AtSource(line.Number, $"секция {section.Name} не содержит ни одной команды или данных"));

        var entry = line.Operands.Length > 0 ? ReadEntry(section, line, errors) : 0;

        foreach (var (name, number) in section.ExtDefs)
            if (!section.Symbols.ContainsKey(name))
                errors.Add(AsmException.AtSource(number, $"внешнее имя {name} из EXTDEF не определено в секции {section.Name}: ни одна команда или директива данных не помечена этой меткой"));

        var symbols = section.Symbols.Values
            .Select(s => s with { External = section.ExtDefs.ContainsKey(s.Name) })
            .ToList();

        return new Section(section.Name, section.Directive, section.Address, entry, section.Rows, symbols,
            [.. section.ExtDefs.Keys], [.. section.ExtRefs.Keys]);
    }

    static int ReadEntry(SectionState section, SourceLine line, List<AsmException> errors)
    {
        var operand = line.Operands[0];
        int entry;

        if (section.ExtRefs.ContainsKey(operand))
        {
            errors.Add(Fail(line, 0, $"точкой входа не может быть внешняя ссылка {operand}: она определена в другой секции"));
            return 0;
        }

        // Операнд с буквы - метка, с цифры - 16-ричный адрес (адрес A пишется как 0A)
        if (Rules.IsName(operand))
        {
            if (!section.Symbols.TryGetValue(operand, out var symbol))
            {
                errors.Add(Fail(line, 0, $"метка точки входа {operand} не объявлена в секции {section.Name}"));
                return 0;
            }
            entry = symbol.Address;
        }
        else if (!Rules.TryAddress(operand, out entry))
        {
            errors.Add(Fail(line, 0, $"точка входа «{operand}» должна быть меткой секции или 16-ричным адресом не длиннее {Rules.AddressInputDigits} цифр (начинается с цифры)"));
            return 0;
        }

        var entryExists = section.Rows.Any(r => r.OpCode is not null && r.Address == entry);
        var emptySectionEntryIsValid = section.Rows.Count == 0 && entry == 0;
        if (errors.Count == 0 && !entryExists && !emptySectionEntryIsValid)
            errors.Add(Fail(line, 0, $"точка входа {Rules.Address(entry)} не совпадает с адресом ни одной команды секции {section.Name}"));

        return entry;
    }

    /// <summary>EXTDEF/EXTREF стоят сразу после START или CSECT, до первой команды секции.</summary>
    static void ReadExternal(SectionState section, SourceLine line, Dictionary<string, string> exported, OpCodeTable opCodes, List<AsmException> errors)
    {
        var directive = line.Command;
        if (line.Label is not null)
            errors.Add(AsmException.AtSource(line.Number, 1, $"у директивы {directive} не может быть метки"));
        if (section.BodyStarted)
        {
            errors.Add(AsmException.AtSource(line.Number, $"директива {directive} должна стоять сразу после START или CSECT, до первой команды или директивы данных секции"));
            return;
        }
        if (line.Operands.Length == 0)
        {
            errors.Add(AsmException.AtSource(line.Number, $"у директивы {directive} нет операндов: нужно одно или два имени"));
            return;
        }

        for (var i = 0; i < line.Operands.Length; i++)
        {
            var name = line.Operands[i];
            AsmException Error(string message) => Fail(line, i, message);

            if (!Rules.IsName(name))
                errors.Add(Error($"операнд «{name}» директивы {directive} должен быть именем из латинских букв и цифр, начинающимся с буквы"));
            else if (Rules.IsRegister(name) || Rules.IsDirective(name) || opCodes.Contains(name))
                errors.Add(Error($"имя {name} совпадает с зарезервированным словом"));
            else if (name == section.Name)
                errors.Add(Error($"имя {name} совпадает с именем секции"));
            else if (section.ExtDefs.TryGetValue(name, out var defLine))
                errors.Add(Error($"имя {name} уже объявлено в EXTDEF (строка {defLine})"));
            else if (section.ExtRefs.TryGetValue(name, out var refLine))
                errors.Add(Error($"имя {name} уже объявлено в EXTREF (строка {refLine})"));
            else if (directive == "EXTREF")
                section.ExtRefs.Add(name, line.Number);
            else if (exported.TryGetValue(name, out var owner))
                errors.Add(Error($"внешнее имя {name} уже экспортирует секция {owner}"));
            else
            {
                exported.Add(name, section.Name);
                section.ExtDefs.Add(name, line.Number);
            }
        }
    }

    static void AddSymbol(SectionState section, string label, int line)
    {
        if (label == section.Name)
            throw AsmException.AtSource(line, 1, $"метка {label} совпадает с именем секции");
        if (section.ExtRefs.ContainsKey(label))
            throw AsmException.AtSource(line, 1, $"метка {label} объявлена в EXTREF: внешняя ссылка определяется в другой секции и не может быть меткой этой");
        if (section.Symbols.TryGetValue(label, out var existing))
            throw AsmException.AtSource(line, 1, $"метка {label} уже объявлена в строке {existing.Line}");

        section.Symbols.Add(label, new Symbol(label, section.Address, line));
    }

    static int KnownSize(SourceLine line, OpCodeTable opCodes) =>
        opCodes.Find(line.Command)?.Length ?? (line.Command == "WORD" ? Rules.WordSize : 0);

    static SupportRow Encode(SourceLine line, OpCodeTable opCodes, SectionState section, AddressingMode mode) =>
        opCodes.Find(line.Command) is { } op ? EncodeCommand(line, op, section, mode) : EncodeDirective(line, section.Address);

    static SupportRow EncodeCommand(SourceLine line, OpCode op, SectionState section, AddressingMode mode)
    {
        var operands = line.Operands;
        SupportRow Row(string rest, string? label = null, bool relative = false) =>
            new(line, section.Address, op.Length, op, op.MachineCode(relative).ToString("X2") + rest, label, relative);

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
                {
                    var (label, relative) = ReadLabel(line, op, section, mode);
                    return Row("", label, relative);
                }
        }
    }

    static (string Label, bool Relative) ReadLabel(SourceLine line, OpCode op, SectionState section, AddressingMode mode)
    {
        var operand = line.Operands[0];
        var relative = Rules.IsRelative(operand);
        var label = relative ? Rules.Unbracket(operand) : operand;

        if (section.ExtRefs.ContainsKey(label))
        {
            if (relative)
                throw Fail(line, 0, $"внешнюю ссылку {label} нельзя адресовать относительно: её адрес станет известен только при загрузке. Запишите операнд без скобок");
            if (mode == AddressingMode.Relative)
                throw Fail(line, 0, $"внешняя ссылка {label} адресуется только прямо, а прямая адресация недоступна в режиме «только относительная адресация»");
            return (label, false);
        }

        if (mode == AddressingMode.Direct && relative)
            throw Fail(line, 0, $"операнд {operand} команды {op.Name}: относительная адресация недоступна в режиме «только прямая адресация»");
        if (mode == AddressingMode.Relative && !relative)
            throw Fail(line, 0, $"операнд «{operand}» команды {op.Name}: прямая адресация недоступна в режиме «только относительная адресация», нужен вид [метка]");

        return (label, relative);
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
