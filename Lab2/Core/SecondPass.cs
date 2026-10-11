namespace Core;

public static class SecondPass
{
    public static SecondPassResult Run(FirstPassResult program)
    {
        var symbols = program.Symbols.ToDictionary(s => s.Name);
        var errors = new List<AsmException>();
        var records = new List<string> { $"H {program.Name} {Rules.Address(program.Length)} {Rules.Address(program.Start)}" };

        foreach (var row in program.Rows)
        {
            var code = row.Code;
            if (row.OpCode is { } op && row.Label is { } label)
            {
                try
                {
                    code += Resolve(row, op, label, symbols, program);
                }
                catch (AsmException e)
                {
                    errors.Add(e);
                    continue;
                }
            }

            var address = Rules.Address(row.Address);
            records.Add(code.Length == 0 ? $"T {address} {row.Size:X2}" : $"T {address} {row.Size:X2} {code}");
        }

        records.Add($"E {Rules.Address(program.Entry)}");
        return errors.Count > 0 ? new SecondPassResult([], AsmException.Sorted(errors)) : new SecondPassResult(records, []);
    }

    static string Resolve(SupportRow row, OpCode op, string label, Dictionary<string, Symbol> symbols, FirstPassResult program)
    {
        var position = row.Source.Positions[0];
        if (!symbols.TryGetValue(label, out var symbol))
            throw AsmException.AtSource(row.Source.Number, position, NotLabel(label, op, program));

        if (program.Mode == AddressingMode.Relative)
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

    static string NotLabel(string operand, OpCode op, FirstPassResult program)
    {
        if (Rules.IsRegister(operand))
            return $"операнд {operand} команды {op.Name} - регистр, а нужна метка";
        if (Rules.IsDirective(operand))
            return $"операнд {operand} команды {op.Name} - директива, а нужна метка";
        if (program.OpCodes.Contains(operand))
            return $"операнд {operand} команды {op.Name} - команда, а нужна метка";
        if (operand == program.Name)
            return $"операнд {operand} команды {op.Name} - имя программы, а нужна метка";
        if (Rules.IsName(operand))
            return $"метка {operand} не объявлена";
        return $"операнд «{operand}» команды {op.Name} должен быть меткой";
    }
}
