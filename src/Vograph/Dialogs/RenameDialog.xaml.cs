using System.Windows;
using Vograph.Core.Services;

namespace Vograph.Dialogs;

public partial class RenameDialog : Window
{
    public string DisplayNameResult { get; private set; } = "";
    public string? NoteResult { get; private set; }
    public string ScopeResult { get; private set; } = "global";
    private readonly string _subjectRaw;
    private readonly int _dayOfWeek;
    private readonly I18nService? _i18n;

    public RenameDialog(string subjectRaw, int dayOfWeek, string currentDisplay, string? currentNote, string currentScope, I18nService? i18n = null)
    {
        InitializeComponent();
        _subjectRaw = subjectRaw;
        _dayOfWeek = dayOfWeek;
        _i18n = i18n;
        // localize static labels if i18n provided
        if (_i18n != null) ApplyI18n();
        OriginalText.Text = _i18n != null ? _i18n.T("original", subjectRaw) : $"Оригинал: {subjectRaw}";
        TxtDisplayName.Text = currentDisplay;
        TxtNote.Text = currentNote ?? "";
        if (currentScope.StartsWith("weekday")) RbWeekday.IsChecked = true;
        else RbGlobal.IsChecked = true;
        PreviewText.Text = _i18n != null ? _i18n.T("preview", currentDisplay) : $"Предпросмотр: {currentDisplay}";
        TxtDisplayName.TextChanged += (s, e) => PreviewText.Text = _i18n != null ? _i18n.T("preview", TxtDisplayName.Text) : $"Предпросмотр: {TxtDisplayName.Text}";
    }

    private void ApplyI18n()
    {
        if (_i18n == null) return;
        // Window title
        Title = _i18n.T("renameTitle");
        // Need to find controls by name and set - we have x:Name but XAML hardcoded, we overwrite
        // This is called after InitializeComponent, XAML already loaded with ru defaults
        // We update via FindName
        if (FindName("LblTitle") is System.Windows.Controls.TextBlock tb1) tb1.Text = _i18n.T("renameTitle");
        if (FindName("LblNewName") is System.Windows.Controls.TextBlock tb2) tb2.Text = _i18n.T("newName");
        if (FindName("LblFootnote") is System.Windows.Controls.TextBlock tb3) tb3.Text = _i18n.T("footnote");
        if (FindName("LblScope") is System.Windows.Controls.TextBlock tb4) tb4.Text = _i18n.T("scope");
        if (RbGlobal != null) RbGlobal.Content = _i18n.T("global");
        if (RbWeekday != null) RbWeekday.Content = _i18n.T("weekdayOnly");
        // Buttons: find by style? We can just search children
        // For simplicity, keep XAML buttons content as is and update via code if needed
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
