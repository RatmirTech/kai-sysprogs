namespace Core;

public sealed class AsmException(string message, int line = int.MaxValue) : Exception(message)
{
    public int Line { get; } = line;

    public static AsmException AtSource(int line, string message) =>
        new($"Исходный текст, строка {line}: {message}", line);

    public static AsmException AtSource(int line, int position, string message) =>
        new($"Исходный текст, строка {line}, позиция {position}: {message}", line);

    public static AsmException AtOpCodes(int line, string message) =>
        new($"ТКО, строка {line}: {message}", line);

    public static AsmException AtOpCodes(int line, int position, string message) =>
        new($"ТКО, строка {line}, позиция {position}: {message}", line);

    public static List<string> Sorted(IEnumerable<AsmException> errors) =>
        [.. errors.OrderBy(e => e.Line).Select(e => e.Message)];
}
