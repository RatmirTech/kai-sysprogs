using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Core;

namespace App;

public partial class MainWindow : Window
{
    FirstPassResult? program;

    public MainWindow()
    {
        InitializeComponent();
        SourceHint.Text = Layout.SourceHint;
        OpCodesHint.Text = Layout.OpCodesHint;
        SourceBox.TextChanged += (_, _) => Reset();
        OpCodesBox.TextChanged += (_, _) => Reset();
        ModeBox.ItemsSource = new[] { "ТОЛЬКО прямая адресация", "ТОЛЬКО относительная адресация", "Смешанная адресация" };
        ModeBox.SelectedIndex = 0;
        ModeBox.SelectionChanged += (_, _) => Reset();
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
        ModeBox.SelectedIndex = (int)sample.Mode;
        OpCodesBox.Text = sample.OpCodes;
        SourceBox.Text = sample.Source;
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

    void OnFirstPass(object? sender, RoutedEventArgs e)
    {
        Reset();
        var result = FirstPass.Run(SourceBox.Text ?? "", OpCodesBox.Text ?? "", (AddressingMode)ModeBox.SelectedIndex);
        if (result.Errors.Count > 0)
        {
            FirstErrorsBox.Text = Printer.Lines(result.Errors);
            return;
        }

        program = result;
        SupportBox.Text = Printer.SupportTable(result);
        SymbolsBox.Text = Printer.Symbols(result);
        FirstPassButton.IsEnabled = false;
        SecondPassButton.IsEnabled = true;
    }

    void OnSecondPass(object? sender, RoutedEventArgs e)
    {
        if (program is null)
            return;

        var result = SecondPass.Run(program);
        HeaderBox.Text = result.Errors.Count == 0 ? Printer.Header(program) : "";
        ObjectBox.Text = Printer.Lines(result.Records);
        RelocationsBox.Text = Printer.Relocations(result.Relocations);
        SecondErrorsBox.Text = Printer.Lines(result.Errors);
        FirstPassButton.IsEnabled = true;
        SecondPassButton.IsEnabled = false;
    }

    void Reset()
    {
        program = null;
        SupportBox.Text = "";
        SymbolsBox.Text = "";
        FirstErrorsBox.Text = "";
        HeaderBox.Text = "";
        ObjectBox.Text = "";
        RelocationsBox.Text = "";
        SecondErrorsBox.Text = "";
        FirstPassButton.IsEnabled = true;
        SecondPassButton.IsEnabled = false;
    }
}
