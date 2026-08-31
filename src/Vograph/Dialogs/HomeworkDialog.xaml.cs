using System.Windows;
using Vograph.Core.Services;

namespace Vograph.Dialogs;

public partial class HomeworkDialog : Window
{
    public string TextResult { get; private set; } = "";
    public int NResult { get; private set; } = 1;
    private readonly I18nService? _i18n;

    public HomeworkDialog(string subjectRaw, string? existingText, int existingN, Func<int, string> duePreviewFunc, I18nService? i18n = null)
    {
        InitializeComponent();
        _i18n = i18n;
        if (_i18n != null) ApplyI18n();
        SubjectText.Text = _i18n != null ? _i18n.T("hwSubject", subjectRaw) : $"Предмет: {subjectRaw}";
        TxtHomework.Text = existingText ?? "";
        TxtN.Text = existingN.ToString();
        void UpdatePreview()
        {
            if (int.TryParse(TxtN.Text, out var n))
            {
                n = Math.Clamp(n, 1, 10);
                DuePreview.Text = duePreviewFunc(n);
            }
        }
        TxtN.TextChanged += (s, e) => UpdatePreview();
        UpdatePreview();
    }

    private void ApplyI18n()
    {
        if (_i18n == null) return;
        Title = _i18n.T("hwTitle");
        if (FindName("LblTitle") is System.Windows.Controls.TextBlock t1) t1.Text = _i18n.T("hwTitle");
        if (FindName("LblHwText") is System.Windows.Controls.TextBlock t2) t2.Text = _i18n.T("hwText");
        if (FindName("LblHwN") is System.Windows.Controls.TextBlock t3) t3.Text = _i18n.T("hwN");
        if (FindName("LblHwHint") is System.Windows.Controls.TextBlock t4) t4.Text = _i18n.T("hwStatusHint");
        if (FindName("BtnCancel") is System.Windows.Controls.Button b1) b1.Content = _i18n.T("cancel");
        if (FindName("BtnSave") is System.Windows.Controls.Button b2) b2.Content = _i18n.T("save");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        TextResult = TxtHomework.Text.Trim();
        if (string.IsNullOrWhiteSpace(TextResult))
        {
            MessageBox.Show("Введите текст", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(TxtN.Text, out var n)) n = 1;
        NResult = Math.Clamp(n, 1, 10);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
