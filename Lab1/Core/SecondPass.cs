namespace Core;

public static class SecondPass
{
    public static IReadOnlyList<string> Run(FirstPassResult program)
    {
        var symbols = program.Symbols.ToDictionary(s => s.Name);
        var records = new List<string> { $"H {program.Name} {program.Start:X6} {program.Length:X6}" };

        foreach (var row in program.Rows)
        {
            var code = Code(row, symbols, program.OpCodes);
            records.Add(code.Length == 0 ? $"T {row.Address:X6} {row.Size:X2}" : $"T {row.Address:X6} {row.Size:X2} {code}");
        }

        records.Add($"E {program.Entry:X6}");
        return records;
    }

    static string Code(SupportRow row, Dictionary<string, Symbol> symbols, OpCodeTable opCodes)
    {
        if (row.OpCode is { } op)
            return row.Code + Operands(row, op, symbols, opCodes);

        var operand = row.Source.Operands[0];
        return row.Code switch
        {
            "WORD" => operand == "?" ? "" : int.Parse(operand).ToString("X6"),
            "BYTE" => ByteOperand.Code(operand),
            _ => "",
        };
    }

    static string Operands(SupportRow row, OpCode op, Dictionary<string, Symbol> symbols, OpCodeTable opCodes)
    {
        var operands = row.Source.Operands;

        return op.Length switch
        {
            1 => "",
            2 when operands.Length == 2 => $"{Rules.RegisterNumber(operands[0]):X}{Rules.RegisterNumber(operands[1]):X}",
            2 => int.Parse(operands[0]).ToString("X2"),
            _ => Address(row, op, symbols, opCodes),
        };
    }

    static string Address(SupportRow row, OpCode op, Dictionary<string, Symbol> symbols, OpCodeTable opCodes)
    {
        var name = row.Source.Operands[0];
        if (!symbols.TryGetValue(name, out var symbol))
            throw AsmException.AtSource(row.Source.Number, row.Source.Positions[0], NotLabel(name, op, opCodes));

        if (op.Length == 4)
            return symbol.Address.ToString("X6");

        if (symbol.Address > 0xFFFF)
            throw AsmException.AtSource(row.Source.Number, row.Source.Positions[0], $"адрес метки {name} ({symbol.Address:X6}) не помещается в 2 байта команды {op.Name}");

        return symbol.Address.ToString("X4");
    }

    static string NotLabel(string operand, OpCode op, OpCodeTable opCodes)
    {
        if (Rules.IsRegister(operand))
            return $"операнд {operand} команды {op.Name} - регистр, а нужна метка";
        if (Rules.IsDirective(operand))
            return $"операнд {operand} команды {op.Name} - директива, а нужна метка";
        if (opCodes.Contains(operand))
            return $"операнд {operand} команды {op.Name} - команда, а нужна метка";
        if (operand.StartsWith('[') && operand.EndsWith(']'))
            return $"относительная адресация {operand} не поддерживается в абсолютном формате, нужна метка";
        if (Rules.IsName(operand))
            return $"метка {operand} не объявлена";
        return $"операнд «{operand}» команды {op.Name} должен быть меткой";
    }
}
