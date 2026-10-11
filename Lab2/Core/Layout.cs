namespace Core;

public static class Layout
{
    public const string SourceHint =
        "[метка] команда [операнд1] [операнд2] - части через один пробел. Строка без метки начинается с одного пробела. Текст с пробелами - в кавычках: \"Hello world\". Прямая адресация: JMP метка. Относительная: JMP [метка]. Комментарий - после ;";

    public const string OpCodesHint = "мнемоника код длина - через один пробел, например: JMP 01 4";
}
