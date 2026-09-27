namespace Core;

public static class Samples
{
    public const string Source = """
        PROG  START   100
              JMP     L1
        A1    RESB    10
        A2    RESW    20
        B1    WORD    40
        B2    BYTE    X"2F4C008A"
        B3    BYTE    C"Hello!"
        B4    BYTE    12
        L1    LOADR1  B1
              LOADR2  B4
              ADD     R1  R2
              SAVER1  B1
              INT     200
              END     100
        """;

    public const string OpCodes = """
        JMP     01  4
        LOADR1  02  4
        LOADR2  03  4
        ADD     04  2
        SAVER1  05  4
        INT     06  2
        SUB     07  2
        NOP     08  1
        CALL    09  3
        """;
}
