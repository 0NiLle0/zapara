using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Vograph.Core.Models;
using Vograph.Core.Services;
using Vograph.Dialogs;

namespace Vograph;

// Code comments in English, UI text in Russian per prompt 0.4
public partial class MainWindow : Window
{
    private readonly string _dbPath;
    private Database? _db;
    private ParserService? _parser;
    private ScheduleService? _schedule;
    private OverrideService? _overrideService;
    private HomeworkService? _homeworkService;
    private IntersectionService? _intersectionService;
    private NotificationService? _notificationService;
    private DispatcherTimer? _notifyTimer;
    private readonly string[] _friendColors = new[] { "#FF6CA5E0", "#FF98C379", "#FFE06C75", "#FFC678DD", "#FFF2C55C" };
    private I18nService? _i18n;
    private AutoRefreshService? _autoRefresh;
    private string _currentTab = "Tomorrow"; // Today|Tomorrow|Week
    private int _weekParity = 1; // 1 odd, 2 even for week view
    private bool _isLoading = false;

    public MainWindow()
    {
        InitializeComponent();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Vograph");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "vograph.db");
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Загрузка...";
        try
        {
            _db = new Database(_dbPath);
            var settings0 = _db.GetSettings();
            _i18n = new I18nService(settings0.Language ?? "ru");
            _i18n.LanguageChanged += ApplyLanguage;
            _parser = new ParserService(_db);
            _schedule = new ScheduleService(_db);
            _overrideService = new OverrideService(_db);
            _homeworkService = new HomeworkService(_db);
            _intersectionService = new IntersectionService(_db);
            _notificationService = new NotificationService(_db, _overrideService, _homeworkService, _schedule, _i18n);
            _autoRefresh = new AutoRefreshService(_db, _parser);
            // Recompute homework statuses on start
            try { _homeworkService.RecomputeAllStatuses(); } catch { }

            await EnsureDataAsync();
            LoadGroups();
            LoadFriendsUI();
            LoadNotificationUI();
            // Language picker sync
            LanguagePicker.SelectionChanged -= LanguagePicker_Changed;
            foreach (ComboBoxItem it in LanguagePicker.Items)
            {
                if ((it.Tag as string) == _i18n.Language) { LanguagePicker.SelectedItem = it; break; }
            }
            LanguagePicker.SelectionChanged += LanguagePicker_Changed;
            ApplyLanguage();
            _autoRefresh.Start();
            StartNotifyTimer();
            SelectInitialGroup();
            UpdateParityBadge(DateTime.Today.AddDays(_currentTab == "Tomorrow" ? 1 : 0));
            RenderCurrentView();
            StatusText.Text = _i18n.T("ready");
            UpdateLastAutoCheckText();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ошибка: {ex.Message}";
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
                if ((DateTime.UtcNow - last).TotalDays > 3) needFetch = true;
            }
            else needFetch = true;
        }
        else if (groups.Count > 0 && string.IsNullOrEmpty(settings.LastFetchedAt))
        {
            needFetch = true;
        }
        string fallbackXml = Path.Combine(AppContext.BaseDirectory, "TimetableGroup50.xml");
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
        GroupPicker.ItemsSource = groups;
        GroupPicker.DisplayMemberPath = "Name";
        GroupPicker.SelectedValuePath = "Id";
        SettingsGroupPicker.ItemsSource = groups;
        SettingsGroupPicker.DisplayMemberPath = "Name";
        SettingsGroupPicker.SelectedValuePath = "Id";
        var settings = _db.GetSettings();
        ChkInvertParity.IsChecked = settings.ParityInvert;
        // avoid duplicate handlers
        ChkInvertParity.Checked -= ChkInvertParity_Checked;
        ChkInvertParity.Unchecked -= ChkInvertParity_Checked;
        ChkInvertParity.Checked += ChkInvertParity_Checked;
        ChkInvertParity.Unchecked += ChkInvertParity_Checked;
        if (!string.IsNullOrEmpty(settings.MyGroupId))
        {
            GroupPicker.SelectedValue = settings.MyGroupId;
            SettingsGroupPicker.SelectedValue = settings.MyGroupId;
        }
        else if (groups.Count > 0)
        {
            var demo = groups.FirstOrDefault(g => g.Id == "3313") ?? groups.FirstOrDefault(g => g.Name.Contains("А863")) ?? groups[0];
            GroupPicker.SelectedValue = demo.Id;
            SettingsGroupPicker.SelectedValue = demo.Id;
            settings.MyGroupId = demo.Id;
            _db.SaveSettings(settings);
        }
        var sel = GroupPicker.SelectedItem as Group;
        if (sel != null)
        {
            HeaderHint.Text = $"Группа {sel.Name} · {(IsOddWeek(DateTime.Today) ? "нечетная" : "четная")} неделя";
        }
    }

    private void ChkInvertParity_Checked(object sender, RoutedEventArgs e) => SaveParityInvert();

    private void SelectInitialGroup()
    {
        _currentTab = "Tomorrow";
        UpdateTabButtons();
    }

    private void SaveParityInvert()
    {
        if (_db == null) return;
        var s = _db.GetSettings();
        s.ParityInvert = ChkInvertParity.IsChecked == true;
        _db.SaveSettings(s);
        try { _homeworkService?.RecomputeAllStatuses(); } catch { }
        RenderCurrentView();
    }

    private void UpdateLastAutoCheckText()
    {
        if (_db == null || _i18n == null) return;
        var s = _db.GetSettings();
        string last = s.LastAutoCheckAt ?? s.LastFetchedAt ?? "—";
        if (DateTime.TryParse(last, out var dt)) last = dt.ToString(_i18n.Language == "ru" ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm");
        if (LastAutoCheckText != null) LastAutoCheckText.Text = _i18n.T("lastAutoCheck", last);
        if (LastUpdatedText != null)
        {
            string upd = s.LastFetchedAt ?? "—";
            if (DateTime.TryParse(upd, out var d2)) upd = d2.ToString(_i18n.Language == "ru" ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm");
            LastUpdatedText.Text = _i18n.T("updated", upd);
        }
    }

    private void ApplyLanguage()
    {
        if (_i18n == null) return;
        // Header
        if (HeaderHint != null && _db != null)
        {
            var g = GroupPicker?.SelectedItem as Group ?? _db.GetGroup(_db.GetSettings().MyGroupId ?? "");
            string grp = g?.Name ?? "—";
            bool odd = IsOddWeek(DateTime.Today);
            string parity = _i18n.FormatParity(odd);
            HeaderHint.Text = _i18n.T("headerHint", grp, parity);
        }
        if (ElevationHint != null) ElevationHint.Text = _i18n.T("headerSub");
        if (TxtSettingsTitle != null) TxtSettingsTitle.Text = _i18n.T("settings");
        if (LblLanguage != null) LblLanguage.Text = _i18n.T("language");
        if (LblMyGroup != null) LblMyGroup.Text = _i18n.T("myGroup");
        if (ChkInvertParity != null) ChkInvertParity.Content = _i18n.T("invertParity");
        if (TxtInvertHint != null) TxtInvertHint.Text = _i18n.T("invertHint");
        if (LblFriendsTitle != null) LblFriendsTitle.Text = _i18n.T("friends");
        if (TxtFriendsHint != null) TxtFriendsHint.Text = _i18n.T("friendsHint");
        if (LblStrictnessTitle != null) LblStrictnessTitle.Text = _i18n.T("strictness");
        if (TxtStrict0 != null) TxtStrict0.Text = _i18n.T("strict0");
        if (TxtStrict40 != null) TxtStrict40.Text = _i18n.T("strict40");
        if (TxtStrict100 != null) TxtStrict100.Text = _i18n.T("strict100");
        if (LblNotificationsTitle != null) LblNotificationsTitle.Text = _i18n.T("notifications");
        if (LblTime1 != null) LblTime1.Text = _i18n.T("time1");
        if (LblTime2 != null) LblTime2.Text = _i18n.T("time2");
        if (BtnSaveTimes != null) BtnSaveTimes.Content = _i18n.T("saveTimes");
        if (TxtNotifHint != null) TxtNotifHint.Text = _i18n.T("notifHint");
        if (LblSyncTitle != null) LblSyncTitle.Text = _i18n.T("sync");
        if (BtnExport != null) BtnExport.Content = _i18n.T("export");
        if (BtnImport != null) BtnImport.Content = _i18n.T("import");
        if (BtnRefreshSettings != null) BtnRefreshSettings.Content = _i18n.T("refresh");
        if (LblGroup != null) LblGroup.Text = _i18n.T("group");
        if (BtnRefresh != null) BtnRefresh.Content = _i18n.T("refresh");
        if (ChkOnlyCurrentWeek != null) ChkOnlyCurrentWeek.Content = _i18n.T("onlyCurrentWeek");
        if (BtnToday != null) BtnToday.Content = _i18n.T("today");
        if (BtnTomorrow != null) BtnTomorrow.Content = _i18n.T("tomorrow");
        if (BtnWeek != null) BtnWeek.Content = _i18n.T("week");
        if (LblWeek != null) LblWeek.Text = _i18n.T("weekLabel");
        if (BtnWeekOdd != null) BtnWeekOdd.Content = _i18n.T("weekOdd");
        if (BtnWeekEven != null) BtnWeekEven.Content = _i18n.T("weekEven");
        if (EmptyText != null) EmptyText.Text = _i18n.T("noLessons");
        if (BtnAddFriend != null) BtnAddFriend.Content = _i18n.T("export").Contains("Export") ? "+ Add" : "+ Добавить"; // fallback
        // Status
        if (StatusText != null && StatusText.Text == "Готово") StatusText.Text = _i18n.T("ready");
        if (StaleBadge != null) StaleBadge.Text = _i18n.T("stale");
        UpdateLastAutoCheckText();
        // Re-render to update dates/parity in current language
        UpdateParityBadge(_currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today);
        RenderCurrentView();
    }

    private void LanguagePicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_db == null || _i18n == null) return;
        if (LanguagePicker.SelectedItem is ComboBoxItem it && it.Tag is string lang)
        {
            if (_i18n.Language == lang) return;
            _i18n.SetLanguage(lang);
            var s = _db.GetSettings();
            s.Language = lang;
            _db.SaveSettings(s);
            // ApplyLanguage will be called via event
        }
    }

    private void LoadFriendsUI()
    {
        if (_db == null) return;
        var groups = _db.GetAllGroups();
        FriendGroupPicker.ItemsSource = groups;
        FriendGroupPicker.DisplayMemberPath = "Name";
        FriendGroupPicker.SelectedValuePath = "Name";
        var settings = _db.GetSettings();
        StrictnessSlider.Value = settings.IntersectionStrictness;
        StrictnessLabel.Text = $"{settings.IntersectionStrictness} — {(settings.IntersectionStrictness == 0 ? "любое время" : settings.IntersectionStrictness == 100 ? "аудитория" : settings.IntersectionStrictness < 40 ? "время" : "корпус")}";
        FriendsListPanel.Children.Clear();
        var friends = _db.GetFriends();
        foreach (var f in friends)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,2,0,2) };
            var dot = new TextBlock { Text = "●", Foreground = (Brush)new BrushConverter().ConvertFromString(f.ColorHex)!, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
            var name = new TextBlock { Text = f.GroupName, Foreground = (Brush)FindResource("Marble"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Width = 120 };
            var btn = new Button { Content = "✕", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(4,2,4,2), FontSize = 10, Margin = new Thickness(6,0,0,0) };
            var fid = f.Id;
            btn.Click += (s, e) => { _db?.DeleteFriend(fid); LoadFriendsUI(); RenderCurrentView(); };
            row.Children.Add(dot); row.Children.Add(name); row.Children.Add(btn);
            FriendsListPanel.Children.Add(row);
        }
        if (friends.Count >= 5)
        {
            FriendGroupPicker.IsEnabled = false;
        }
        else FriendGroupPicker.IsEnabled = true;
    }

    private void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        if (_db == null) return;
        if (FriendGroupPicker.SelectedValue is not string name) { StatusText.Text = "Выберите группу"; return; }
        var existing = _db.GetFriends();
        if (existing.Count >= 5) { StatusText.Text = "Максимум 5 друзей"; return; }
        if (existing.Any(x => x.GroupName == name)) { StatusText.Text = "Уже добавлена"; return; }
        var idx = existing.Count % _friendColors.Length;
        var f = new FriendGroup { GroupName = name, ColorHex = _friendColors[idx], Enabled = true };
        _db.InsertFriend(f);
        LoadFriendsUI();
        RenderCurrentView();
        StatusText.Text = $"Друг {name} добавлен";
    }

    private void Strictness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_db == null || _isLoading) return;
        var val = (int)StrictnessSlider.Value;
        var s = _db.GetSettings();
        s.IntersectionStrictness = val;
        _db.SaveSettings(s);
        StrictnessLabel.Text = $"{val} — {(val == 0 ? "любое время" : val == 100 ? "аудитория" : val < 40 ? "время" : "корпус")}";
        RenderCurrentView();
    }

    private void LoadNotificationUI()
    {
        if (_db == null) return;
        var s = _db.GetSettings();
        NotifyTime1Box.Text = s.NotifyTime1 ?? "20:00";
        NotifyTime2Box.Text = s.NotifyTime2 ?? "07:30";
    }

    private void SaveNotifyTimes_Click(object sender, RoutedEventArgs e)
    {
        if (_db == null) return;
        var s = _db.GetSettings();
        // Validate HH:mm
        bool ok1 = TimeSpan.TryParse(NotifyTime1Box.Text.Trim(), out _);
        bool ok2 = TimeSpan.TryParse(NotifyTime2Box.Text.Trim(), out _);
        if (!ok1 || !ok2) { StatusText.Text = "Неверный формат времени HH:mm"; return; }
        s.NotifyTime1 = NotifyTime1Box.Text.Trim();
        s.NotifyTime2 = NotifyTime2Box.Text.Trim();
        _db.SaveSettings(s);
        StatusText.Text = $"Уведомления сохранены {s.NotifyTime1} и {s.NotifyTime2}";
        StartNotifyTimer();
    }

    private void StartNotifyTimer()
    {
        _notifyTimer?.Stop();
        _notifyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _notifyTimer.Tick += (s, e) =>
        {
            if (_db == null || _notificationService == null) return;
            var now = DateTime.Now;
            var settings = _db.GetSettings();
            if (_notificationService.ShouldFire(now, settings.NotifyTime1, settings.NotifyTime2))
            {
                // Avoid duplicate firing within same minute: log check
                _notificationService.LogAndShow(now);
                StatusText.Text = $"Уведомление показано в {now:HH:mm}";
            }
        };
        _notifyTimer.Start();
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
            try { _homeworkService?.RecomputeAllStatuses(); } catch { }
            RenderCurrentView();
        }
    }

    private void UpdateTabButtons()
    {
        BtnToday.Style = (Style)FindResource("GhostButton");
        BtnTomorrow.Style = (Style)FindResource("GhostButton");
        BtnWeek.Style = (Style)FindResource("GhostButton");
        BtnWeekOdd.Style = (Style)FindResource("GhostButton");
        BtnWeekEven.Style = (Style)FindResource("GhostButton");
        if (_currentTab == "Today") BtnToday.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Tomorrow") BtnTomorrow.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Week") BtnWeek.Style = (Style)FindResource("FerryButton");
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
        if (_i18n == null) return;
        bool odd = IsOddWeek(date);
        ParityText.Text = _i18n.FormatParityBadge(odd);
        ParityBadge.Background = odd ? (Brush)FindResource("PanelAlt") : (Brush)FindResource("Panel");
        string dayName = _i18n.FormatDayFull(date);
        DateHeader.Text = $"{_i18n.FormatDate(date)} · {dayName}";
        if (_currentTab == "Week")
        {
            DateHeader.Text = _weekParity == 1 ? _i18n.T("weekOdd") + " " + _i18n.T("week").ToLower() : _i18n.T("weekEven") + " " + _i18n.T("week").ToLower();
            ParityText.Text = _i18n.FormatParityBadge(_weekParity == 1);
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
        // recompute statuses each render
        try { _homeworkService?.RecomputeAllStatuses(); } catch { }
        SchedulePanel.Children.Clear();
        EmptyText.Visibility = Visibility.Collapsed;
        DateTime date = _currentTab == "Today" ? DateTime.Today : DateTime.Today.AddDays(1);
        var lessons = _schedule.GetSchedule(date, gid);
        if (lessons.Count == 0)
        {
            EmptyText.Text = _i18n?.T("noLessons") ?? "Нет занятий";
            EmptyText.Visibility = Visibility.Visible;
            SchedulePanel.Children.Add(EmptyText);
            return;
        }
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
        try { _homeworkService?.RecomputeAllStatuses(); } catch { }
        WeekGrid.Children.Clear();
        WeekGrid.RowDefinitions.Clear();
        WeekGrid.ColumnDefinitions.Clear();
        for (int c = 0; c < 3; c++) WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 2; r++) WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int dow = 1; dow <= 6; dow++)
        {
            int parity = _weekParity;
            var lessons = _db.GetLessons(gid, dow, parity);
            var dayCard = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(4), Padding = new Thickness(7) };
            var stack = new StackPanel();
            string dayKey = dow switch { 1=>"mon",2=>"tue",3=>"wed",4=>"thu",5=>"fri",6=>"sat",_=>"mon"};
            string dayTitle = _i18n != null ? _i18n.T(dayKey).ToUpper() : ParityService.DayNumberToTitle(dow).ToUpper();
            var title = new TextBlock { Text = dayTitle, Style = (Style)FindResource("SectionLabel"), Margin = new Thickness(0,0,0,6) };
            stack.Children.Add(title);
            if (lessons.Count == 0)
            {
                string noLessons = _i18n?.T("noLessons") ?? "Нет занятий";
                stack.Children.Add(new TextBlock { Text = noLessons, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10, Margin = new Thickness(0,4,0,0) });
            }
            else
            {
                var hdr = new Grid { Margin = new Thickness(0,0,0,4) };
                hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
                hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                var th1 = new TextBlock { Text = _i18n?.T("colTime") ?? "Время", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, FontWeight = FontWeights.SemiBold };
                var th2 = new TextBlock { Text = _i18n?.T("colSubject") ?? "Предмет", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, FontWeight = FontWeights.SemiBold };
                var th3 = new TextBlock { Text = _i18n?.T("colRoom") ?? "Ауд.", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, FontWeight = FontWeights.SemiBold };
                Grid.SetColumn(th1, 0); Grid.SetColumn(th2, 1); Grid.SetColumn(th3, 2);
                hdr.Children.Add(th1); hdr.Children.Add(th2); hdr.Children.Add(th3);
                stack.Children.Add(hdr);
                foreach (var l in lessons.OrderBy(x => x.TimeStart))
                {
                    var row = new Grid { Margin = new Thickness(0,2,0,2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    // Use override display name here too
                    string subj = _overrideService?.GetDisplayName(l.SubjectRaw, dow) ?? l.SubjectRaw;
                    var t = new TextBlock { Text = l.TimeStart, Foreground = (Brush)FindResource("Marble"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
                    var subjTb = new TextBlock { Text = string.IsNullOrEmpty(subj) ? "—" : subj, Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,0,0,0) };
                    var room = new TextBlock { Text = string.IsNullOrEmpty(l.ClassroomRaw) ? "—" : l.ClassroomRaw, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10 };
                    Grid.SetColumn(t, 0); Grid.SetColumn(subjTb, 1); Grid.SetColumn(room, 2);
                    row.Children.Add(t); row.Children.Add(subjTb); row.Children.Add(room);
                    stack.Children.Add(row);
                    // homework mini for week view
                    var hwList = _homeworkService?.GetForSubject(l.SubjectRaw) ?? new List<Homework>();
                    foreach (var hw in hwList.Where(h => h.Status != "done" && h.Status != "far").Take(1))
                    {
                        var hwTb = new TextBlock { Text = $"ДЗ: {hw.Text} ({hw.Status})", Foreground = GetHomeworkBrush(hw.Status), FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,2,0,0) };
                        stack.Children.Add(hwTb);
                    }
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        string[] headers = _i18n != null ? new[] { _i18n.T("colNo"), _i18n.T("colTime"), _i18n.T("colSubject"), _i18n.T("colTeacher"), _i18n.T("colRoom"), "·" } : new[] { "№", "Время", "Предмет", "Преподаватель", "Ауд./Корп.", "·" };
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
        // Outer card
        var outer = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0,0,0,6), Padding = new Thickness(7) };
        var outerStack = new StackPanel();

        // Top grid with lesson info
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // actions

        // Display name via override
        string displayName = _overrideService?.GetDisplayName(l.SubjectRaw, l.DayOfWeek) ?? l.SubjectRaw;
        bool isRenamed = displayName != l.SubjectRaw;
        string note = _overrideService?.GetNote(l.SubjectRaw, l.DayOfWeek) ?? "";

        var tbNo = new TextBlock { Text = number.ToString(), Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var tbTime = new TextBlock { Text = string.IsNullOrEmpty(l.TimeStart) ? "—" : $"{l.TimeStart}\n{l.TimeEnd}", Foreground = (Brush)FindResource("Marble"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var tbSubjStack = new StackPanel();
        var tbSubj = new TextBlock { Text = string.IsNullOrEmpty(displayName) ? "—" : displayName, Foreground = (Brush)FindResource("Marble"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,0,0,0) };
        if (!string.IsNullOrEmpty(l.TypeRaw))
        {
            tbSubj.Text = $"[{l.TypeRaw}] {displayName}";
        }
        if (isRenamed)
        {
            tbSubj.FontWeight = FontWeights.SemiBold;
        }
        tbSubjStack.Children.Add(tbSubj);
        if (isRenamed)
        {
            var orig = new TextBlock { Text = l.SubjectRaw, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,0,0,0) };
            tbSubjStack.Children.Add(orig);
        }
        if (!string.IsNullOrEmpty(note))
        {
            var noteTb = new TextBlock { Text = note, Foreground = (Brush)FindResource("Bronze"), FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,2,0,0), FontStyle = FontStyles.Italic };
            tbSubjStack.Children.Add(noteTb);
        }

        var tbTeach = new TextBlock { Text = string.IsNullOrEmpty(l.TeacherRaw) ? "—" : l.TeacherRaw, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var tbRoom = new TextBlock { Text = string.IsNullOrEmpty(l.ClassroomRaw) ? "—" : l.ClassroomRaw, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        // Intersection icons: colored dots per friend meeting threshold
        var iconPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        try
        {
            var friends = _db?.GetFriends() ?? new List<FriendGroup>();
            var settings = _db?.GetSettings();
            int strict = settings?.IntersectionStrictness ?? 50;
            DateTime iconDate = _currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today; // for week view handled separately
            // For day view we have exact date, for week view we need dow-based; but here we use today/tomorrow date; intersection will be based on that date's parity
            var inters = _intersectionService?.GetIntersections(l, iconDate, friends, strict) ?? new List<IntersectionService.IntersectionResult>();
            if (inters.Count == 0)
            {
                var tbIcon = new TextBlock { Text = "·", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.3 };
                iconPanel.Children.Add(tbIcon);
            }
            else
            {
                foreach (var inter in inters.Take(5))
                {
                    var dot = new TextBlock { Text = "●", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,1,0,1), ToolTip = $"{inter.FriendGroupName} — {inter.Teacher} {inter.Room} ({inter.Score})" };
                    try { dot.Foreground = (Brush)new BrushConverter().ConvertFromString(inter.FriendColor)!; } catch { dot.Foreground = (Brush)FindResource("Bronze"); }
                    iconPanel.Children.Add(dot);
                }
            }
        }
        catch
        {
            var tbIconFallback = new TextBlock { Text = "●", Foreground = (Brush)FindResource("Bronze"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.15 };
            iconPanel.Children.Add(tbIconFallback);
        }

        // Action buttons
        var actionPanel = new StackPanel { Orientation = Orientation.Vertical };
        var btnRename = new Button { Content = "✎", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(4,2,4,2), FontSize = 10, Margin = new Thickness(0,0,0,2), ToolTip = "Переименовать" };
        btnRename.Click += (s, e) => OpenRenameDialog(l);
        var btnHw = new Button { Content = "+", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(4,2,4,2), FontSize = 10, ToolTip = "ДЗ" };
        btnHw.Click += (s, e) => OpenHomeworkDialog(l, null);
        actionPanel.Children.Add(btnRename);
        actionPanel.Children.Add(btnHw);

        Grid.SetColumn(tbNo, 0); Grid.SetColumn(tbTime, 1); Grid.SetColumn(tbSubjStack, 2); Grid.SetColumn(tbTeach, 3); Grid.SetColumn(tbRoom, 4); Grid.SetColumn(iconPanel, 5); Grid.SetColumn(actionPanel, 6);
        grid.Children.Add(tbNo); grid.Children.Add(tbTime); grid.Children.Add(tbSubjStack); grid.Children.Add(tbTeach); grid.Children.Add(tbRoom); grid.Children.Add(iconPanel); grid.Children.Add(actionPanel);

        // Context menu for right-click
        var cm = new ContextMenu();
        var miRename = new MenuItem { Header = "Переименовать" };
        miRename.Click += (s, e) => OpenRenameDialog(l);
        var miReset = new MenuItem { Header = "Сбросить к оригиналу" };
        miReset.Click += (s, e) => { var ovs = _db!.GetOverrides().Where(o => o.SubjectRawNormalized == ParityService.NormalizeSubject(l.SubjectRaw)).ToList(); foreach (var ov in ovs) _overrideService!.Remove(ov.Id); RenderCurrentView(); };
        var miHw = new MenuItem { Header = "Добавить ДЗ" };
        miHw.Click += (s, e) => OpenHomeworkDialog(l, null);
        cm.Items.Add(miRename); cm.Items.Add(miReset); cm.Items.Add(miHw);
        outer.ContextMenu = cm;
        // Also handle mouse right click to open rename? Use outer.MouseRightButtonUp

        outerStack.Children.Add(grid);

        // Homework block under row
        var homeworks = _homeworkService?.GetForSubject(l.SubjectRaw) ?? new List<Homework>();
        // Sort: burning first, then approaching, far hidden? Spec: far hidden or dot, approaching gray, burning bold, done at bottom
        var visibleHw = homeworks.Where(h => h.Status != "far" || false).OrderBy(h => h.Status == "done" ? 1 : 0).ThenBy(h => h.DueDateComputed).ToList();
        // Actually far hidden: don't show if >1 lesson before due? For MVP, hide far, show dot? We'll hide far and only show approaching/burning/overdue/done
        visibleHw = homeworks.Where(h => h.Status != "far").OrderBy(h => GetHomeworkOrder(h.Status)).ToList();
        // If user wants to see all, we could show dot indicator but for now hide far

        foreach (var hw in visibleHw)
        {
            var hwBorder = new Border
            {
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6,3,6,3),
                Margin = new Thickness(0,6,0,0)
            };
            // Style per status
            Brush fg = (Brush)FindResource("Marble");
            Brush bg = (Brush)FindResource("PanelAlt");
            Brush borderBrush = (Brush)FindResource("BorderDim");
            double opacity = 1;
            FontWeight fw = FontWeights.Normal;
            string icon = "●";
            switch (hw.Status)
            {
                case "approaching":
                    fg = (Brush)FindResource("MarbleDim");
                    bg = Brushes.Transparent;
                    borderBrush = (Brush)FindResource("BorderDim");
                    break;
                case "burning":
                    fg = (Brush)FindResource("Marble");
                    fw = FontWeights.Bold;
                    bg = (Brush)FindResource("PanelAlt");
                    borderBrush = (Brush)FindResource("BorderDim");
                    break;
                case "burning_urgent":
                    fg = (Brush)FindResource("Bronze");
                    fw = FontWeights.Bold;
                    bg = (Brush)FindResource("PanelAlt");
                    borderBrush = (Brush)FindResource("Bronze");
                    icon = "🔥";
                    break;
                case "done":
                    fg = (Brush)FindResource("MarbleDim");
                    opacity = 0.5;
                    bg = Brushes.Transparent;
                    borderBrush = (Brush)FindResource("BorderDim");
                    break;
                case "overdue":
                    fg = (Brush)FindResource("Cinnabar");
                    fw = FontWeights.SemiBold;
                    bg = (Brush)FindResource("PanelAlt");
                    borderBrush = (Brush)FindResource("Cinnabar");
                    icon = "⚠";
                    break;
                default:
                    fg = (Brush)FindResource("MarbleDim");
                    break;
            }
            hwBorder.Background = bg;
            hwBorder.BorderBrush = borderBrush;
            hwBorder.Opacity = opacity;

            var hwGrid = new Grid();
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var iconTb = new TextBlock { Text = icon, Foreground = fg, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontWeight = fw };
            var textTb = new TextBlock { Text = hw.Text, Foreground = fg, FontSize = 11, TextWrapping = TextWrapping.Wrap, FontWeight = fw };
            if (hw.Status == "done") textTb.TextDecorations = TextDecorations.Strikethrough;
            var dueTb = new TextBlock { Text = hw.DueDateComputed?.ToString("dd.MM") ?? "—", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            if (hw.Status == "burning" || hw.Status == "burning_urgent") dueTb.Text = $"срок {hw.DueDateComputed:dd.MM}";

            Grid.SetColumn(iconTb, 0); Grid.SetColumn(textTb, 1); Grid.SetColumn(dueTb, 2);
            hwGrid.Children.Add(iconTb); hwGrid.Children.Add(textTb); hwGrid.Children.Add(dueTb);

            hwBorder.Child = hwGrid;
            hwBorder.Cursor = Cursors.Hand;
            hwBorder.ToolTip = $"ДЗ: {hw.Text} · статус {hw.Status} · через {hw.TargetNthOccurrence} занятий";
            // Click to mark done / edit
            hwBorder.MouseLeftButtonUp += (s, e) => OpenHomeworkActionDialog(hw, l);
            outerStack.Children.Add(hwBorder);
        }

        outer.Child = outerStack;
        return outer;
    }

    private int GetHomeworkOrder(string status) => status switch
    {
        "burning_urgent" => 0,
        "burning" => 1,
        "overdue" => 2,
        "approaching" => 3,
        "far" => 4,
        "done" => 5,
        _ => 6
    };

    private Brush GetHomeworkBrush(string status) => status switch
    {
        "approaching" => (Brush)FindResource("MarbleDim"),
        "burning" => (Brush)FindResource("Marble"),
        "burning_urgent" => (Brush)FindResource("Bronze"),
        "overdue" => (Brush)FindResource("Cinnabar"),
        "done" => (Brush)FindResource("MarbleDim"),
        _ => (Brush)FindResource("MarbleDim")
    };

    private void OpenRenameDialog(Lesson l)
    {
        if (_db == null || _overrideService == null) return;
        string norm = ParityService.NormalizeSubject(l.SubjectRaw);
        var existingGlobal = _db.GetOverrides().FirstOrDefault(o => o.SubjectRawNormalized == norm && o.Scope == "global");
        var existingWeekday = _db.GetOverrides().FirstOrDefault(o => o.SubjectRawNormalized == norm && o.Scope == $"weekday:{l.DayOfWeek}");
        // Prefer global if exists
        var existing = existingGlobal ?? existingWeekday;
        string currentDisplay = existing?.DisplayName ?? _overrideService.GetDisplayName(l.SubjectRaw, l.DayOfWeek);
        string currentScope = existing?.Scope ?? "global";
        string? note = existing?.Note;

        var dlg = new RenameDialog(l.SubjectRaw, l.DayOfWeek, currentDisplay, note, currentScope, _i18n);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            _overrideService.AddOrUpdate(l.SubjectRaw, dlg.ScopeResult, dlg.DisplayNameResult, dlg.NoteResult);
            RenderCurrentView();
        }
    }

    private void OpenHomeworkDialog(Lesson l, Homework? existing)
    {
        if (_db == null || _homeworkService == null) return;
        string subject = l.SubjectRaw;
        Homework? hw = existing;
        var dlg = new HomeworkDialog(subject, hw?.Text, hw?.TargetNthOccurrence ?? 1, (n) =>
        {
            var tmp = new Homework { SubjectRawNormalized = ParityService.NormalizeSubject(subject), CreatedAt = DateTime.Today, TargetNthOccurrence = n };
            var due = _homeworkService.ComputeDueDate(tmp.SubjectRawNormalized, tmp.CreatedAt, n);
            if (_i18n != null)
            {
                return due != null ? _i18n.T("hwDue", due.Value.ToString(_i18n.Language=="ru"?"dd.MM.yyyy":"yyyy-MM-dd") + " (" + _i18n.FormatDayFull(due.Value) + ")") : _i18n.T("hwNoDate");
            }
            return due != null ? $"Срок: {due:dd.MM.yyyy} ({due:dddd})" : "Срок: — (нет занятий)";
        }, _i18n);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            if (hw == null)
            {
                _homeworkService.AddHomework(subject, dlg.TextResult, dlg.NResult, DateTime.Today);
            }
            else
            {
                _homeworkService.UpdateHomework(hw.Id, dlg.TextResult, dlg.NResult);
            }
            RenderCurrentView();
        }
    }

    private void OpenHomeworkActionDialog(Homework hw, Lesson l)
    {
        var menu = new ContextMenu();
        var miDone = new MenuItem { Header = hw.Status == "done" ? "Снять отметку" : "Отметить выполненным" };
        miDone.Click += (s, e) => { _homeworkService?.MarkDone(hw.Id, hw.Status != "done"); RenderCurrentView(); };
        var miEdit = new MenuItem { Header = "Редактировать" };
        miEdit.Click += (s, e) => OpenHomeworkDialog(l, hw);
        var miDel = new MenuItem { Header = "Удалить" };
        miDel.Click += (s, e) => { _homeworkService?.Delete(hw.Id); RenderCurrentView(); };
        menu.Items.Add(miDone); menu.Items.Add(miEdit); menu.Items.Add(miDel);
        menu.IsOpen = true;
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
        if (_db == null) return;
        try
        {
            var svc = new SyncService(_db);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"vograph-sync-{DateTime.Now:yyyyMMdd}.json",
                DefaultExt = ".json",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                svc.ExportToFile(dlg.FileName);
                // Generate QR
                try
                {
                    var json = svc.ExportToJson();
                    var qrContent = svc.GenerateQrContent(json);
                    var qrPath = Path.Combine(Path.GetDirectoryName(dlg.FileName) ?? ".", Path.GetFileNameWithoutExtension(dlg.FileName) + ".qr.png");
                    svc.SaveQrImage(qrContent, qrPath);
                    StatusText.Text = $"Экспорт сохранен {Path.GetFileName(dlg.FileName)} + QR {Path.GetFileName(qrPath)}";
                    // Also try to show QR in message
                    MessageBox.Show($"Экспорт завершен:\n{dlg.FileName}\nQR: {qrPath}\nВерсия 1, overrides {svc.ExportToJson().Length} chars", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { StatusText.Text = $"Экспорт OK, QR ошибка: {ex.Message}"; }
            }
        }
        catch (Exception ex) { StatusText.Text = $"Ошибка экспорта: {ex.Message}"; }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_db == null) return;
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", DefaultExt = ".json" };
            if (dlg.ShowDialog() == true)
            {
                var svc = new SyncService(_db);
                var (o, h, f) = svc.ImportFromJson(File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8));
                LoadGroups();
                LoadFriendsUI();
                LoadNotificationUI();
                RenderCurrentView();
                StatusText.Text = $"Импорт: {o} переименований, {h} ДЗ, {f} друзей";
                MessageBox.Show($"Импорт завершен:\nПереименований: {o}\nДЗ: {h}\nДрузей: {f}", "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) { StatusText.Text = $"Ошибка импорта: {ex.Message}"; MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoRefresh?.Dispose();
        _notifyTimer?.Stop();
        _db?.Dispose();
        base.OnClosed(e);
    }
}
