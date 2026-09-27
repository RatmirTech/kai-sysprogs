namespace Core;

public sealed class AsmException(string message) : Exception(message)
{
    public static AsmException AtSource(int line, string message) =>
        new($"Исходный текст, строка {line}: {message}");

    public static AsmException AtSource(int line, int position, string message) =>
        new($"Исходный текст, строка {line}, позиция {position}: {message}");

    public static AsmException AtOpCodes(int line, int position, string message) =>
        new($"ТКО, строка {line}, позиция {position}: {message}");

    public static AsmException AtOpCodes(int line, string message) =>
        new($"ТКО, строка {line}: {message}");
}
