namespace Core;

public static class Rules
{
    public const int MaxAddress = 0xFFFFFF;
    public const int MaxOpCode = 0x3F;
    public const int MaxMnemonicLength = 6;
    public const int MaxNameLength = 10;
    public const int MaxAddressDigits = 6;
    public const int OpCodeDigits = 2;
    public const int WordSize = 3;

    public static readonly string[] Directives = ["START", "END", "WORD", "BYTE", "RESB", "RESW"];

    static readonly string[] Registers = [.. Enumerable.Range(0, 16).Select(i => $"R{i}")];

    public static bool IsDirective(string token) => Directives.Contains(token);

    public static int RegisterNumber(string token) => Array.IndexOf(Registers, token);

    public static bool IsRegister(string token) => RegisterNumber(token) >= 0;

    public static bool IsName(string token) =>
        token.Length > 0 && char.IsAsciiLetter(token[0]) && token.All(char.IsAsciiLetterOrDigit);

    public static bool IsHex(string token) => token.Length > 0 && token.All(char.IsAsciiHexDigit);

    public static bool IsDecimal(string token) => token.Length > 0 && token.All(char.IsAsciiDigit);

    public static bool IsAddress(string token) => IsHex(token) && token.Length <= MaxAddressDigits;
}
