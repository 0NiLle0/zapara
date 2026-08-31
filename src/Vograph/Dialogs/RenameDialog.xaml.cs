using System.Windows;

namespace Vograph.Dialogs;

public partial class RenameDialog : Window
{
    public string DisplayNameResult { get; private set; } = "";
    public string? NoteResult { get; private set; }
    public string ScopeResult { get; private set; } = "global";
    private readonly string _subjectRaw;
    private readonly int _dayOfWeek;

    public RenameDialog(string subjectRaw, int dayOfWeek, string currentDisplay, string? currentNote, string currentScope)
    {
        InitializeComponent();
        _subjectRaw = subjectRaw;
        _dayOfWeek = dayOfWeek;
        OriginalText.Text = $"Оригинал: {subjectRaw}";
        TxtDisplayName.Text = currentDisplay;
        TxtNote.Text = currentNote ?? "";
        if (currentScope.StartsWith("weekday")) RbWeekday.IsChecked = true;
        else RbGlobal.IsChecked = true;
        PreviewText.Text = $"Предпросмотр: {currentDisplay}";
        TxtDisplayName.TextChanged += (s, e) => PreviewText.Text = $"Предпросмотр: {TxtDisplayName.Text}";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DisplayNameResult = TxtDisplayName.Text.Trim();
        if (string.IsNullOrWhiteSpace(DisplayNameResult))
        {
            MessageBox.Show("Введите название", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        NoteResult = string.IsNullOrWhiteSpace(TxtNote.Text) ? null : TxtNote.Text.Trim();
        ScopeResult = RbGlobal.IsChecked == true ? "global" : $"weekday:{_dayOfWeek}";
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        TxtDisplayName.Text = _subjectRaw;
        TxtNote.Text = "";
        RbGlobal.IsChecked = true;
    }
}
