namespace Core;

public sealed record SupportRow(SourceLine Source, int Address, int Size, OpCode? OpCode, string Code, string? Label, bool Relative = false);

public sealed record Symbol(string Name, int Address, int Line);

public sealed record FirstPassResult(
    string Name,
    int Start,
    int Length,
    int Entry,
    IReadOnlyList<SupportRow> Rows,
    IReadOnlyList<Symbol> Symbols,
    OpCodeTable OpCodes,
    AddressingMode Mode,
    IReadOnlyList<string> Errors);

public sealed record SecondPassResult(IReadOnlyList<string> Records, IReadOnlyList<int> Relocations, IReadOnlyList<string> Errors);
