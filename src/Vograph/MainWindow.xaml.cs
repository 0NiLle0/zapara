using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vograph.Core.Models;
using Vograph.Core.Services;

namespace Vograph;

// Code comments in English, UI text in Russian per prompt §0.4
public partial class MainWindow : Window
{
    private readonly string _dbPath;
    private Database? _db;
    private ParserService? _parser;
    private ScheduleService? _schedule;
    private string _currentTab = "Tomorrow"; // Today|Tomorrow|Week
    private int _weekParity = 1; // 1 odd, 2 even for week view
    private bool _isLoading = false;

    public MainWindow()
    {
        InitializeComponent();
        // DB in LocalAppData\Vograph\vograph.db
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Vograph");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "vograph.db");
        // Fallback: also check base directory for cached xml for offline demo
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Загрузка...";
        try
        {
            _db = new Database(_dbPath);
            _parser = new ParserService(_db);
            _schedule = new ScheduleService(_db);

            await EnsureDataAsync();

            LoadGroups();
            SelectInitialGroup();
            UpdateParityBadge(DateTime.Today.AddDays(_currentTab == "Tomorrow" ? 1 : 0));
            RenderCurrentView();
            StatusText.Text = "Готово";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ошибка: {ex.Message}";
            // Show stale if possible
            if (_db != null)
            {
                try { LoadGroups(); RenderCurrentView(); StaleBadge.Visibility = Visibility.Visible; } catch { }
            }
        }
    }

    private async Task EnsureDataAsync()
    {
        if (_db == null || _parser == null) return;
        var groups = _db.GetAllGroups();
        var settings = _db.GetSettings();
        bool needFetch = groups.Count == 0;
        if (!needFetch && !string.IsNullOrEmpty(settings.LastFetchedAt))
        {
            if (DateTime.TryParse(settings.LastFetchedAt, out var last))
            {
                // background refresh every 1-3 days is enough
                if ((DateTime.UtcNow - last).TotalDays > 3) needFetch = true;
            }
            else needFetch = true;
        }
        else if (groups.Count > 0 && string.IsNullOrEmpty(settings.LastFetchedAt))
        {
            needFetch = true;
        }

        // Try to use cached xml from docs if offline and no DB
        string fallbackXml = Path.Combine(AppContext.BaseDirectory, "TimetableGroup50.xml");
        // Also check temp location from Phase 0 fetch
        string tempXml = @"C:\Users\NiLle\AppData\Local\Temp\opencode\TimetableGroup50.xml";
        if (needFetch)
        {
            try
            {
                await _parser.RefreshAsync();
                LastUpdatedText.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";
                StatusText.Text = "Расписание обновлено";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Не удалось обновить: {ex.Message}";
                StaleBadge.Visibility = Visibility.Visible;
                // fallback to local file if exists
                string xml = null!;
                if (File.Exists(fallbackXml)) xml = await File.ReadAllTextAsync(fallbackXml);
                else if (File.Exists(tempXml)) xml = await File.ReadAllTextAsync(tempXml);
                else if (File.Exists(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs", "verify_phase1", "raw_TimetableGroup50.xml"))) xml = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "docs", "verify_phase1", "raw_TimetableGroup50.xml"));
                if (xml != null)
                {
                    try { await _parser.RefreshAsync(xmlOverride: xml); } catch { }
                }
            }
        }
        else
        {
            LastUpdatedText.Text = $"Обновлено: {settings.LastFetchedAt ?? "—"}";
        }
    }

    private void LoadGroups()
    {
        if (_db == null) return;
        var groups = _db.GetAllGroups();
        // Sort by name for display, but keep Id as value
        GroupPicker.ItemsSource = groups;
        GroupPicker.DisplayMemberPath = "Name";
        GroupPicker.SelectedValuePath = "Id";

        SettingsGroupPicker.ItemsSource = groups;
        SettingsGroupPicker.DisplayMemberPath = "Name";
        SettingsGroupPicker.SelectedValuePath = "Id";

        var settings = _db.GetSettings();
        ChkInvertParity.IsChecked = settings.ParityInvert;
        ChkInvertParity.Checked += (s, e) => SaveParityInvert();
        ChkInvertParity.Unchecked += (s, e) => SaveParityInvert();

        if (!string.IsNullOrEmpty(settings.MyGroupId))
        {
            GroupPicker.SelectedValue = settings.MyGroupId;
            SettingsGroupPicker.SelectedValue = settings.MyGroupId;
        }
        else if (groups.Count > 0)
        {
            // Try to select 3313 (А863С) as demo, else first
            var demo = groups.FirstOrDefault(g => g.Id == "3313") ?? groups.FirstOrDefault(g => g.Name.Contains("А863")) ?? groups[0];
            GroupPicker.SelectedValue = demo.Id;
            SettingsGroupPicker.SelectedValue = demo.Id;
            // save
            settings.MyGroupId = demo.Id;
            _db.SaveSettings(settings);
        }

        // Update hint
        var sel = GroupPicker.SelectedItem as Group;
        if (sel != null)
        {
            HeaderHint.Text = $"Группа {sel.Name} · {(IsOddWeek(DateTime.Today) ? "нечетная" : "четная")} неделя";
        }
    }

    private void SelectInitialGroup()
    {
        // Default tab is Tomorrow per spec
        _currentTab = "Tomorrow";
        UpdateTabButtons();
    }

    private void SaveParityInvert()
    {
        if (_db == null) return;
        var s = _db.GetSettings();
        s.ParityInvert = ChkInvertParity.IsChecked == true;
        _db.SaveSettings(s);
        RenderCurrentView();
    }

    private void GroupPicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (_db == null) return;
        if (GroupPicker.SelectedValue is string gid)
        {
            var s = _db.GetSettings();
            s.MyGroupId = gid;
            _db.SaveSettings(s);
            SettingsGroupPicker.SelectedValue = gid;
            var g = _db.GetGroup(gid);
            if (g != null) HeaderHint.Text = $"Группа {g.Name} · {(IsOddWeek(DateTime.Today) ? "нечетная" : "четная")} неделя";
            RenderCurrentView();
        }
    }

    private void UpdateTabButtons()
    {
        // Reset styles
        BtnToday.Style = (Style)FindResource("GhostButton");
        BtnTomorrow.Style = (Style)FindResource("GhostButton");
        BtnWeek.Style = (Style)FindResource("GhostButton");
        BtnWeekOdd.Style = (Style)FindResource("GhostButton");
        BtnWeekEven.Style = (Style)FindResource("GhostButton");

        if (_currentTab == "Today") BtnToday.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Tomorrow") BtnTomorrow.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Week") BtnWeek.Style = (Style)FindResource("FerryButton");

        // Week parity buttons
        if (_weekParity == 1) BtnWeekOdd.Style = (Style)FindResource("FerryButton");
        else BtnWeekEven.Style = (Style)FindResource("FerryButton");

        WeekParityPanel.Visibility = _currentTab == "Week" ? Visibility.Visible : Visibility.Collapsed;
        ScheduleScroll.Visibility = _currentTab != "Week" ? Visibility.Visible : Visibility.Collapsed;
        WeekScroll.Visibility = _currentTab == "Week" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TabToday_Click(object sender, RoutedEventArgs e) { _currentTab = "Today"; UpdateTabButtons(); UpdateParityBadge(DateTime.Today); RenderCurrentView(); }
    private void TabTomorrow_Click(object sender, RoutedEventArgs e) { _currentTab = "Tomorrow"; UpdateTabButtons(); UpdateParityBadge(DateTime.Today.AddDays(1)); RenderCurrentView(); }
    private void TabWeek_Click(object sender, RoutedEventArgs e) { _currentTab = "Week"; UpdateTabButtons(); RenderCurrentView(); }
    private void WeekOdd_Click(object sender, RoutedEventArgs e) { _weekParity = 1; UpdateTabButtons(); RenderWeekView(); }
    private void WeekEven_Click(object sender, RoutedEventArgs e) { _weekParity = 2; UpdateTabButtons(); RenderWeekView(); }

    private bool IsOddWeek(DateTime date)
    {
        if (_db == null) return true;
        var s = _db.GetSettings();
        DateTime periodStart = DateTime.TryParse(s.PeriodStart, out var ps) ? ps : new DateTime(DateTime.Now.Year, 9, 1);
        int wc = s.WeekCount > 0 ? s.WeekCount : 2;
        return ParityService.IsOddWeek(date, periodStart, wc, s.ParityInvert);
    }

    private void UpdateParityBadge(DateTime date)
    {
        bool odd = IsOddWeek(date);
        ParityText.Text = odd ? "НЕЧЕТНАЯ" : "ЧЕТНАЯ";
        ParityBadge.Background = odd ? (Brush)FindResource("PanelAlt") : (Brush)FindResource("Panel");
        // Date header
        string[] days = { "Воскресенье", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };
        int dow = (int)date.DayOfWeek;
        string dayName = days[dow];
        DateHeader.Text = $"{date:dd.MM.yyyy} · {dayName}";
        if (_currentTab == "Week")
        {
            DateHeader.Text = _weekParity == 1 ? "Нечетная неделя" : "Четная неделя";
            ParityText.Text = _weekParity == 1 ? "НЕЧЕТНАЯ" : "ЧЕТНАЯ";
        }
    }

    private void RenderCurrentView()
    {
        if (_db == null) return;
        if (GroupPicker.SelectedValue is not string gid) return;
        UpdateParityBadge(_currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today);
        if (_currentTab == "Week") RenderWeekView();
        else RenderDayView();
    }

    private void RenderDayView()
    {
        if (_db == null || _schedule == null) return;
        if (GroupPicker.SelectedValue is not string gid) return;
        SchedulePanel.Children.Clear();
        // Keep EmptyText but remove from panel and re-add later if needed
        EmptyText.Visibility = Visibility.Collapsed;

        DateTime date = _currentTab == "Today" ? DateTime.Today : DateTime.Today.AddDays(1);
        // If user checked OnlyCurrentWeek, filter? But spec says that checkbox toggles filtering in site, we respect parity anyway.
        // For Tomorrow/Today we show only that day's parity
        var lessons = _schedule.GetSchedule(date, gid);
        // If ChkOnlyCurrentWeek is checked? Already filtered via parity, but if unchecked we could show both? However spec says default shows parity-filtered.
        // We'll respect parity regardless.

        if (lessons.Count == 0)
        {
            EmptyText.Text = "Нет занятий";
            EmptyText.Visibility = Visibility.Visible;
            SchedulePanel.Children.Add(EmptyText);
            return;
        }

        // Header columns
        var header = CreateHeaderRow();
        SchedulePanel.Children.Add(header);

        int idx = 1;
        foreach (var l in lessons.OrderBy(x => x.TimeStart))
        {
            var card = CreateLessonCard(l, idx++);
            SchedulePanel.Children.Add(card);
        }
    }

    private void RenderWeekView()
    {
        if (_db == null || _schedule == null) return;
        if (GroupPicker.SelectedValue is not string gid) return;
        WeekGrid.Children.Clear();
        WeekGrid.RowDefinitions.Clear();
        WeekGrid.ColumnDefinitions.Clear();
        // Create 2 rows, 3 columns = 6 days
        for (int c = 0; c < 3; c++) WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 2; r++) WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int dow = 1; dow <= 6; dow++)
        {
            int parity = _weekParity;
            var lessons = _db.GetLessons(gid, dow, parity);
            var dayCard = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(4), Padding = new Thickness(7) };
            var stack = new StackPanel();
            var title = new TextBlock { Text = ParityService.DayNumberToTitle(dow).ToUpper(), Style = (Style)FindResource("SectionLabel"), Margin = new Thickness(0,0,0,6) };
            stack.Children.Add(title);
            if (lessons.Count == 0)
            {
                stack.Children.Add(new TextBlock { Text = "Нет занятий", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10, Margin = new Thickness(0,4,0,0) });
            }
            else
            {
                // Mini header
                var hdr = new Grid { Margin = new Thickness(0,0,0,4) };
                hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
                hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                var th1 = new TextBlock { Text = "Время", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, FontWeight = FontWeights.SemiBold };
                var th2 = new TextBlock { Text = "Предмет", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, FontWeight = FontWeights.SemiBold };
                var th3 = new TextBlock { Text = "Ауд.", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, FontWeight = FontWeights.SemiBold };
                Grid.SetColumn(th1, 0); Grid.SetColumn(th2, 1); Grid.SetColumn(th3, 2);
                hdr.Children.Add(th1); hdr.Children.Add(th2); hdr.Children.Add(th3);
                stack.Children.Add(hdr);

                foreach (var l in lessons.OrderBy(x => x.TimeStart))
                {
                    var row = new Grid { Margin = new Thickness(0,2,0,2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    var t = new TextBlock { Text = l.TimeStart, Foreground = (Brush)FindResource("Marble"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
                    var subj = new TextBlock { Text = string.IsNullOrEmpty(l.SubjectRaw) ? "—" : l.SubjectRaw, Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,0,0,0) };
                    var room = new TextBlock { Text = string.IsNullOrEmpty(l.ClassroomRaw) ? "—" : l.ClassroomRaw, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10 };
                    Grid.SetColumn(t, 0); Grid.SetColumn(subj, 1); Grid.SetColumn(room, 2);
                    row.Children.Add(t); row.Children.Add(subj); row.Children.Add(room);
                    stack.Children.Add(row);
                }
            }
            dayCard.Child = stack;
            int col = (dow - 1) % 3;
            int rowIdx = (dow - 1) / 3;
            Grid.SetColumn(dayCard, col);
            Grid.SetRow(dayCard, rowIdx);
            WeekGrid.Children.Add(dayCard);
        }
    }

    private Border CreateHeaderRow()
    {
        var border = new Border { Background = (Brush)FindResource("PanelAlt"), BorderBrush = (Brush)FindResource("BorderDim"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Margin = new Thickness(0,0,0,6), Padding = new Thickness(7,4,7,4) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // No.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); // Time
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Subject
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); // Teacher
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // Room
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // icon

        var headers = new[] { "№", "Время", "Предмет", "Преподаватель", "Ауд./Корп.", "·" };
        for (int i = 0; i < headers.Length; i++)
        {
            var tb = new TextBlock { Text = headers[i], Foreground = (Brush)FindResource("Bronze"), FontSize = 10, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            if (i == 0) tb.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(tb, i);
            grid.Children.Add(tb);
        }
        border.Child = grid;
        return border;
    }

    private Border CreateLessonCard(Lesson l, int number)
    {
        var border = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0,0,0,6), Padding = new Thickness(7) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

        var tbNo = new TextBlock { Text = number.ToString(), Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var tbTime = new TextBlock { Text = string.IsNullOrEmpty(l.TimeStart) ? "—" : $"{l.TimeStart}\n{l.TimeEnd}", Foreground = (Brush)FindResource("Marble"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var tbSubj = new TextBlock { Text = string.IsNullOrEmpty(l.SubjectRaw) ? "—" : l.SubjectRaw, Foreground = (Brush)FindResource("Marble"), FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0) };
        if (!string.IsNullOrEmpty(l.TypeRaw))
        {
            tbSubj.Text = $"[{l.TypeRaw}] {l.SubjectRaw}";
        }
        var tbTeach = new TextBlock { Text = string.IsNullOrEmpty(l.TeacherRaw) ? "—" : l.TeacherRaw, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var tbRoom = new TextBlock { Text = string.IsNullOrEmpty(l.ClassroomRaw) ? "—" : l.ClassroomRaw, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var tbIcon = new TextBlock { Text = "●", Foreground = (Brush)FindResource("Bronze"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.3 };

        Grid.SetColumn(tbNo, 0); Grid.SetColumn(tbTime, 1); Grid.SetColumn(tbSubj, 2); Grid.SetColumn(tbTeach, 3); Grid.SetColumn(tbRoom, 4); Grid.SetColumn(tbIcon, 5);
        grid.Children.Add(tbNo); grid.Children.Add(tbTime); grid.Children.Add(tbSubj); grid.Children.Add(tbTeach); grid.Children.Add(tbRoom); grid.Children.Add(tbIcon);

        border.Child = grid;
        return border;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_parser == null || _db == null) return;
        StatusText.Text = "Обновление...";
        try
        {
            await _parser.RefreshAsync();
            LoadGroups();
            RenderCurrentView();
            LastUpdatedText.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";
            StatusText.Text = "Готово — расписание обновлено";
            StaleBadge.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ошибка обновления: {ex.Message}";
            StaleBadge.Visibility = Visibility.Visible;
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Экспорт — будет в Фазе 5";
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Импорт — будет в Фазе 5";
    }

    protected override void OnClosed(EventArgs e)
    {
        _db?.Dispose();
        base.OnClosed(e);
    }
}
