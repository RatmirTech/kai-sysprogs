using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lab1SysProg.Assembler;
using Lab1SysProg.Models;

namespace Lab1SysProg;

public partial class MainWindow : Window
{
    // Таблицы, отображаемые в DataGrid-ах (двусторонняя привязка).
    public ObservableCollection<SourceCodeRow> SourceRows { get; } = new();
    public ObservableCollection<OperCodeRow> OperCodeRows { get; } = new();
    public ObservableCollection<SupportTableRow> SupportRows { get; } = new();
    public ObservableCollection<SymbolTableRow> SymbolRows { get; } = new();

    private bool _firstPassError = true;
    private FirstSecondPass? _pass;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        LoadDefaultProgram();
    }

    // Аналог Form1_Load из исходной лабораторной: заполняет таблицы демонстрационной программой.
    private void LoadDefaultProgram()
    {
        SourceRows.Add(new SourceCodeRow { Label = "PROG", Command = "START", Operand1 = "100" });
        SourceRows.Add(new SourceCodeRow { Command = "JMP", Operand1 = "L1" });
        SourceRows.Add(new SourceCodeRow { Label = "A1", Command = "RESB", Operand1 = "10" });
        SourceRows.Add(new SourceCodeRow { Label = "A2", Command = "RESW", Operand1 = "20" });
        SourceRows.Add(new SourceCodeRow { Label = "B1", Command = "WORD", Operand1 = "40" });
        SourceRows.Add(new SourceCodeRow { Label = "B2", Command = "BYTE", Operand1 = "X\"2F4C008A\"" });
        SourceRows.Add(new SourceCodeRow { Label = "B3", Command = "BYTE", Operand1 = "C\"Hello!\"" });
        SourceRows.Add(new SourceCodeRow { Label = "B4", Command = "BYTE", Operand1 = "12" });
        SourceRows.Add(new SourceCodeRow { Label = "L1", Command = "LOADR1", Operand1 = "B1" });
        SourceRows.Add(new SourceCodeRow { Command = "LOADR2", Operand1 = "B4" });
        SourceRows.Add(new SourceCodeRow { Command = "ADD", Operand1 = "R1", Operand2 = "R2" });
        SourceRows.Add(new SourceCodeRow { Command = "SAVER1", Operand1 = "B1" });
        SourceRows.Add(new SourceCodeRow { Command = "INT", Operand1 = "200" });
        SourceRows.Add(new SourceCodeRow { Command = "END", Operand1 = "100" });

        OperCodeRows.Add(new OperCodeRow { Command = "JMP", Code = "01", Length = "4" });
        OperCodeRows.Add(new OperCodeRow { Command = "LOADR1", Code = "02", Length = "4" });
        OperCodeRows.Add(new OperCodeRow { Command = "LOADR2", Code = "03", Length = "4" });
        OperCodeRows.Add(new OperCodeRow { Command = "ADD", Code = "04", Length = "2" });
        OperCodeRows.Add(new OperCodeRow { Command = "SAVER1", Code = "05", Length = "4" });
        OperCodeRows.Add(new OperCodeRow { Command = "INT", Code = "06", Length = "2" });
    }

    private void ClearTables()
    {
        SupportRows.Clear();
        SymbolRows.Clear();
        ErrorOnePassBox.Text = "";
        ErrorTwoPassBox.Text = "";
        BinaryCodeList.ItemsSource = null;
    }

    private void AppendError(TextBox box, string message)
    {
        box.Text += message + Environment.NewLine;
    }

    // Первый проход
    private void Button1_Click(object? sender, RoutedEventArgs e)
    {
        _firstPassError = true;
        _pass = new FirstSecondPass();
        ClearTables();

        // Удаляем полностью пустые строки прямо из таблицы (как и в оригинале).
        _pass.DeleteEmptyRows(SourceRows);

        var arrSource = new string[SourceRows.Count, 4];
        for (int i = 0; i < SourceRows.Count; i++)
        {
            arrSource[i, 0] = SourceRows[i].Label;
            arrSource[i, 1] = SourceRows[i].Command;
            arrSource[i, 2] = SourceRows[i].Operand1;
            arrSource[i, 3] = SourceRows[i].Operand2;
        }

        var arrOperCode = new string[OperCodeRows.Count, 3];
        for (int i = 0; i < OperCodeRows.Count; i++)
        {
            arrOperCode[i, 0] = (OperCodeRows[i].Command ?? "").ToUpper();
            arrOperCode[i, 1] = (OperCodeRows[i].Code ?? "").ToUpper();
            arrOperCode[i, 2] = (OperCodeRows[i].Length ?? "").ToUpper();
        }

        if (SourceRows.Count == 0)
        {
            AppendError(ErrorOnePassBox, "Ошибка! Таблица исходного кода пуста");
            return;
        }

        if (_pass.CheckOperationCodeTable(ref arrOperCode))
        {
            if (_pass.FirstPass(arrSource, arrOperCode, SupportRows, SymbolRows))
            {
                _firstPassError = false;
                Button2.IsEnabled = true;
                AppendError(ErrorOnePassBox, _pass.ErrorMessage);
            }
            else
            {
                AppendError(ErrorOnePassBox, _pass.ErrorMessage);
            }
        }
        else
        {
            AppendError(ErrorOnePassBox, _pass.ErrorMessage);
        }
    }

    // Второй проход
    private void Button2_Click(object? sender, RoutedEventArgs e)
    {
        BinaryCodeList.ItemsSource = null;
        ErrorTwoPassBox.Text = "";

        if (!_firstPassError && _pass != null)
        {
            var binaryCode = new ObservableCollection<string>();
            var list = new System.Collections.Generic.List<string>();
            _pass.Second_pass(list);
            foreach (var line in list)
                binaryCode.Add(line);

            BinaryCodeList.ItemsSource = binaryCode;
            AppendError(ErrorTwoPassBox, _pass.ErrorMessage);
        }
        else
        {
            _ = ShowMessage("Выполните первый проход");
        }
    }

    private async System.Threading.Tasks.Task ShowMessage(string text)
    {
        var dialog = new Window
        {
            Title = "Сообщение",
            Width = 360,
            Height = 140,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                }
            }
        };
        var closeButton = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        ((StackPanel)dialog.Content!).Children.Add(closeButton);
        closeButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    // При редактировании исходных таблиц результаты первого/второго прохода становятся неактуальными.
    private void AddSourceRow_Click(object? sender, RoutedEventArgs e)
    {
        SourceRows.Add(new SourceCodeRow());
        InvalidatePasses();
    }

    private void RemoveSourceRow_Click(object? sender, RoutedEventArgs e)
    {
        if (SourceGrid.SelectedItem is SourceCodeRow row)
            SourceRows.Remove(row);
        else if (SourceRows.Count > 0)
            SourceRows.RemoveAt(SourceRows.Count - 1);
        InvalidatePasses();
    }

    private void AddOperCodeRow_Click(object? sender, RoutedEventArgs e)
    {
        OperCodeRows.Add(new OperCodeRow());
        InvalidatePasses();
    }

    private void RemoveOperCodeRow_Click(object? sender, RoutedEventArgs e)
    {
        if (OperCodeGrid.SelectedItem is OperCodeRow row)
            OperCodeRows.Remove(row);
        else if (OperCodeRows.Count > 0)
            OperCodeRows.RemoveAt(OperCodeRows.Count - 1);
        InvalidatePasses();
    }

    private void InvalidatePasses()
    {
        ClearTables();
        Button2.IsEnabled = false;
        _firstPassError = true;
    }
}
