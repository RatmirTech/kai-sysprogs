using System.Text;

namespace Core;

public static class ByteOperand
{
    public static int Size(string operand, Func<string, AsmException> fail)
    {
        if (operand == "?")
            return 1;

        if (Rules.IsDecimal(operand))
            return Rules.TryNumber(operand, Rules.MaxByte, out _)
                ? 1
                : throw fail($"число {operand} в BYTE должно быть от 0 до {Rules.MaxByte} (не более 3 цифр)");

        if (TryText(operand, out var text))
        {
            if (text.Length == 0)
                throw fail("пустая строка");
            if (!text.All(IsPrintable))
                throw fail($"в строке {operand} допустимы только латинские буквы, цифры и знаки ASCII без кавычек");
            if (text.Length > Rules.MaxDataBytes)
                throw fail($"строка длиннее {Rules.MaxDataBytes} символов");
            return text.Length;
        }

        if (TryUnquote(operand, "X\"", out var hex))
        {
            if (!Rules.IsHex(hex))
                throw fail($"в {operand} допустимы только 16-ричные цифры");
            if (hex.Length % 2 != 0)
                throw fail($"в {operand} нечётное число 16-ричных цифр");
            if (hex.Length / 2 > Rules.MaxDataBytes)
                throw fail($"в X\"...\" больше {Rules.MaxDataBytes} байт ({Rules.MaxDataBytes * 2} цифр)");
            return hex.Length / 2;
        }

        throw fail($"неверный операнд BYTE «{operand}»: ожидается число 0..{Rules.MaxByte}, C\"текст\", \"текст\", X\"16-ричные цифры\" или ?");
    }

    public static string Code(string operand)
    {
        if (operand == "?")
            return "";
        if (Rules.IsDecimal(operand))
            return int.Parse(operand).ToString("X2");
        if (TryText(operand, out var text))
            return Convert.ToHexString(Encoding.ASCII.GetBytes(text));
        return operand[2..^1].ToUpperInvariant();
    }

    static bool TryText(string operand, out string text) =>
        TryUnquote(operand, "C\"", out text) || TryUnquote(operand, "\"", out text);

    static bool TryUnquote(string operand, string prefix, out string content)
    {
        var quoted = operand.Length > prefix.Length && operand.StartsWith(prefix) && operand[^1] == '"';
        content = quoted ? operand[prefix.Length..^1] : "";
        return quoted;
    }

    static bool IsPrintable(char c) => c is >= ' ' and <= '~' and not '"';
}
