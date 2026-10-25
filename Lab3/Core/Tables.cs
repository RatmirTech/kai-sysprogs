namespace Core;

public sealed record SupportRow(SourceLine Source, int Address, int Size, OpCode? OpCode, string Code, string? Label, bool Relative = false);

/// <summary>Метка ТСИ. External - метка объявлена в EXTDEF своей секции.</summary>
public sealed record Symbol(string Name, int Address, int Line, bool External = false);

/// <summary>Управляющая секция: от START/CSECT до следующей CSECT или END. Адреса в каждой секции идут от 0.</summary>
public sealed record Section(
    string Name,
    string Directive,
    int Length,
    int Entry,
    IReadOnlyList<SupportRow> Rows,
    IReadOnlyList<Symbol> Symbols,
    IReadOnlyList<string> ExtDefs,
    IReadOnlyList<string> ExtRefs);

public sealed record FirstPassResult(
    IReadOnlyList<Section> Sections,
    OpCodeTable OpCodes,
    AddressingMode Mode,
    IReadOnlyList<string> Errors);

/// <summary>Строка таблицы настройки. Reference - имя внешней ссылки, если адрес подставит загрузчик.</summary>
public sealed record Relocation(string Section, int Address, string? Reference);

public sealed record SecondPassResult(IReadOnlyList<string> Records, IReadOnlyList<Relocation> Relocations, IReadOnlyList<string> Errors);
