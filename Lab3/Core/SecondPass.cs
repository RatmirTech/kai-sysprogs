namespace Core;

public static class SecondPass
{
    /// <summary>Каждая управляющая секция ассемблируется отдельно и даёт свой объектный модуль: H, D, R, T, M, E.</summary>
    public static SecondPassResult Run(FirstPassResult program)
    {
        var errors = new List<AsmException>();
        var relocations = new List<Relocation>();
        var records = new List<string>();

        foreach (var section in program.Sections)
            records.AddRange(RunSection(section, program, relocations, errors));

        return errors.Count > 0
            ? new SecondPassResult([], [], AsmException.Sorted(errors))
            : new SecondPassResult(records, relocations, []);
    }

    static List<string> RunSection(Section section, FirstPassResult program, List<Relocation> relocations, List<AsmException> errors)
    {
        var symbols = section.Symbols.ToDictionary(s => s.Name);
        var sectionRelocations = new List<Relocation>();
        var text = new List<string>();

        foreach (var row in section.Rows)
        {
            var code = row.Code;
            if (row.OpCode is { } op && row.Label is { } label)
            {
                try
                {
                    if (section.ExtRefs.Contains(label))
                    {
                        // Адрес внешней ссылки неизвестен: пишем нули, загрузчик подставит адрес по записи M
                        code += new string('0', op.AddressDigits);
                        sectionRelocations.Add(new Relocation(section.Name, row.Address, label));
                    }
                    else
                    {
                        code += Resolve(row, op, label, symbols, section, program);
                        if (!row.Relative)
                            sectionRelocations.Add(new Relocation(section.Name, row.Address, null));
                    }
                }
                catch (AsmException e)
                {
                    errors.Add(e);
                    continue;
                }
            }

            var address = Rules.Address(row.Address);
            text.Add(code.Length == 0 ? $"T {address} {row.Size:X2}" : $"T {address} {row.Size:X2} {code}");
        }

        relocations.AddRange(sectionRelocations);
        return
        [
            $"H {section.Name} {Rules.Address(0)} {Rules.Address(section.Length)}",
            .. section.ExtDefs.Select(name => $"D {name} {Rules.Address(symbols[name].Address)}"),
            .. section.ExtRefs.Select(name => $"R {name}"),
            .. text,
            .. sectionRelocations.Select(r => r.Reference is null ? $"M {Rules.Address(r.Address)}" : $"M {Rules.Address(r.Address)} {r.Reference}"),
            $"E {Rules.Address(section.Entry)}",
        ];
    }

    static string Resolve(SupportRow row, OpCode op, string label, Dictionary<string, Symbol> symbols, Section section, FirstPassResult program)
    {
        var position = row.Source.Positions[0];
        if (!symbols.TryGetValue(label, out var symbol))
            throw AsmException.AtSource(row.Source.Number, position, NotLabel(label, op, section, program));

        if (row.Relative)
            return Offset(row, op, label, symbol, position);

        if (symbol.Address > (1 << (op.AddressDigits * 4)) - 1)
            throw AsmException.AtSource(row.Source.Number, position,
                $"адрес метки {label} ({Rules.Address(symbol.Address)}) не помещается в {op.AddressDigits / 2} байта команды {op.Name}");

        return symbol.Address.ToString($"X{op.AddressDigits}");
    }

    static string Offset(SupportRow row, OpCode op, string label, Symbol symbol, int position)
    {
        var offset = symbol.Address - (row.Address + row.Size);
        var half = 1 << (op.AddressDigits * 4 - 1);
        if (offset < -half || offset >= half)
            throw AsmException.AtSource(row.Source.Number, position,
                $"смещение до метки {label} ({offset}) не помещается в {op.AddressDigits / 2} байта команды {op.Name}");

        return (offset & (2 * half - 1)).ToString($"X{op.AddressDigits}");
    }

    static string NotLabel(string operand, OpCode op, Section section, FirstPassResult program)
    {
        if (Rules.IsRegister(operand))
            return $"операнд {operand} команды {op.Name} - регистр, а нужна метка";
        if (Rules.IsDirective(operand))
            return $"операнд {operand} команды {op.Name} - директива, а нужна метка";
        if (program.OpCodes.Contains(operand))
            return $"операнд {operand} команды {op.Name} - команда, а нужна метка";
        if (program.Sections.Any(s => s.Name == operand))
            return $"операнд {operand} команды {op.Name} - имя секции, а нужна метка";
        if (program.Sections.FirstOrDefault(s => s != section && s.Symbols.Any(x => x.Name == operand)) is { } other)
            return $"метка {operand} объявлена в другой секции ({other.Name}): чтобы обратиться к ней, объявите её в EXTDEF секции {other.Name} и в EXTREF секции {section.Name}";
        if (Rules.IsName(operand))
            return $"метка {operand} не объявлена в секции {section.Name}";
        return $"операнд «{operand}» команды {op.Name} должен быть меткой";
    }
}
