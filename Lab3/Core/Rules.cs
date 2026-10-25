namespace Core;

public static class Rules
{
    public const int MaxAddress = 0xFFFFFF;
    public const int AddressDigits = 6;
    public const int AddressInputDigits = 8;
    public const int MaxOpCode = 0x3F;
    public const int OpCodeDigits = 2;
    public const int MaxMnemonicLength = 6;
    public const int MaxNameLength = 256;
    public const int MaxOperandLength = MaxNameLength;
    public const int WordSize = 3;
    public const int MaxByte = 255;
    public const int MaxDataBytes = 30;

    public static readonly string[] Directives = ["START", "END", "WORD", "BYTE", "RESB", "RESW", "CSECT", "EXTDEF", "EXTREF"];

    static readonly string[] Registers = [.. Enumerable.Range(0, 16).Select(i => $"R{i}")];

    public static bool IsDirective(string token) => Directives.Contains(token);

    public static int RegisterNumber(string token) => Array.IndexOf(Registers, token);

    public static bool IsRegister(string token) => RegisterNumber(token) >= 0;

    public static bool IsName(string token) =>
        token.Length > 0 && char.IsAsciiLetter(token[0]) && token.All(char.IsAsciiLetterOrDigit);

    public static bool IsRelative(string token) => token.Length > 2 && token[0] == '[' && token[^1] == ']';

    public static string Unbracket(string token) => token[1..^1];

    public static bool IsHex(string token) => token.Length > 0 && token.All(char.IsAsciiHexDigit);

    public static bool IsDecimal(string token) => token.Length > 0 && token.All(char.IsAsciiDigit);

    public static bool TryNumber(string token, int max, out int value)
    {
        value = 0;
        return IsDecimal(token) && token.Length <= max.ToString().Length && int.TryParse(token, out value) && value <= max;
    }

    public static bool TryAddress(string token, out int value)
    {
        value = 0;
        if (!IsHex(token) || token.Length > AddressInputDigits || Convert.ToInt64(token, 16) > MaxAddress)
            return false;

        value = Convert.ToInt32(token, 16);
        return true;
    }

    public static string Address(int value) => value.ToString($"X{AddressDigits}");
}
