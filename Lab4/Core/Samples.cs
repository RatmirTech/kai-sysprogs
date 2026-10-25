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

    const string ForwardReferences = """
        FWD START 00000100
         JMP MAIN ; ссылка вперёд
         CALL SUB1 ; ссылка вперёд, адрес 2 байта
        MAIN LOADR1 X1 ; ссылка вперёд
         LOADR2 X1 ; та же метка ещё раз
         ADD R1 R2
         SAVER1 RES
         JMP MAIN ; метка уже известна
        SUB1 NOP
        X1 WORD 7
        RES RESW 1
         END 00000100
        """;

    const string DuplicateLabel = """
        PROG START 00000100
        L1 LOADR1 B1
        B1 WORD 40
        B1 BYTE 12
         END
        """;

    const string UnknownCommand = """
        PROG START 00000100
        L1 LOADR1 B1
         LDD R1 R2
        B1 WORD 40
         END
        """;

    const string UndefinedLabel = """
        PROG START 00000100
        L1 LOADR1 B1
         JMP L9
         CALL L8
        B1 WORD 40
         END
        """;

    const string TextAfterEnd = """
        PROG START 00000100
        L1 LOADR1 B1
        B1 WORD 40
         END
         JMP L1
        """;

    const string NoEnd = """
        PROG START 00000100
        L1 LOADR1 B1
        B1 WORD 40
        """;

    const string SpacingErrors =
        "Exampl START 00001000\n" +
        "Loop  LOADR1 One\n" +
        " END";

    const string StartErrors = """
        Exampl START 10G0
        Loop NOP
         END
        """;

    const string NotLabelErrors = """
        Exampl START 00001000
        Loop LOADR1 One
         LOADR2 R1
        One WORD 1
         END
        """;

    const string RelativeOperand = """
        PROG START 00000100
        L1 JMP [L1]
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
        new("Много ссылок вперёд (без ошибок)", ForwardReferences, OpCodes),
        new("Exampl из методички (без ошибок)", Exampl, OpCodes),
        new("Все 4 формата команд (без ошибок)", Formats, OpCodes),
        new("Все виды данных (без ошибок)", Data, OpCodes),
        new("Ошибка: повторная метка", DuplicateLabel, OpCodes),
        new("Ошибка: неизвестная команда", UnknownCommand, OpCodes),
        new("Ошибка: к END остались неопределённые метки", UndefinedLabel, OpCodes),
        new("Ошибка: текст после END", TextAfterEnd, OpCodes),
        new("Ошибка: нет END", NoEnd, OpCodes),
        new("Ошибка: лишние пробелы", SpacingErrors, OpCodes),
        new("Ошибка: адрес загрузки", StartErrors, OpCodes),
        new("Ошибка: вместо метки регистр", NotLabelErrors, OpCodes),
        new("Ошибка: [метка] в абсолютном формате", RelativeOperand, OpCodes),
        new("Ошибка: адрес ссылки вперёд не помещается в CALL", FarCall, OpCodes),
        new("Ошибки ТКО", Exampl, BadOpCodes),
    ];
}
