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
        SourceBox.Text = Samples.Source;
        OpCodesBox.Text = Samples.OpCodes;
        SourceBox.TextChanged += (_, _) => Reset();
        OpCodesBox.TextChanged += (_, _) => Reset();
    }

    void OnFirstPass(object? sender, RoutedEventArgs e)
    {
        Reset();
        try
        {
            program = FirstPass.Run(SourceBox.Text ?? "", OpCodesBox.Text ?? "");
            SupportBox.Text = Printer.SupportTable(program);
            SymbolsBox.Text = Printer.Symbols(program);
            FirstPassButton.IsEnabled = false;
            SecondPassButton.IsEnabled = true;
        }
        catch (AsmException ex)
        {
            FirstErrorsBox.Text = ex.Message;
        }
    }

    void OnSecondPass(object? sender, RoutedEventArgs e)
    {
        if (program is null)
            return;

        try
        {
            HeaderBox.Text = Printer.Header(program);
            ObjectBox.Text = Printer.ObjectModule(SecondPass.Run(program));
        }
        catch (AsmException ex)
        {
            HeaderBox.Text = "";
            SecondErrorsBox.Text = ex.Message;
        }

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
        SecondErrorsBox.Text = "";
        FirstPassButton.IsEnabled = true;
        SecondPassButton.IsEnabled = false;
    }
}
