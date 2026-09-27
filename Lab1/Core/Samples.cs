namespace Core;

public sealed record Sample(string Title, string Source, string OpCodes);

public static class Samples
{
    public const string OpCodes = """
        JMP 01 4
        LOADR1 02 4
        LOADR2 03 4
        ADD 04 2
        SAVER1 05 4
        INT 06 2
        SUB 07 2
        NOP 08 1
        CALL 09 3
        """;

    public const string Source = """
        PROG START 00000100
         JMP L1
        A1 RESB 10
        A2 RESW 20
        B1 WORD 40
        B2 BYTE X"2F4C008A"
        B3 BYTE C"Hello!"
        B4 BYTE 12
        L1 LOADR1 B1
         LOADR2 B4
         ADD R1 R2
         SAVER1 B1
         INT 200
         END 00000100
        """;

    const string Exampl = """
        Exampl START 00001000
        Loop LOADR1 One
         LOADR2 Two
         ADD R1 R2
         SAVER1 Rez
         JMP Loop
        One WORD 1
        Two WORD 2
        Rez WORD ?
        Text BYTE "Hello"
         END
        """;

    const string Formats = """
        FORMATS START 00002000
        BEGIN NOP ; формат 1: только код
         SUB R3 R4 ; формат 2: два регистра
         INT 33 ; формат 2: число
         CALL PROC ; формат 3: адрес 2 байта
         JMP BEGIN ; формат 4: адрес 3 байта
        PROC NOP
         END 00002000
        """;

    const string Data = """
        DATA START 0000A000
         JMP MAIN
        W1 WORD 16777215
        W2 WORD ?
        B1 BYTE 255
        B2 BYTE ?
        S1 BYTE "A b!"
        H1 BYTE X"00FF"
        BUF RESB 16
        ARR RESW 100
        MAIN LOADR1 W1
         SAVER1 W2
         END 0000A000
        """;

    const string ManyFirstPassErrors = """
        Exampl START 00001000
        Loop LOADR1 One
         LDD Two
         ADD R1 One
        ADD SAVER1 Rez
         JMP Loop
        One WORD 1
        One WORD 2
        Rez WORD ?
        Text BYTE 300
         END
        """;

    const string SpacingErrors =
        "Exampl START 00001000\n" +
        "Loop  LOADR1 One\n" +
        "  ADD R1 R2\n" +
        " JMP Loop \n" +
        "One\tWORD 1\n" +
        " END";

    const string StartErrors = """
        Exampl START 10G0
        Loop NOP
        """;

    const string SecondPassErrors = """
        Exampl START 00001000
        Loop LOADR1 Onw
         LOADR2 R1
         SAVER1 ADD
         JMP Exampl
         CALL Loop
        One WORD 1
         END
        """;

    const string FarCall = """
        FAR START 0000FFF0
         CALL SUB1
        BUF RESB 100
        SUB1 NOP
         END 0000FFF0
        """;

    const string BadOpCodes = """
        JMP 01 4
        LOADR1 01 4
        LOADR2 03 5
        WORD 04 2
        SAVER1 40 4
        INT 06 2
        SUB 07 2
        NOP 08 1
        CALL 09 3
        """;

    public static readonly IReadOnlyList<Sample> All =
    [
        new("По умолчанию (без ошибок)", Source, OpCodes),
        new("Exampl из методички (без ошибок)", Exampl, OpCodes),
        new("Все 4 формата команд (без ошибок)", Formats, OpCodes),
        new("Все виды данных (без ошибок)", Data, OpCodes),
        new("Ошибки 1-го прохода: несколько сразу", ManyFirstPassErrors, OpCodes),
        new("Ошибки 1-го прохода: пробелы и табуляция", SpacingErrors, OpCodes),
        new("Ошибки 1-го прохода: адрес загрузки, нет END", StartErrors, OpCodes),
        new("Ошибки ТКО", Exampl, BadOpCodes),
        new("Ошибки 2-го прохода: вместо метки регистр, команда, имя программы", SecondPassErrors, OpCodes),
        new("Ошибка 2-го прохода: адрес не помещается в CALL", FarCall, OpCodes),
    ];
}
