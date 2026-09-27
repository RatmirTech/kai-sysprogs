using System.Text;

namespace Core;

public static class ByteOperand
{
    public static int Size(int line, string operand)
    {
        if (operand == "?")
            return 1;

        if (Rules.IsDecimal(operand))
            return int.TryParse(operand, out var value) && value <= 255
                ? 1
                : throw AsmException.AtSource(line, $"число {operand} в BYTE должно быть от 0 до 255");

        if (TryUnquote(operand, 'C', out var text))
        {
            if (text.Length == 0)
                throw AsmException.AtSource(line, "пустая строка C\"\"");
            if (!text.All(IsPrintable))
                throw AsmException.AtSource(line, $"в строке {operand} допустимы только латинские буквы, цифры и знаки ASCII без кавычек");
            return text.Length;
        }

        if (TryUnquote(operand, 'X', out var hex))
        {
            if (!Rules.IsHex(hex))
                throw AsmException.AtSource(line, $"в {operand} допустимы только 16-ричные цифры");
            if (hex.Length % 2 != 0)
                throw AsmException.AtSource(line, $"в {operand} нечётное число 16-ричных цифр");
            return hex.Length / 2;
        }

        throw AsmException.AtSource(line, $"неверный операнд BYTE «{operand}»: ожидается число 0..255, C\"текст\", X\"16-ричные цифры\" или ?");
    }

    public static string Code(string operand)
    {
        if (operand == "?")
            return "";
        if (Rules.IsDecimal(operand))
            return int.Parse(operand).ToString("X2");
        if (TryUnquote(operand, 'C', out var text))
            return Convert.ToHexString(Encoding.ASCII.GetBytes(text));
        return operand[2..^1].ToUpperInvariant();
    }

    static bool TryUnquote(string operand, char prefix, out string content)
    {
        var quoted = operand.Length >= 3 && operand[0] == prefix && operand[1] == '"' && operand[^1] == '"';
        content = quoted ? operand[2..^1] : "";
        return quoted;
    }

    static bool IsPrintable(char c) => c is >= ' ' and <= '~' and not '"';
}
