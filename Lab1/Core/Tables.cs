namespace Core;

public sealed record SupportRow(SourceLine Source, int Address, int Size, string Code, OpCode? OpCode);

public sealed record Symbol(string Name, int Address, int Line);

public sealed record FirstPassResult(
    string Name,
    int Start,
    int Length,
    int Entry,
    IReadOnlyList<SupportRow> Rows,
    IReadOnlyList<Symbol> Symbols,
    OpCodeTable OpCodes);
