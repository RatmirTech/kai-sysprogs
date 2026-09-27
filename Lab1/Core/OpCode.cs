namespace Core;

public sealed record OpCode(string Name, int Code, int Length)
{
    public bool IsDirect => Length >= 3;

    public int MachineCode => Code << 2 | (IsDirect ? 1 : 0);
}
