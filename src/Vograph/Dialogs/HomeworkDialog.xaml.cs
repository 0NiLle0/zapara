using System.Windows;

namespace Vograph.Dialogs;

public partial class HomeworkDialog : Window
{
    public string TextResult { get; private set; } = "";
    public int NResult { get; private set; } = 1;

    public HomeworkDialog(string subjectRaw, string? existingText, int existingN, Func<int, string> duePreviewFunc)
    {
        InitializeComponent();
        SubjectText.Text = $"Предмет: {subjectRaw}";
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
