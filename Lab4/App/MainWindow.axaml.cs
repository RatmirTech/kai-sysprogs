using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Core;

namespace App;

public partial class MainWindow : Window
{
    OnePassAssembler? assembler;

    public MainWindow()
    {
        InitializeComponent();
        SourceHint.Text = Layout.SourceHint;
        OpCodesHint.Text = Layout.OpCodesHint;
        AddressBox.MaxLength = Rules.AddressInputDigits;
        SourceBox.TextChanged += (_, _) => OnSourceChanged();
        OpCodesBox.TextChanged += (_, _) => Reset();
        AddressBox.TextChanged += (_, _) => OnAddressChanged();
        ExamplesBox.ItemsSource = Samples.All.Select(s => s.Title).ToList();
        ExamplesBox.SelectionChanged += (_, _) => LoadSample();
        ExamplesBox.SelectedIndex = 0;
        TrackCaret(SourceBox, SourceCaret);
        TrackCaret(OpCodesBox, OpCodesCaret);
    }

    void LoadSample()
    {
        if (ExamplesBox.SelectedIndex < 0)
            return;

        var sample = Samples.All[ExamplesBox.SelectedIndex];
        OpCodesBox.Text = sample.OpCodes;
        SourceBox.Text = sample.Source;
    }

    void OnSourceChanged()
    {
        Reset();
        if (AddressBox.IsFocused)
            return;

        var address = LoadAddress.Read(SourceBox.Text ?? "");
        AddressBox.Text = address ?? "";
        AddressBox.IsEnabled = address is not null;
    }

    void OnAddressChanged()
    {
        if (AddressBox.IsFocused)
            SourceBox.Text = LoadAddress.Write(SourceBox.Text ?? "", AddressBox.Text ?? "");
    }

    static void TrackCaret(TextBox box, TextBlock caret)
    {
        void Update()
        {
            var (line, column) = TextLine.Position(box.Text ?? "", box.CaretIndex);
            caret.Text = $"Строка {line}, позиция {column}";
        }

        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty)
                Update();
        };
        Update();
    }

    OnePassAssembler Assembler => assembler ??= new OnePassAssembler(SourceBox.Text ?? "", OpCodesBox.Text ?? "");

    void OnStep(object? sender, RoutedEventArgs e)
    {
        Assembler.Step();
        ShowState();
    }

    void OnRun(object? sender, RoutedEventArgs e)
    {
        Assembler.Run();
        ShowState();
    }

    void OnReset(object? sender, RoutedEventArgs e) => Reset();

    void ShowState()
    {
        var a = Assembler;
        ObjectBox.Text = Printer.Lines(a.Records);
        SymbolsBox.Text = Printer.Symbols(a.Symbols);
        ErrorsBox.Text = a.Error ?? "";
        LastStepText.Text = a.LastLine is { } last ? $"Строка {last}: {a.LastAction}" : "";
        ProgressText.Text = a.Finished ? "Готово: двоичный код сформирован"
            : a.Error is not null ? $"Остановлено с ошибкой{(a.LastLine is { } l ? $" на строке {l}" : "")}. Исправьте текст или нажмите «Сброс»"
            : $"Следующая строка: {a.NextLine}";
        StepButton.IsEnabled = RunButton.IsEnabled = !a.Stopped;
        HighlightNextLine(a.NextLine);
    }

    // Выделяет в исходном тексте строку, которую обработает следующий «Шаг»
    void HighlightNextLine(int? number)
    {
        var text = SourceBox.Text ?? "";
        if (number is not { } n)
        {
            SourceBox.SelectionStart = SourceBox.SelectionEnd = 0;
            return;
        }

        var start = 0;
        for (var i = 1; i < n; i++)
            start = text.IndexOf('\n', start) + 1;
        var end = text.IndexOf('\n', start);
        SourceBox.SelectionStart = start;
        SourceBox.SelectionEnd = end < 0 ? text.Length : end;
    }

    void Reset()
    {
        assembler = null;
        ObjectBox.Text = "";
        SymbolsBox.Text = "";
        ErrorsBox.Text = "";
        LastStepText.Text = "";
        ProgressText.Text = "Нажмите «Шаг», чтобы обработать одну строку, или «Полный проход»";
        StepButton.IsEnabled = RunButton.IsEnabled = true;
    }
}
