namespace Core;

public sealed record OpCode(string Name, int Code, int Length)
{
    public bool HasAddress => Length >= 3;

    public int AddressDigits => Length == 3 ? 4 : 6;

    public int MachineCode(AddressingMode mode) =>
        Code << 2 | (!HasAddress ? 0 : mode == AddressingMode.Direct ? 1 : 2);
}
