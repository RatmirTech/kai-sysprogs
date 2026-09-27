namespace Core;

public sealed record SupportRow(int Line, int Address, int Size, string Code, OpCode? OpCode, string[] Operands);

public sealed record Symbol(string Name, int Address, int Line);

public sealed record FirstPassResult(
    string Name,
    int Start,
    int Length,
    int Entry,
    IReadOnlyList<SupportRow> Rows,
    IReadOnlyList<Symbol> Symbols,
    OpCodeTable OpCodes);
