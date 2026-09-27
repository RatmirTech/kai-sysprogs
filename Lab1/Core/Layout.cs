namespace Core;

public static class Layout
{
    public const int CommandPosition = Rules.MaxNameLength + 2;
    public const int OperandPosition = CommandPosition + Rules.MaxMnemonicLength + 1;
    public const int CodePosition = Rules.MaxMnemonicLength + 2;
    public const int LengthPosition = CodePosition + Rules.OpCodeDigits + 1;

    public static readonly string SourceHint =
        $"Метка: позиции 1-{Rules.MaxNameLength}, команда: {CommandPosition}-{OperandPosition - 2}, операнды: с {OperandPosition} через один пробел";

    public static readonly string OpCodesHint =
        $"Мнемоника: позиции 1-{Rules.MaxMnemonicLength}, код: {CodePosition}-{LengthPosition - 2}, длина: {LengthPosition}";
}
