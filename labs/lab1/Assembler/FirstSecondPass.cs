using System;
using System.Collections.Generic;
using Lab1SysProg.Models;

namespace Lab1SysProg.Assembler
{
    /// <summary>
    /// Двухпросмотровый ассемблер для программы в АБСОЛЮТНОМ формате (Лаба 1).
    /// Логика первого/второго прохода портирована один в один из исходной лабораторной
    /// (.NET Framework, WinForms). Единственное, что изменилось, — источники/приёмники
    /// данных: вместо DataGridView/ListBox используются простые списки/модели, которые
    /// связывает Avalonia UI (см. MainWindow).
    /// </summary>
    public class FirstSecondPass
    {
        string NameProg = "";
        public string ErrorMessage = "";
        int EndAddress = 0;   // Адрес точки входа в программу
        int StartAddress = 0; // Адрес начала программы
        int AddressCount = 0;
        const int MAX_MemmoryAdr = 16777215; // максимальное значение адреса оперативной памяти

        // | Метка | МКОП | Операнд1 | Операнд2 | -- Вспомогательная таблица
        List<List<string>> SupportTable = new List<List<string>>();

        // | Метка | Адрес | -- Таблица символических имён
        List<List<string>> SymbolNameTable = new List<List<string>>();

        // | МКОП | МКОП Код | Размер | -- Таблица кодов операций
        string[,] CodeTable = new string[0, 0];

        public void AddStringToSupportTable(string n1, string n2, string n3, string n4)
        {
            SupportTable[0].Add(n1);
            SupportTable[1].Add(n2);
            SupportTable[2].Add(n3);
            SupportTable[3].Add(n4);
        }

        public bool MemoryCheck()
        {
            if (AddressCount < 0 || AddressCount > MAX_MemmoryAdr)
            {
                ErrorMessage = "Ошибка! Выход за границы доступной памяти";
                return false;
            }
            return true;
        }

        // передается массив, номер строки который надо разложить по переменным
        // проверяется допустимость введеных символов
        public bool GetRow(string[,] mas, int number, out string label, out string command, out string dir1, out string dir2)
        {
            label = mas[number, 0];
            command = mas[number, 1].ToUpper();
            dir1 = mas[number, 2];
            dir2 = mas[number, 3];

            if (CheckData.CheckLabel(label) || CheckData.CheckDir(dir1) || CheckData.CheckDir(dir2))
            {
                return false;
            }
            if (number > 0 && NameProg == label.ToUpper())
                return false;

            if ((CheckData.OnlySymbolsAndNumbers(label) || label.Length == 0) &&
                (CheckData.OnlySymbolsAndNumbers(command)) &&
                (CheckData.OnlySymbolsAndNumbers(dir2) || dir2.Length == 0))
            {
                if (label.Length > 0)
                {
                    // метка должна начинаться с символа
                    if (CheckData.OnlySymbols(Convert.ToString(label[0])))
                        return true;
                    else
                        return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        // Процедура проверяет таблицу кодов операций
        public bool CheckOperationCodeTable(ref string[,] OperationCodeTable) // проверяем таблицу операций
        {
            int rows = OperationCodeTable.GetLength(0);

            for (int i = 0; i < rows; i++)
            {
                if (OperationCodeTable[i, 0] == "" || OperationCodeTable[i, 1] == "" || OperationCodeTable[i, 2] == "")
                {
                    ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Пустая ячейка в таблице кодов операций недопустима";
                    return false;
                }

                if (OperationCodeTable[i, 0].Length > 6 || OperationCodeTable[i, 1].Length > 2 || OperationCodeTable[i, 2].Length > 1)
                {
                    ErrorMessage = "Строка {" + (i + 1) + "} Ошибка в размере строки в таблице кодов операций (Команда (от 1 до 6),МКОП (от 1 до 2), Длина(не более 1))";
                    return false;
                }

                if (!CheckData.OnlySymbolsAndNumbers(OperationCodeTable[i, 0]))
                {
                    ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! В поле команды недопустимый символ";
                    return false;
                }

                // проверяем поле МКОП команды, что там были только 16чные цифры
                if (CheckData.IsAdressPossible(OperationCodeTable[i, 1]))
                {
                    if (CheckData.IsDirective(OperationCodeTable[i, 0]) || CheckData.OnlyRegisters(OperationCodeTable[i, 0]))
                    {
                        ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Поле код команды является зарезервированным словом";
                        return false;
                    }
                    // преобразуем их в число
                    int count = Converting.HexToDec(OperationCodeTable[i, 1]);
                    if (count > 63)
                    {
                        ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Поле код команды не должен превышать 3F";
                        return false;
                    }
                    else
                    {
                        // если оно в пределах байта, но записано просто как ... "F" поправляем его на "0F"
                        if (OperationCodeTable[i, 1].Length == 1)
                            OperationCodeTable[i, 1] = Converting.ToTwoChars(OperationCodeTable[i, 1]);
                    }
                }
                else
                {
                    ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Посторонние символы в поле МКОП";
                    return false;
                }

                int value = 0;
                if (CheckData.OnlyNumbers(OperationCodeTable[i, 2]))
                {
                    bool result = Int32.TryParse(OperationCodeTable[i, 2], out value);
                    if (result)
                    {
                        if (value <= 0 || value > 4)
                        {
                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Проверьте размер команды (от 1 до 4)";
                            return false;
                        }
                    }
                }
                else
                {
                    ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! В поле размер операции недопустимый символ";
                    return false;
                }

                // проверяем уникальность поля названия команды
                for (int k = i + 1; k < rows; k++)
                {
                    string cmp_str1 = OperationCodeTable[i, 0];
                    string cmp_str2 = OperationCodeTable[k, 0];
                    if (Equals(cmp_str1, cmp_str2))
                    {
                        ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Найдены совпадения в команде";
                        return false;
                    }
                }
                // проверяем уникальность поля код операции
                for (int k = i + 1; k < rows; k++)
                {
                    string str1 = Convert.ToString(Converting.HexToDec(OperationCodeTable[i, 1]));
                    string str2 = Convert.ToString(Converting.HexToDec(OperationCodeTable[k, 1]));
                    if (Equals(str1, str2))
                    {
                        ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Найдены совпадения в коде команды";
                        return false;
                    }
                }
            }

            return true;
        }

        // Удаляет полностью пустые строки из таблицы исходного кода перед обработкой
        // (аналог DeleteEmptyRows из WinForms-версии, где так же выкидывались пустые строки).
        // IList<> вместо List<>, чтобы работать как со списком-копией, так и с
        // ObservableCollection, которая напрямую отображается в DataGrid.
        public void DeleteEmptyRows(IList<SourceCodeRow> sourceRows)
        {
            for (int i = sourceRows.Count - 1; i >= 0; i--)
            {
                if (sourceRows[i].IsEmpty)
                    sourceRows.RemoveAt(i);
            }
        }

        // Определяем сколько байт нужно на команду
        public int FindCodeLengthInCodeTable(int index)
        {
            if (Int32.TryParse(SupportTable[1][index], System.Globalization.NumberStyles.HexNumber, null, out int hexNumber))
            {
                for (int j = 0; j < CodeTable.GetLength(0); j++)
                {
                    if (Int32.TryParse(CodeTable[j, 1], out int codeNumber))
                    {
                        if (hexNumber == (codeNumber * 4 + 1))
                        {
                            return int.Parse(CodeTable[j, 2]);
                        }
                    }
                }
            }
            return -1;
        }

        // проверяет есть ли такая команда в массиве команд
        public int FindCodeInCodeTable(string label, string[,] code_table)
        {
            for (int i = 0; i < code_table.GetLength(0); i++)
            {
                if (label == code_table[i, 0])
                    return i;
            }
            return -1;
        }

        // проверяет есть ли такая метка в массиве меток
        public int FindLabelInLabelTable(string label)
        {
            for (int i = 0; i < SymbolNameTable[0].Count; i++)
            {
                if (label == SymbolNameTable[0][i])
                    return i;
            }
            return -1;
        }

        // Процедура проверяет таблицу исходного кода, ищет директиву START,END.
        // Проверяет адрес памяти, не выходим ли за границу и т.д.
        public bool FirstPass(string[,] SourceCodeTable, string[,] OperCodeTable,
            IList<SupportTableRow> outSupportTable, IList<SymbolTableRow> outSymbolTable)
        {
            AddressCount = 0;
            StartAddress = 0;
            EndAddress = 0;

            SymbolNameTable.Add(new List<string>());
            SymbolNameTable.Add(new List<string>());

            SupportTable.Add(new List<string>());
            SupportTable.Add(new List<string>());
            SupportTable.Add(new List<string>());
            SupportTable.Add(new List<string>());

            int rows = SourceCodeTable.GetLength(0) - 1;

            int StartFlag = 0; // флаг найденной директивы START
            int EndFlag = 0;   // флаг найденной директивы END

            string[,] arr_CodeTable = OperCodeTable;
            CodeTable = OperCodeTable;

            string label, MKOP, Operand1, Operand2;

            for (int i = 0; i <= rows; i++)
            {
                if (StartFlag == 1)
                {
                    if (AddressCount > MAX_MemmoryAdr)
                    {
                        ErrorMessage = "Строка {" + (i + 1) + "Ошибка! Произошло переполнение";
                        return false;
                    }
                }

                if (EndFlag == 1)
                {
                    break;
                }

                if (!GetRow(SourceCodeTable, i, out label, out MKOP, out Operand1, out Operand2))
                {
                    ErrorMessage = "Синтаксическая ошибка строка = " + (i + 1);
                    return false;
                }

                int number_in_LabelTable = FindLabelInLabelTable(label);

                if (number_in_LabelTable != -1)
                {
                    ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Найдена уже существующая метка " + label;
                    return false;
                }
                else
                {
                    if (label != "" && StartFlag == 1)
                    {
                        SymbolNameTable[0].Add(label);
                        SymbolNameTable[1].Add(Converting.ToSixChars(Converting.DecToHex(AddressCount)));
                    }

                    if (CheckData.IsDirective(MKOP))
                    {
                        switch (MKOP)
                        {
                            case "START":
                                {
                                    if (i == 0 && StartFlag == 0)
                                    {
                                        StartFlag = 1;
                                        if (CheckData.IsAdressPossible(Operand1))
                                        {
                                            AddressCount = Converting.HexToDec(Operand1);
                                            StartAddress = AddressCount;

                                            if (AddressCount == 0)
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Адрес начала программы не может быть равен 0";
                                                return false;
                                            }

                                            if (AddressCount > MAX_MemmoryAdr || AddressCount < 0)
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Неправильный адрес загрузки.";
                                                return false;
                                            }

                                            if (label == "")
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Не задано имя программы";
                                                return false;
                                            }
                                            if (label.Length > 10)
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "Ошибка! Превышена длина имени программы( > 10 символов)";
                                                return false;
                                            }

                                            AddStringToSupportTable(label, MKOP, Converting.ToSixChars(Operand1), "");
                                            NameProg = label;
                                            if (Operand2.Length > 0)
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы START не рассматривается\n";
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Неверный адрес начала программы";
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        ErrorMessage = "Строка {" + (i + 1) + "} Ошибка в директиве START";
                                        return false;
                                    }
                                }
                                break;

                            case "WORD":
                                {
                                    int number;
                                    if (int.TryParse(Operand1, out number))
                                    {
                                        if (number >= 0 && number <= MAX_MemmoryAdr)
                                        {
                                            if (AddressCount + 3 > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Ошибка! Произошло переполнение";
                                                return false;
                                            }
                                            if (!MemoryCheck())
                                            {
                                                return false;
                                            }

                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                MKOP, Convert.ToString(number), "");

                                            AddressCount = AddressCount + 3;

                                            if (!MemoryCheck())
                                            { return false; }

                                            if (AddressCount < 0 || AddressCount > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Выход за границы доступной памяти";
                                                return false;
                                            }

                                            if (Operand2.Length > 0)
                                            {
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы WORD не рассматривается\n";
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Отрицательное число, либо превышено максимальное значение числа";
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        if (Operand1.Length == 1 && Operand1 == "?")
                                        {
                                            if (AddressCount + 3 > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Ошибка! Произошло переполнение";
                                                return false;
                                            }

                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                MKOP, Operand1, "");

                                            AddressCount = AddressCount + 3;

                                            if (!MemoryCheck()) { return false; }

                                            if (Operand2.Length > 0)
                                            {
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы WORD не рассматривается\n";
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Невозможно выполнить преобразование в число " + Operand1;
                                            return false;
                                        }
                                    }
                                }
                                break;
                            case "BYTE":
                                {
                                    int number;
                                    if (int.TryParse(Operand1, out number))
                                    {
                                        if (number >= 0 && number <= 255)
                                        {
                                            if (AddressCount + 1 > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Ошибка! Произошло переполнение";
                                                return false;
                                            }

                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                MKOP, Convert.ToString(number), "");

                                            AddressCount = AddressCount + 1;

                                            if (!MemoryCheck())
                                            { return false; }

                                            if (Operand2.Length > 0)
                                            {
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы BYTE не рассматривается\n";
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Отрицательное число, либо превышеноо максимальное значение числа";
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        string symbols = CheckData.String(Operand1);
                                        if (symbols != "")
                                        {
                                            if (AddressCount + symbols.Length > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Ошибка! Произошло переполнение";
                                                return false;
                                            }

                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                MKOP, Operand1, "");

                                            AddressCount = AddressCount + symbols.Length;

                                            if (!MemoryCheck())
                                            { return false; }

                                            if (Operand2.Length > 0)
                                            {
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы BYTE не рассматривается\n";
                                                return false;
                                            }
                                            continue;
                                        }

                                        symbols = "";
                                        symbols = CheckData.ByteString(Operand1);

                                        if (symbols != "")
                                        {
                                            int lenght = symbols.Length;
                                            if ((lenght % 2) == 0)
                                            {
                                                if (AddressCount + symbols.Length / 2 > MAX_MemmoryAdr)
                                                {
                                                    ErrorMessage = "Ошибка! Произошло переполнение";
                                                    return false;
                                                }

                                                AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                    MKOP, Operand1, "");

                                                AddressCount = AddressCount + symbols.Length / 2;

                                                if (!MemoryCheck())
                                                { return false; }

                                                if (Operand2.Length > 0)
                                                {
                                                    ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы BYTE не рассматривается \n";
                                                    return false;
                                                }
                                                continue;
                                            }
                                            else
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Невозможно преобразовать BYTE нечетное количество символов";
                                                return false;
                                            }
                                        }

                                        if (Operand1.Length == 1 && Operand1 == "?")
                                        {
                                            if (AddressCount + 1 > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Ошибка! Произошло переполнение";
                                                return false;
                                            }

                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                MKOP, Operand1, "");

                                            AddressCount = AddressCount + 1;

                                            if (!MemoryCheck()) { return false; }

                                            if (Operand2.Length > 0)
                                            {
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы BYTE не рассматривается \n";
                                                return false;
                                            }

                                            continue;
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Неверный формат строки " + Operand1;
                                            return false;
                                        }
                                    }
                                }
                                break;

                            case "RESB":
                                {
                                    int number;
                                    if (int.TryParse(Operand1, out number))
                                    {
                                        if (number > 0)
                                        {
                                            if (AddressCount > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Ошибка! Переполнение памяти";
                                                return false;
                                            }
                                            else
                                            {
                                                if (AddressCount + number > MAX_MemmoryAdr)
                                                {
                                                    ErrorMessage = "Ошибка! Произошло переполнение";
                                                    return false;
                                                }

                                                AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                    MKOP, Convert.ToString(number), "");

                                                AddressCount = AddressCount + number;

                                                if (!MemoryCheck())
                                                { return false; }

                                                if (Operand2.Length > 0)
                                                {
                                                    ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы RESB не рассматривается \n";
                                                    return false;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Количество байт равно нулю или меньше нуля";
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        ErrorMessage = "Строка {" + (i + 1) + "} Ошибка! Невозможно выполнить преобразование в число " + Operand1;
                                        return false;
                                    }
                                }
                                break;
                            case "RESW":
                                {
                                    int number;
                                    if (int.TryParse(Operand1, out number))
                                    {
                                        if (number > 0)
                                        {
                                            if (AddressCount + number * 3 > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Произошло переполнение";
                                                return false;
                                            }

                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                MKOP, Convert.ToString(number), "");

                                            AddressCount = AddressCount + number * 3;

                                            if (!MemoryCheck())
                                            { return false; }

                                            if (AddressCount < 0 || AddressCount > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "} Выход за границы доступной памяти";
                                                return false;
                                            }

                                            if (Operand2.Length > 0)
                                            {
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд директивы RESW не рассматривается \n";
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Количество слов равно нулю или меньше нуля";
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        ErrorMessage = "Строка {" + (i + 1) + "} Невозможно выполнить преобразование в число " + Operand1;
                                        return false;
                                    }
                                }
                                break;

                            case "END":
                                {
                                    if (StartFlag == 1 && EndFlag == 0)
                                    {
                                        EndFlag = 1;
                                        if (Operand1.Length == 0)
                                        {
                                            EndAddress = StartAddress;
                                        }
                                        else
                                        {
                                            if (CheckData.IsAdressPossible(Operand1))
                                            {
                                                EndAddress = Converting.HexToDec(Operand1);
                                                if (EndAddress >= StartAddress && EndAddress <= AddressCount)
                                                {
                                                    break;
                                                }
                                                else
                                                {
                                                    ErrorMessage = "Строка {" + (i + 1) + "} Адрес точки входа неверен";
                                                    return false;
                                                }
                                            }
                                            else
                                            {
                                                ErrorMessage = "Строка {" + (i + 1) + "} Неверный адрес входа в программу";
                                                return false;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ErrorMessage = "Строка {" + (i + 1) + "} Ошибка в директиве END";
                                        return false;
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (MKOP.Length > 0)
                        {
                            int num = FindCodeInCodeTable(MKOP, arr_CodeTable);
                            if (num > -1)
                            {
                                if (arr_CodeTable[num, 2] == "1")
                                {
                                    if (AddressCount + 1 > MAX_MemmoryAdr)
                                    {
                                        ErrorMessage = "Произошло переполнение";
                                        return false;
                                    }
                                    int AddressationType = Converting.HexToDec(arr_CodeTable[num, 1]) * 4;

                                    AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                            Converting.ToTwoChars(Converting.DecToHex(AddressationType)), "", "");

                                    AddressCount = AddressCount + 1;

                                    if (!MemoryCheck())
                                    { return false; }

                                    if (Operand1.Length > 0 || Operand2.Length > 0)
                                    {
                                        ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Операнды не рассматриваются в команде " + arr_CodeTable[num, 0] + "\n";
                                        return false;
                                    }
                                }
                                else if (arr_CodeTable[num, 2] == "2")
                                {
                                    int number;
                                    if (int.TryParse(Operand1, out number))
                                    {
                                        if (number >= 0 && number <= 255)
                                        {
                                            int AddressationType = Converting.HexToDec(arr_CodeTable[num, 1]) * 4;

                                            if (AddressCount + 2 > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Произошло переполнение";
                                                return false;
                                            }

                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                                    Converting.ToTwoChars(Converting.DecToHex(AddressationType)), Operand1, "");

                                            AddressCount = AddressCount + 2;

                                            if (!MemoryCheck())
                                            { return false; }

                                            if (Operand2.Length > 0)
                                            {
                                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд команды" + arr_CodeTable[num, 0] + " не рассматривается \n";
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Отрицательное число, либо превышено максимальное значение числа";
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        if (CheckData.OnlyRegisters(Operand1) && CheckData.OnlyRegisters(Operand2))
                                        {
                                            int AddressationType = Converting.HexToDec(arr_CodeTable[num, 1]) * 4;
                                            if (AddressCount + 2 > MAX_MemmoryAdr)
                                            {
                                                ErrorMessage = "Произошло переполнение";
                                                return false;
                                            }
                                            AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                                    Converting.ToTwoChars(Converting.DecToHex(AddressationType)), Operand1, Operand2);

                                            AddressCount = AddressCount + 2;

                                            if (!MemoryCheck())
                                            { return false; }
                                        }
                                        else
                                        {
                                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка в команде " + arr_CodeTable[num, 0];
                                            return false;
                                        }
                                    }
                                }
                                else if (arr_CodeTable[num, 2] == "3")
                                {
                                    if (AddressCount + 3 > MAX_MemmoryAdr)
                                    {
                                        ErrorMessage = "Произошло переполнение";
                                        return false;
                                    }

                                    int AddressationType = Converting.HexToDec(arr_CodeTable[num, 1]) * 4 + 1;

                                    AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                            Converting.ToTwoChars(Converting.DecToHex(AddressationType)), Operand1, Operand2);

                                    AddressCount = AddressCount + 3;

                                    if (string.IsNullOrWhiteSpace(Operand1))
                                    {
                                        ErrorMessage = $"Строка {{ {i + 1} }} Ошибка! У команды " + arr_CodeTable[num, 0] + " отсутствует операнд.";
                                        return false;
                                    }

                                    if (!MemoryCheck())
                                    { return false; }

                                    if (Operand2.Length > 0)
                                    {
                                        ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд команды" + arr_CodeTable[num, 0] + "  не рассматривается\n";
                                        return false;
                                    }
                                }
                                else if (arr_CodeTable[num, 2] == "4")
                                {
                                    if (AddressCount + 4 > MAX_MemmoryAdr)
                                    {
                                        ErrorMessage = "Произошло переполнение";
                                        return false;
                                    }

                                    int AddressationType = Converting.HexToDec(arr_CodeTable[num, 1]) * 4 + 1;

                                    AddStringToSupportTable(Converting.ToSixChars(Converting.DecToHex(AddressCount)),
                                                            Converting.ToTwoChars(Converting.DecToHex(AddressationType)), Operand1, Operand2);

                                    AddressCount = AddressCount + 4;

                                    if (string.IsNullOrWhiteSpace(Operand1))
                                    {
                                        ErrorMessage = $"Строка {{ {i + 1} }} Ошибка! У команды " + arr_CodeTable[num, 0] + " отсутствует операнд.";
                                        return false;
                                    }

                                    if (!MemoryCheck())
                                    { return false; }

                                    if (Operand2.Length > 0)
                                    {
                                        ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Второй операнд команды " + arr_CodeTable[num, 0] + " не рассматривается\n";
                                        return false;
                                    }
                                }
                                else
                                {
                                    ErrorMessage = "Строка {" + (i + 1) + "} Размер команды больше установленного";
                                    return false;
                                }
                            }
                            else
                            {
                                ErrorMessage = "Строка {" + (i + 1) + "} МКОП не найден в таблице исходный текст программы " + MKOP;
                                return false;
                            }
                        }
                        else
                        {
                            ErrorMessage = "Строка {" + (i + 1) + "} Ошибка в МКОП";
                            return false;
                        }
                    }
                }
            }

            if (EndFlag == 0)
            {
                ErrorMessage = ErrorMessage + "Не найдена точка входа в программу \n";
                return false;
            }

            // Помещаем сформированную Вспомогательную таблицу в результат
            for (int i = 0; i < SupportTable[0].Count; i++)
            {
                outSupportTable.Add(new SupportTableRow
                {
                    Address = SupportTable[0][i],
                    Command = SupportTable[1][i],
                    Operand1 = SupportTable[2][i],
                    Operand2 = SupportTable[3][i],
                });
            }

            // Помещаем сформированную Таблицу символических имён в результат
            for (int j = 0; j < SymbolNameTable[1].Count; j++)
            {
                outSymbolTable.Add(new SymbolTableRow
                {
                    Label = SymbolNameTable[0][j],
                    Address = SymbolNameTable[1][j],
                });
            }

            return true;
        }

        // проверка операнда во втором проходе
        // Если в операнде метка - возвращает адрес метки
        // Если в операнде регистр - возвращает номер регистра
        // Если там строка типа C"????" - возвращает ASCII код
        // Если там строка типа X"????" - возвращает строку
        // Если что-то в 10ричном формате - то вернет это же число в 16ричном формате
        // иначе возвращает пустую строку
        public string CheckingOperandSecondPass(int index, string operand1, out int error, out int label)
        {
            label = 0;
            string result = "";
            error = 0;
            if (operand1 != "")
            {
                int LabelStringNum = FindLabelInLabelTable(operand1);
                if (LabelStringNum > -1)
                {
                    label = 1;
                    int codeLength = FindCodeLengthInCodeTable(index);
                    if (codeLength == 3)
                    {
                        return SymbolNameTable[1][LabelStringNum].Substring(2);
                    }
                    else if (codeLength == 4)
                    {
                        return SymbolNameTable[1][LabelStringNum];
                    }
                }
                else
                {
                    int regnum = CheckData.RegisterNumber(operand1);
                    if (regnum > -1)
                    {
                        return result = Converting.DecToHex(regnum, true);
                    }
                    else
                    {
                        if (CheckData.OnlyNumbers(operand1))
                        {
                            return result = Converting.DecToHex(Convert.ToInt32(operand1));
                        }
                        else
                        {
                            string sentence = "";
                            sentence = CheckData.String(operand1);
                            if (sentence != "")
                            {
                                return Converting.ToASCII(sentence);
                            }
                            sentence = CheckData.ByteString(operand1);
                            if (sentence != "")
                            {
                                return sentence;
                            }

                            error = 1;
                        }
                    }
                }
            }

            return result;
        }

        int Div(int x)
        {
            return (x - 1) / 2 + 1;
        }

        // Второй проход
        public bool Second_pass(List<string> binaryCode)
        {
            ErrorMessage = "";
            for (int i = 0; i < SupportTable[0].Count; i++)
            {
                string address = SupportTable[0][i];
                string MKOP = SupportTable[1][i];
                string operand1 = SupportTable[2][i];
                string operand2 = SupportTable[3][i];

                if (i == 0)
                {
                    string str = Converting.EditingString("H", SupportTable[0][0], SupportTable[2][0], "",
                                                           Convert.ToString(AddressCount - StartAddress), "");
                    binaryCode.Add(str);
                }
                else
                {
                    int error, label1, label2;

                    string result1 = CheckingOperandSecondPass(i, operand1, out error, out label1);

                    if (error == 1)
                    { ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Ошибка в операнде, код отсутствует в ТСИ " + operand1; break; }

                    string result2 = CheckingOperandSecondPass(i, operand2, out error, out label2);

                    if (error == 1)
                    { ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Ошибка в операнде,код отсутствует в ТСИ " + operand2; break; }

                    if (CheckData.IsDirective(MKOP) == true)
                    {
                        if (MKOP == "RESB")
                        {
                            MKOP = "";
                            string str1 = Converting.EditingString("T", address, MKOP, result1, "", "");
                            binaryCode.Add(str1);
                            continue;
                        }

                        if (MKOP == "RESW")
                        {
                            MKOP = "";
                            string str2 = Converting.EditingString("T", address, MKOP, Converting.ToTwoChars(Converting.DecToHex(Convert.ToInt32(operand1) * 3)), "", "");
                            binaryCode.Add(str2);
                            continue;
                        }

                        if (MKOP == "BYTE")
                        {
                            MKOP = "";
                            string str2 = Converting.EditingString("T", address, MKOP, Converting.ToTwoChars(Converting.DecToHex(Div(result1.Length + result2.Length))), result1, result2);
                            binaryCode.Add(str2);
                            continue;
                        }

                        if (MKOP == "WORD")
                        {
                            MKOP = "";
                            result1 = Converting.ToSixChars(result1);
                            string str2 = Converting.EditingString("T", address, MKOP, Converting.ToTwoChars(Converting.DecToHex(Div(result1.Length + result2.Length))), result1, result2);
                            binaryCode.Add(str2);
                            continue;
                        }

                        if (MKOP == "BYTE" && operand1 == "?")
                        {
                            MKOP = "";
                            string str2 = Converting.EditingString("T", address, MKOP, Converting.ToTwoChars(Converting.DecToHex(1)), "", "");
                            binaryCode.Add(str2);
                            continue;
                        }

                        if (MKOP == "WORD" && operand1 == "?")
                        {
                            MKOP = "";
                            string str2 = Converting.EditingString("T", address, MKOP, Converting.ToTwoChars(Converting.DecToHex(3)), "", "");
                            binaryCode.Add(str2);
                            continue;
                        }
                    }
                    else
                    {
                        int Type_of_adr = (byte)Converting.HexToDec(MKOP) & 0x03;
                        if (Type_of_adr == 1)
                        {
                            if (label1 != 1)
                            {
                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Для данного типа адресации операнд должен быть меткой " + MKOP;
                                binaryCode.Clear();
                                return false;
                            }
                            if (result2 != "")
                            {
                                ErrorMessage = ErrorMessage + "Строка {" + (i + 1) + "} Данный тип адрессации поддерживает 1 операнд";
                                binaryCode.Clear();
                                return false;
                            }
                        }

                        string RecordLength = Converting.ToTwoChars(Converting.DecToHex(Div(MKOP.Length + result1.Length + result2.Length)));
                        string str5 = Converting.EditingString("T", address, MKOP, RecordLength, result1, result2);
                        binaryCode.Add(str5);
                    }
                }
            }
            string str3 = Converting.EditingString("E", Converting.ToSixChars(Converting.DecToHex(EndAddress)), "", "", "", "");
            binaryCode.Add(str3);

            if (ErrorMessage != "")
                binaryCode.Clear();

            return true;
        }
    }
}
