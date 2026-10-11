using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lab1SysProg.Models
{
    /// <summary>
    /// Базовый класс для строк, редактируемых пользователем в DataGrid.
    /// </summary>
    public abstract class ObservableRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    /// <summary>Строка таблицы исходного кода: Метка | МКОП | Операнд1 | Операнд2.</summary>
    public class SourceCodeRow : ObservableRow
    {
        private string _label = "";
        private string _command = "";
        private string _operand1 = "";
        private string _operand2 = "";

        public string Label { get => _label; set => SetField(ref _label, value ?? ""); }
        public string Command { get => _command; set => SetField(ref _command, value ?? ""); }
        public string Operand1 { get => _operand1; set => SetField(ref _operand1, value ?? ""); }
        public string Operand2 { get => _operand2; set => SetField(ref _operand2, value ?? ""); }

        public bool IsEmpty =>
            string.IsNullOrEmpty(Label) && string.IsNullOrEmpty(Command) &&
            string.IsNullOrEmpty(Operand1) && string.IsNullOrEmpty(Operand2);
    }

    /// <summary>Строка таблицы кодов операций: Команда | МКОП-код | Длина.</summary>
    public class OperCodeRow : ObservableRow
    {
        private string _command = "";
        private string _code = "";
        private string _length = "";

        public string Command { get => _command; set => SetField(ref _command, value ?? ""); }
        public string Code { get => _code; set => SetField(ref _code, value ?? ""); }
        public string Length { get => _length; set => SetField(ref _length, value ?? ""); }
    }

    /// <summary>Строка вспомогательной таблицы: Адрес | МКОП | Операнд1 | Операнд2. Только для чтения.</summary>
    public class SupportTableRow
    {
        public string Address { get; set; } = "";
        public string Command { get; set; } = "";
        public string Operand1 { get; set; } = "";
        public string Operand2 { get; set; } = "";
    }

    /// <summary>Строка таблицы символических имён: Метка | Адрес. Только для чтения.</summary>
    public class SymbolTableRow
    {
        public string Label { get; set; } = "";
        public string Address { get; set; } = "";
    }
}
