namespace Core;

public sealed record SourceLine(int Number, string? Label, string Command, string[] Operands);
