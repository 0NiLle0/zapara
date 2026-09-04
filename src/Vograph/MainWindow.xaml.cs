using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vograph.Core.Models;
using Vograph.Core.Services;
using Vograph.Dialogs;
using Vograph.Helpers;

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
    private MapService? _mapService;
    private MapInfo? _currentMap;
    private double _mapZoom = 1.0;
    private Point _mapPanStart;
    private Vector _mapPanOrigin;
    private bool _isMapPanning = false;
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
        SourceInitialized += (s, e) => DarkModeHelper.EnableDarkTitleBar(this);
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
            _mapService = new MapService(_db, _schedule);
            _autoRefresh = new AutoRefreshService(_db, _parser);
            // Recompute homework statuses on start
            try { _homeworkService.RecomputeAllStatuses(); } catch { }

            await EnsureDataAsync();
            LoadGroups();
            LoadFriendsUI();
            LoadNotificationUI();
            InitMapUI();
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
            // silent self-update from git on startup (non-blocking, opt-out via settings)
            _ = AutoUpdateFlowAsync(manual: false);
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
        GroupPicker.SelectedValuePath = "Id";
        SettingsGroupPicker.ItemsSource = groups;
        SettingsGroupPicker.SelectedValuePath = "Id";
        // searchable dropdowns: editable text filters items (group name)
        SearchableComboBox.Enable(GroupPicker, o => (o as Group)?.Name ?? o?.ToString() ?? "");
        SearchableComboBox.Enable(SettingsGroupPicker, o => (o as Group)?.Name ?? o?.ToString() ?? "");
        var settings = _db.GetSettings();
        ChkInvertParity.IsChecked = settings.ParityInvert;
        // avoid duplicate handlers
        ChkInvertParity.Checked -= ChkInvertParity_Checked;
        ChkInvertParity.Unchecked -= ChkInvertParity_Checked;
        ChkInvertParity.Checked += ChkInvertParity_Checked;
        ChkInvertParity.Unchecked += ChkInvertParity_Checked;
        ChkAutoUpdate.Checked -= ChkAutoUpdate_Changed;
        ChkAutoUpdate.Unchecked -= ChkAutoUpdate_Changed;
        ChkAutoUpdate.IsChecked = settings.AutoUpdate;
        ChkAutoUpdate.Checked += ChkAutoUpdate_Changed;
        ChkAutoUpdate.Unchecked += ChkAutoUpdate_Changed;
        if (!string.IsNullOrEmpty(settings.MyGroupId))
        {
            GroupPicker.SelectedValue = settings.MyGroupId;
            SettingsGroupPicker.SelectedValue = settings.MyGroupId;
        }
        else if (groups.Count > 0)
        {
            // Do not hardcode group 3313 — let user pick from dropdown, select first as placeholder
            var first = groups.OrderBy(g => g.Name).First();
            GroupPicker.SelectedValue = first.Id;
            SettingsGroupPicker.SelectedValue = first.Id;
            settings.MyGroupId = first.Id;
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
        // Smart initial tab: if today's pairs haven't passed yet -> Today, else Tomorrow
        try
        {
            if (_db != null && _schedule != null && GroupPicker.SelectedValue is string gid)
            {
                var today = DateTime.Today;
                var lessonsToday = _schedule.GetSchedule(today, gid);
                if (lessonsToday.Count > 0)
                {
                    var last = lessonsToday.OrderBy(l => l.TimeEnd).LastOrDefault();
                    if (last != null && TimeSpan.TryParse(last.TimeEnd, out var end))
                    {
                        var lastEnd = today.Add(end);
                        if (DateTime.Now < lastEnd.AddMinutes(15)) // 15 min grace after last pair
                        {
                            _currentTab = "Today";
                            UpdateTabButtons();
                            UpdateParityBadge(today);
                            return;
                        }
                    }
                }
            }
        }
        catch {}
        _currentTab = "Tomorrow";
        UpdateTabButtons();
        try { UpdateParityBadge(DateTime.Today.AddDays(1)); } catch {}
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
        if (ChkAutoUpdate != null) ChkAutoUpdate.Content = _i18n.T("autoUpdate");
        if (TxtInvertHint != null) TxtInvertHint.Text = _i18n.T("invertHint");
        if (LblFriendsTitle != null) LblFriendsTitle.Text = _i18n.T("friends");
        if (TxtFriendsHint != null) TxtFriendsHint.Text = _i18n.T("friendsHint");
        if (LblStrictnessTitle != null) LblStrictnessTitle.Text = _i18n.T("strictness");
        if (TxtStrict0 != null) TxtStrict0.Text = _i18n.T("strict0");
        if (TxtStrict40 != null) TxtStrict40.Text = _i18n.T("strict40");
        if (TxtStrict100 != null) TxtStrict100.Text = _i18n.T("strict100");
        if (LblBlockWidthTitle != null) LblBlockWidthTitle.Text = _i18n.T("blockWidth");
        if (TxtBlockWidthHint != null) TxtBlockWidthHint.Text = _i18n.T("blockWidthHint");
        if (BtnBlockWidthReset != null) BtnBlockWidthReset.Content = _i18n.T("blockWidthReset");
        if (BtnBlockWidthWide != null) BtnBlockWidthWide.Content = _i18n.T("blockWidthWide");
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
        if (BtnYesterday != null) BtnYesterday.Content = _i18n.T("yesterday");
        if (BtnToday != null) BtnToday.Content = _i18n.T("today");
        if (BtnTomorrow != null) BtnTomorrow.Content = _i18n.T("tomorrow");
        if (BtnWeek != null) BtnWeek.Content = _i18n.T("week");
        if (LblWeek != null) LblWeek.Text = _i18n.T("weekLabel");
        if (BtnWeekOdd != null) BtnWeekOdd.Content = _i18n.T("weekOdd");
        if (BtnWeekEven != null) BtnWeekEven.Content = _i18n.T("weekEven");
        if (EmptyText != null) EmptyText.Text = _i18n.T("noLessons");
        if (BtnAddFriend != null) BtnAddFriend.Content = _i18n.T("export").Contains("Export") ? "+ Add" : "+ Добавить"; // fallback
        // Map
        if (LblMapTitle != null) LblMapTitle.Text = _i18n.T("mapTitle");
        if (MapNextBadge != null && _currentMap != null) { /* keep building/floor */ } else if (MapNextBadge != null) MapNextBadge.Text = _i18n.T("mapNext");
        if (LblMapAll != null) LblMapAll.Text = _i18n.T("mapAll");
        if (TxtMapHint != null) TxtMapHint.Text = _i18n.T("mapHint");
        if (BtnMapOpen != null) BtnMapOpen.Content = _i18n.T("mapOpen");
        if (BtnMapSite != null) BtnMapSite.Content = _i18n.T("mapOpenSite");
        if (BtnMapDownload != null) BtnMapDownload.Content = _i18n.T("mapDownload");
        if (MapCacheText != null) MapCacheText.Text = _i18n.T("mapCacheDir", MapService.GetMapsCacheDir());
        // Status
        if (StatusText != null && StatusText.Text == "Готово") StatusText.Text = _i18n.T("ready");
        if (StaleBadge != null) StaleBadge.Text = _i18n.T("stale");
        UpdateLastAutoCheckText();
        // Re-render to update dates/parity in current language
        UpdateParityBadge(_currentTab == "Yesterday" ? DateTime.Today.AddDays(-1) : _currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today);
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
        FriendGroupPicker.SelectedValuePath = "Name";
        SearchableComboBox.Enable(FriendGroupPicker, o => (o as Group)?.Name ?? o?.ToString() ?? "");
        var settings = _db.GetSettings();
        // Strictness UI is collapsed, but keep logic for traffic light filtering (default 25)
        try
        {
            if (StrictnessSlider != null)
            {
                StrictnessSlider.ValueChanged -= Strictness_Changed;
                StrictnessSlider.Value = settings.IntersectionStrictness;
                StrictnessSlider.ValueChanged += Strictness_Changed;
            }
            if (StrictnessLabel != null) StrictnessLabel.Text = $"{settings.IntersectionStrictness} — {(settings.IntersectionStrictness == 0 ? "любое время" : settings.IntersectionStrictness == 100 ? "аудитория" : settings.IntersectionStrictness < 40 ? "время" : "корпус")}";
            // Always show toggle
            if (ChkAlwaysShowTraffic != null)
            {
                ChkAlwaysShowTraffic.Checked -= ChkAlwaysShowTraffic_Changed;
                ChkAlwaysShowTraffic.Unchecked -= ChkAlwaysShowTraffic_Changed;
                ChkAlwaysShowTraffic.IsChecked = settings.AlwaysShowAllTrafficLights;
                ChkAlwaysShowTraffic.Checked += ChkAlwaysShowTraffic_Changed;
                ChkAlwaysShowTraffic.Unchecked += ChkAlwaysShowTraffic_Changed;
            }
        }
        catch {}
        FriendsListPanel.Children.Clear();
        var friends = _db.GetFriends();
        foreach (var f in friends)
        {
            var row = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0,2,0,2), Padding = new Thickness(6,4,6,4), Background = (Brush)FindResource("PanelAlt") };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            var dot = new TextBlock { Text = "●", Foreground = (Brush)new BrushConverter().ConvertFromString(f.ColorHex)!, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(dot, 0);
            var name = new TextBlock { Text = f.GroupName, Foreground = (Brush)FindResource("Marble"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 1);
            var tbNames = new TextBox { Text = f.MemberNames ?? "", Background = (Brush)FindResource("Panel"), Foreground = (Brush)FindResource("Marble"), BorderBrush = (Brush)FindResource("BorderDim"), CaretBrush = (Brush)FindResource("Bronze"), FontSize = 10, Padding = new Thickness(4,2,4,2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,0,0,0), ToolTip = "Имена товарищей в этой группе (через запятую)" };
            Grid.SetColumn(tbNames, 2);
            tbNames.LostFocus += (s, e) =>
            {
                var newVal = tbNames.Text ?? "";
                if (newVal != f.MemberNames)
                {
                    f.MemberNames = newVal;
                    _db?.UpdateFriend(f);
                    RenderCurrentView();
                }
            };
            tbNames.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    var newVal2 = tbNames.Text ?? "";
                    f.MemberNames = newVal2;
                    _db?.UpdateFriend(f);
                    RenderCurrentView();
                    // move focus
                    FocusManager.SetFocusedElement(this, null);
                    Keyboard.ClearFocus();
                }
            };
            var btn = new Button { Content = "✕", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(4,2,4,2), FontSize = 10, Margin = new Thickness(4,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(btn, 3);
            var fid = f.Id;
            btn.Click += (s, e) => { _db?.DeleteFriend(fid); LoadFriendsUI(); RenderCurrentView(); };
            grid.Children.Add(dot); grid.Children.Add(name); grid.Children.Add(tbNames); grid.Children.Add(btn);
            row.Child = grid;
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

    private void ChkAlwaysShowTraffic_Changed(object sender, RoutedEventArgs e)
    {
        if (_db == null || _isLoading) return;
        var s = _db.GetSettings();
        s.AlwaysShowAllTrafficLights = ChkAlwaysShowTraffic.IsChecked == true;
        _db.SaveSettings(s);
        RenderCurrentView();
        StatusText.Text = s.AlwaysShowAllTrafficLights ? "Светофоры: всегда все (пустые погасшие)" : "Светофоры: только непустые";
    }

    private void LoadNotificationUI()
    {
        if (_db == null) return;
        var s = _db.GetSettings();
        NotifyTime1Box.Text = s.NotifyTime1 ?? "20:00";
        NotifyTime2Box.Text = s.NotifyTime2 ?? "07:30";
    }

    private void InitMapUI()
    {
        if (_mapService == null) return;
        try
        {
            MapPicker.SelectionChanged -= MapPicker_Changed;
            MapPicker.ItemsSource = _mapService.GetAllMaps();
            MapPicker.DisplayMemberPath = "Title";
            MapPicker.SelectedValuePath = "Url";
            SearchableComboBox.Enable(MapPicker, o => (o as MapInfo)?.Title ?? o?.ToString() ?? "");
            // set default hint
            MapCacheText.Text = _i18n != null ? _i18n.T("mapCacheDir", MapService.GetMapsCacheDir()) : $"Кэш: {MapService.GetMapsCacheDir()}";
            MapPicker.SelectionChanged += MapPicker_Changed;
        }
        catch { }
        UpdateOfflineStatus();
        LoadBlockWidthUI();
        // initial update after groups loaded
        UpdateMapForNextLesson();
        // Ensure offline maps are available without network: copy bundled -> cache in background (even if offline ready via bundled, ensure local cache for speed)
        Task.Run(async () =>
        {
            try
            {
                var all = _mapService.GetAllMaps();
                bool anyMissing = all.Any(m => !File.Exists(m.LocalPath) || new FileInfo(m.LocalPath).Length < 1000);
                if (!anyMissing) { await Dispatcher.InvokeAsync(() => UpdateOfflineStatus()); return; }
                await Dispatcher.InvokeAsync(() =>
                {
                    MapOfflineStatus.Text = "Офлайн: подготовка из пакета...";
                    MapOfflineStatus.Foreground = (Brush)FindResource("Bronze");
                    MapDownloadProgress.Visibility = Visibility.Visible;
                    MapDownloadProgress.IsIndeterminate = true;
                });
                await _mapService.EnsureAllMapsCachedAsync(null, new Progress<string>(s => Dispatcher.Invoke(() => StatusText.Text = s)), preferBundledFirst: true);
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateOfflineStatus();
                    MapDownloadProgress.Visibility = Visibility.Collapsed;
                    MapDownloadProgress.IsIndeterminate = false;
                    // refresh current map if was missing
                    if (_currentMap != null) _ = ShowMapImageAsync(_currentMap);
                });
            }
            catch { await Dispatcher.InvokeAsync(() => { MapDownloadProgress.Visibility = Visibility.Collapsed; UpdateOfflineStatus(); }); }
        });
    }

    private void UpdateOfflineStatus()
    {
        if (_mapService == null) return;
        try
        {
            var (cached, total, ready, status) = _mapService.GetCacheStatus();
            if (MapOfflineStatus != null)
            {
                MapOfflineStatus.Text = status;
                MapOfflineStatus.Foreground = ready ? (Brush)FindResource("Patina") : (Brush)FindResource("Cinnabar");
            }
            if (MapCacheText != null)
            {
                string dir = MapService.GetMapsCacheDir();
                MapCacheText.Text = _i18n != null ? _i18n.T("mapCacheDir", dir) : $"Кэш: {dir}";
                MapCacheText.ToolTip = dir;
            }
            if (MapDownloadProgress != null)
            {
                MapDownloadProgress.Maximum = total;
                MapDownloadProgress.Value = cached;
                MapDownloadProgress.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        catch {}
    }

    private void ApplyMapPanelWidth(int width)
    {
        if (width < 240) width = 240;
        if (width > 620) width = 620;
        try
        {
            if (MapColumn != null) MapColumn.Width = new GridLength(width);
            if (BlockWidthSlider != null)
            {
                BlockWidthSlider.ValueChanged -= BlockWidth_Changed;
                BlockWidthSlider.Value = width;
                BlockWidthSlider.ValueChanged += BlockWidth_Changed;
            }
            if (BlockWidthLabel != null) BlockWidthLabel.Text = width.ToString();
        }
        catch {}
    }

    private void LoadBlockWidthUI()
    {
        if (_db == null) return;
        try
        {
            var s = _db.GetSettings();
            ApplyMapPanelWidth(s.MapPanelWidth);
            // Map hidden by default per user request — significantly smaller initial size already (320 vs 380, MinHeight 180)
            SetMapVisible(false, save:false);
        }
        catch {}
    }

    private Window? _mapFullscreenWindow;
    private void MapFullscreen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_mapFullscreenWindow != null && _mapFullscreenWindow.IsVisible)
            {
                _mapFullscreenWindow.Close();
                _mapFullscreenWindow = null;
                return;
            }
            // Create fullscreen window with current map
            var win = new Window
            {
                Title = _currentMap != null ? _currentMap.Title : "Карта — на весь экран",
                Background = (Brush)FindResource("Obsidian"),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowState = WindowState.Maximized,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Topmost = false
            };
            win.PreviewKeyDown += (s, ev) => { if (ev.Key == System.Windows.Input.Key.Escape) { win.Close(); } };
            var outer = new Grid { Background = (Brush)FindResource("Obsidian") };
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12,8,12,8) };
            var title = new TextBlock { Text = _currentMap != null ? _currentMap.Title : "Карта", Foreground = (Brush)FindResource("Marble"), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            var hint = new TextBlock { Text = "Esc — закрыть", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,0,0) };
            var btnClose = new Button { Content = "✕ Закрыть", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(10,4,10,4), Margin = new Thickness(12,0,0,0) };
            btnClose.Click += (s2, e2) => win.Close();
            header.Children.Add(title); header.Children.Add(hint); header.Children.Add(btnClose);
            Grid.SetRow(header, 0);
            outer.Children.Add(header);
            // Clone map viewer
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, PanningMode = PanningMode.Both };
            var img = new System.Windows.Controls.Image { Source = MapImage.Source, Stretch = System.Windows.Media.Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.Fant);
            // Copy transform
            if (MapScale != null)
            {
                var tr = new ScaleTransform(MapScale.ScaleX, MapScale.ScaleY);
                img.LayoutTransform = tr;
            }
            // Add highlight if visible
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(img);
            if (MapHighlight.Visibility == Visibility.Visible)
            {
                // Clone highlight position
                var hl = new Border { BorderBrush = (Brush)FindResource("Bronze"), BorderThickness = new Thickness(2.5), CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(40,108,165,224)), Width = MapHighlight.Width, Height = MapHighlight.Height, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Canvas.GetLeft(MapHighlight), Canvas.GetTop(MapHighlight), 0, 0) };
                var lbl = new TextBlock { Text = MapHighlightLabel.Text, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6,3,6,3) };
                hl.Child = lbl;
                grid.Children.Add(hl);
            }
            scroll.Content = grid;
            // Zoom with wheel
            scroll.PreviewMouseWheel += (s2, e2) =>
            {
                double delta = e2.Delta > 0 ? 0.1 : -0.1;
                var tr = img.LayoutTransform as ScaleTransform;
                if (tr == null) tr = new ScaleTransform(1,1);
                double nz = Math.Clamp(tr.ScaleX + delta, 0.4, 4.0);
                tr.ScaleX = nz; tr.ScaleY = nz;
                img.LayoutTransform = tr;
                e2.Handled = true;
            };
            Grid.SetRow(scroll, 1);
            outer.Children.Add(scroll);
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,8,0,12) };
            var btnZoomOut = new Button { Content = "−", Style = (Style)FindResource("GhostButton"), Width = 32, Height = 28, Margin = new Thickness(4,0,4,0) };
            var btnZoomIn = new Button { Content = "+", Style = (Style)FindResource("GhostButton"), Width = 32, Height = 28, Margin = new Thickness(4,0,4,0) };
            var btnClose2 = new Button { Content = "Закрыть", Style = (Style)FindResource("FerryButton"), Padding = new Thickness(12,4,12,4), Margin = new Thickness(12,0,0,0) };
            btnZoomOut.Click += (s2, e2) => { var tr = img.LayoutTransform as ScaleTransform ?? new ScaleTransform(1,1); double nz = Math.Clamp(tr.ScaleX - 0.2, 0.4, 4.0); tr.ScaleX = nz; tr.ScaleY = nz; img.LayoutTransform = tr; };
            btnZoomIn.Click += (s2, e2) => { var tr = img.LayoutTransform as ScaleTransform ?? new ScaleTransform(1,1); double nz = Math.Clamp(tr.ScaleX + 0.2, 0.4, 4.0); tr.ScaleX = nz; tr.ScaleY = nz; img.LayoutTransform = tr; };
            btnClose2.Click += (s2, e2) => win.Close();
            footer.Children.Add(btnZoomOut); footer.Children.Add(btnZoomIn); footer.Children.Add(btnClose2);
            Grid.SetRow(footer, 2);
            outer.Children.Add(footer);
            win.Content = outer;
            win.Show();
            _mapFullscreenWindow = win;
            win.Closed += (s2, e2) => _mapFullscreenWindow = null;
            StatusText.Text = "Карта на весь экран — Esc для выхода, колесо для зума";
        }
        catch (Exception ex) { StatusText.Text = $"Ошибка разворота: {ex.Message}"; }
    }

    private bool _isMapVisible = false;
    private void SetMapVisible(bool visible, bool save = false)
    {
        _isMapVisible = visible;
        try
        {
            if (MapPanel != null) MapPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (MapSplitter != null) MapSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (MapColumn != null)
            {
                if (visible)
                {
                    // restore width from settings (or 300 reduced)
                    var s = _db?.GetSettings();
                    int w = s?.MapPanelWidth ?? 300;
                    MapColumn.Width = new GridLength(w);
                }
                else
                {
                    MapColumn.Width = new GridLength(0);
                }
            }
            if (SplitterColumn != null)
            {
                SplitterColumn.Width = visible ? new GridLength(6) : new GridLength(0);
            }
            if (BtnMapToggle != null)
            {
                BtnMapToggle.Style = visible ? (Style)FindResource("FerryButton") : (Style)FindResource("GhostButton");
                BtnMapToggle.Content = visible ? "Скрыть карту" : "Карта";
            }
            if (save && _db != null)
            {
                // optional: persist visibility if needed (not in DB yet, just UI)
            }
        }
        catch {}
    }

    private void ToggleMap_Click(object sender, RoutedEventArgs e)
    {
        SetMapVisible(!_isMapVisible);
        if (_isMapVisible) StatusText.Text = _i18n != null && _i18n.Language=="en" ? "Map shown — search via picker" : "Карта показана — ищите через выпадающий список";
        else StatusText.Text = _i18n != null && _i18n.Language=="en" ? "Map hidden" : "Карта скрыта";
    }

    private void EnsureMapVisible()
    {
        if (!_isMapVisible) SetMapVisible(true);
    }

    private void BlockWidth_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_db == null || _isLoading) return;
        int w = (int)e.NewValue;
        ApplyMapPanelWidth(w);
        try
        {
            var s = _db.GetSettings();
            s.MapPanelWidth = w;
            _db.SaveSettings(s);
            StatusText.Text = $"Ширина карты {w}px";
        }
        catch {}
    }

    private void BlockWidthReset_Click(object sender, RoutedEventArgs e)
    {
        ApplyMapPanelWidth(300);
        if (_db != null) { var s = _db.GetSettings(); s.MapPanelWidth = 300; _db.SaveSettings(s); }
        StatusText.Text = "Ширина сброшена 300";
    }

    private void BlockWidthWide_Click(object sender, RoutedEventArgs e)
    {
        // Make map take ~60% of window width
        try
        {
            double winW = ActualWidth;
            int target = winW > 100 ? (int)(winW * 0.52) : 520;
            if (target < 300) target = 520;
            if (target > 620) target = 620;
            ApplyMapPanelWidth(target);
            if (_db != null) { var s = _db.GetSettings(); s.MapPanelWidth = target; _db.SaveSettings(s); }
            StatusText.Text = $"Ширина карты {target} (широкая)";
        }
        catch {}
    }

    private void MapSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_db == null) return;
        try
        {
            // MapColumn.ActualWidth is current width after drag
            int w = (int)MapColumn.ActualWidth;
            if (w < 240) w = 240;
            if (w > 620) w = 620;
            // Clamp column width if user dragged too far
            MapColumn.Width = new GridLength(w);
            ApplyMapPanelWidth(w);
            var s = _db.GetSettings();
            s.MapPanelWidth = w;
            _db.SaveSettings(s);
            StatusText.Text = $"Ширина карты {w}px (перетаскивание)";
        }
        catch {}
    }

    private void UpdateMapForNextLesson()
    {
        if (_db == null || _mapService == null) return;
        if (GroupPicker.SelectedValue is not string gid) return;
        try
        {
            var now = DateTime.Now;
            var (lesson, date) = _mapService.GetNextLesson(gid, now);
            if (lesson == null)
            {
                UpdateMapDisplay(null);
                return;
            }
            var info = _mapService.GetMapForLesson(lesson);
            UpdateMapDisplay(info, date, lesson);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Map error: {ex.Message}";
        }
    }

    private async void UpdateMapDisplay(MapInfo? info, DateTime? whenDate = null, Lesson? lesson = null)
    {
        if (_i18n == null || _mapService == null) return;
        _currentMap = info;
        if (info == null)
        {
            MapWhereText.Text = _i18n.T("mapNoNext");
            MapWhenText.Text = "";
            MapNoteText.Visibility = Visibility.Collapsed;
            MapPlaceholder.Visibility = Visibility.Visible;
            MapPlaceholder.Text = _i18n.T("mapNoNext");
            MapImage.Source = null;
            MapNextBadge.Text = _i18n.T("mapNoNext");
            return;
        }
        if (info.IsRemote)
        {
            MapWhereText.Text = info.Title;
            MapWhenText.Text = whenDate != null ? _i18n.T("mapWhen", _i18n.FormatDate(whenDate.Value) + " " + whenDate.Value.ToString("HH:mm") + (lesson != null ? $" · {lesson.TimeStart}-{lesson.TimeEnd} · {lesson.SubjectRaw}" : "")) : "";
            MapNoteText.Text = _i18n.T("mapRemote");
            MapNoteText.Visibility = Visibility.Visible;
            MapPlaceholder.Visibility = Visibility.Visible;
            MapPlaceholder.Text = _i18n.T("mapRemote");
            MapImage.Source = null;
            MapNextBadge.Text = info.Building;
            return;
        }
        if (!info.HasMap)
        {
            MapWhereText.Text = _i18n.T("mapWhere", info.Title);
            MapWhenText.Text = whenDate != null && lesson != null ? _i18n.T("mapWhen", _i18n.FormatDate(whenDate.Value) + " " + lesson.TimeStart + " · " + lesson.SubjectRaw) : "";
            MapNoteText.Text = string.IsNullOrEmpty(info.Note) ? _i18n.T("mapNoRoom") : info.Note;
            MapNoteText.Visibility = Visibility.Visible;
            MapPlaceholder.Visibility = Visibility.Visible;
            MapPlaceholder.Text = _i18n.T("mapNoRoom");
            MapImage.Source = null;
            MapNextBadge.Text = $"{info.Building} {info.Floor} этаж";
            return;
        }
        // has map
        string where = _i18n.T("mapWhere", info.Title);
        MapWhereText.Text = where;
        if (whenDate != null && lesson != null)
        {
            string dayName = _i18n.FormatDayFull(whenDate.Value);
            string when = $"{dayName} {whenDate:dd.MM} {lesson.TimeStart}-{lesson.TimeEnd} · {(_overrideService?.GetDisplayName(lesson.SubjectRaw, lesson.DayOfWeek) ?? lesson.SubjectRaw)}";
            MapWhenText.Text = _i18n.T("mapWhen", when);
        }
        else
        {
            MapWhenText.Text = $"{info.RoomRaw} · {info.ClassroomRaw}";
        }
        if (!string.IsNullOrEmpty(info.Note))
        {
            MapNoteText.Text = info.Note;
            MapNoteText.Visibility = Visibility.Visible;
        }
        else MapNoteText.Visibility = Visibility.Collapsed;
        MapNextBadge.Text = $"{info.Building} {info.Floor} этаж";

        // update picker selection without triggering loop
        try
        {
            MapPicker.SelectionChanged -= MapPicker_Changed;
            MapPicker.SelectedValue = info.Url;
            MapPicker.SelectionChanged += MapPicker_Changed;
        }
        catch { }

        // load image (prefer local cache)
        await ShowMapImageAsync(info);
    }

    private async Task ShowMapImageAsync(MapInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.Url)) return;
        MapPlaceholder.Visibility = Visibility.Collapsed;
        MapLoadingOverlay.Visibility = Visibility.Visible;
        MapImage.Source = null;
        try
        {
            string? path = null;
            // try local first
            if (File.Exists(info.LocalPath) && new FileInfo(info.LocalPath).Length > 1000)
            {
                path = info.LocalPath;
            }
            else
            {
                // download async
                path = await _mapService!.EnsureCachedAsync(info);
            }
            if (path != null && File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                MapImage.Source = bmp;
                MapPlaceholder.Visibility = Visibility.Collapsed;
                MapCacheText.Text = _i18n != null ? _i18n.T("mapCacheDir", MapService.GetMapsCacheDir()) : $"Кэш: {MapService.GetMapsCacheDir()}";
                try { MapImage.SizeChanged -= MapImage_SizeChanged; } catch {}
                MapImage.UpdateLayout();
                Dispatcher.BeginInvoke(new Action(() => PositionMapHighlight(info)), System.Windows.Threading.DispatcherPriority.Loaded);
                try { MapImage.SizeChanged += MapImage_SizeChanged; } catch {}
            }
            else
            {
                // fallback to remote url directly
                var bmp2 = new BitmapImage();
                bmp2.BeginInit();
                bmp2.UriSource = new Uri(info.Url);
                bmp2.CacheOption = BitmapCacheOption.OnLoad;
                bmp2.EndInit();
                MapImage.Source = bmp2;
                MapPlaceholder.Visibility = Visibility.Collapsed;
                try { MapImage.SizeChanged -= MapImage_SizeChanged; } catch {}
                MapImage.UpdateLayout();
                Dispatcher.BeginInvoke(new Action(() => PositionMapHighlight(info)), System.Windows.Threading.DispatcherPriority.Loaded);
                try { MapImage.SizeChanged += MapImage_SizeChanged; } catch {}
            }
        }
        catch (Exception ex)
        {
            MapPlaceholder.Visibility = Visibility.Visible;
            MapPlaceholder.Text = $"Не удалось загрузить карту: {ex.Message}\n{info.Url}";
            MapImage.Source = null;
        }
        finally
        {
            MapLoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void MapPicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_mapService == null || _i18n == null) return;
        if (MapPicker.SelectedItem is MapInfo mi)
        {
            _currentMap = mi;
            MapWhereText.Text = _i18n.T("mapWhere", mi.Title);
            MapWhenText.Text = _i18n.T("mapHint");
            MapNoteText.Visibility = Visibility.Collapsed;
            MapNextBadge.Text = $"{mi.Building} {mi.Floor} этаж";
            await ShowMapImageAsync(mi);
        }
    }

    private void MapOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMap == null || string.IsNullOrEmpty(_currentMap.Url)) { StatusText.Text = "Карта не выбрана"; return; }
        try
        {
            string path = _currentMap.LocalPath;
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                StatusText.Text = $"Открыта карта {Path.GetFileName(path)}";
            }
            else
            {
                Process.Start(new ProcessStartInfo(_currentMap.Url) { UseShellExecute = true });
                StatusText.Text = $"Открыта карта {_currentMap.Url}";
            }
        }
        catch (Exception ex) { StatusText.Text = $"Не удалось открыть карту: {ex.Message}"; MessageBox.Show(ex.Message, "Карта", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void MapSite_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://voenmeh.ru/openmap/") { UseShellExecute = true }); } catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private async void MapDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_mapService == null) return;
        BtnMapDownload.IsEnabled = false;
        string orig = BtnMapDownload.Content?.ToString() ?? "Скачать для офлайна";
        BtnMapDownload.Content = _i18n?.T("mapDownloading") ?? "Загрузка...";
        MapLoadingOverlay.Visibility = Visibility.Visible;
        MapDownloadProgress.Visibility = Visibility.Visible;
        MapDownloadProgress.IsIndeterminate = false;
        MapDownloadProgress.Value = 0;
        var (c0, total, _, _) = _mapService.GetCacheStatus();
        MapDownloadProgress.Maximum = total;
        MapDownloadProgress.Value = c0;
        int done = c0;
        try
        {
            var progress = new Progress<string>(s =>
            {
                StatusText.Text = s;
                if (s.StartsWith("Cached") || s.StartsWith("Готово") || s.StartsWith("Копирование") || s.Contains("Готово"))
                {
                    done = Math.Min(total, done + 1);
                    MapDownloadProgress.Value = done;
                }
                // also update offline status text live
                if (s.Contains("/")) UpdateOfflineStatus();
            });
            await _mapService.EnsureAllMapsCachedAsync(null, progress, preferBundledFirst: true);
            StatusText.Text = _i18n != null ? _i18n.T("updatedOk") : "Карты готовы для офлайна";
            // refresh current display
            if (_currentMap != null) await ShowMapImageAsync(_currentMap);
            UpdateOfflineStatus();
            MapDownloadProgress.Value = total;
            MessageBox.Show($"Карты готовы: {total}/{total} — теперь работают без интернета.\nКэш: {MapService.GetMapsCacheDir()}\nВстроенные карты из пакета скопированы и/или докачаны.", "Офлайн карты", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { StatusText.Text = $"Ошибка загрузки карт: {ex.Message}"; MessageBox.Show($"Ошибка: {ex.Message}", "Карты", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally
        {
            BtnMapDownload.IsEnabled = true;
            BtnMapDownload.Content = orig;
            MapLoadingOverlay.Visibility = Visibility.Collapsed;
            UpdateOfflineStatus();
            // hide progress after 2s if ready
            if (_mapService.GetCacheStatus().ready)
            {
                await Task.Delay(1200);
                MapDownloadProgress.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void OpenMapsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = MapService.GetMapsCacheDir();
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            StatusText.Text = $"Открыта папка {dir}";
        }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(ex.Message, "Папка карт", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void VerifyOffline_Click(object sender, RoutedEventArgs e)
    {
        if (_mapService == null) return;
        var (cached, total, ready, status) = _mapService.GetCacheStatus();
        UpdateOfflineStatus();
        string detail = string.Join("\n", _mapService.GetAllMaps().Select(m =>
        {
            bool localOk = File.Exists(m.LocalPath) && new FileInfo(m.LocalPath).Length > 1000;
            string bundled = MapService.GetBundledPathForUrl(m.Url) ?? "";
            bool bundledOk = !string.IsNullOrEmpty(bundled) && File.Exists(bundled);
            string src = localOk ? "кэш" : bundledOk ? "пакет" : "нет";
            return $"{m.Title}: {src} {(localOk ? new FileInfo(m.LocalPath).Length + "b" : bundledOk ? new FileInfo(bundled).Length + "b" : "—")}";
        }));
        string msg = $"{status}\n\nКэш: {MapService.GetMapsCacheDir()}\nПакет: {Path.Combine(AppContext.BaseDirectory, "maps")}\n\nДетали:\n{detail}\n\n" + (ready ? "Офлайн готов — можно без интернета." : "Нажмите «Скачать для офлайна» чтобы докачать.");
        MessageBox.Show(msg, "Офлайн карты", MessageBoxButton.OK, ready ? MessageBoxImage.Information : MessageBoxImage.Warning);
        StatusText.Text = status;
    }

    // --- Map zoom & highlight ---
    private void SetMapZoom(double zoom)
    {
        _mapZoom = Math.Clamp(zoom, 0.4, 3.0);
        if (MapScale != null) { MapScale.ScaleX = _mapZoom; MapScale.ScaleY = _mapZoom; }
        if (ZoomSlider != null)
        {
            ZoomSlider.ValueChanged -= ZoomSlider_Changed;
            ZoomSlider.Value = _mapZoom;
            ZoomSlider.ValueChanged += ZoomSlider_Changed;
        }
        if (ZoomLabel != null) ZoomLabel.Text = $"{(int)(_mapZoom * 100)}%";
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetMapZoom(_mapZoom + 0.2);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetMapZoom(_mapZoom - 0.2);
    private void ZoomReset_Click(object sender, RoutedEventArgs e) => SetMapZoom(1.0);
    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        // Fit: scale to viewer size
        if (MapImage.Source == null || MapScrollViewer == null) { SetMapZoom(1.0); return; }
        try
        {
            double viewW = MapScrollViewer.ViewportWidth;
            double viewH = MapScrollViewer.ViewportHeight;
            if (viewW < 10 || viewH < 10) { SetMapZoom(1.0); return; }
            var bmp = MapImage.Source as BitmapSource;
            if (bmp == null) { SetMapZoom(1.0); return; }
            double scaleW = viewW / bmp.PixelWidth;
            double scaleH = viewH / bmp.PixelHeight;
            double fit = Math.Min(scaleW, scaleH) * 0.95;
            // clamp and use
            if (double.IsNaN(fit) || double.IsInfinity(fit)) fit = 1.0;
            SetMapZoom(Math.Clamp(fit, 0.4, 1.2));
            // center after fit
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { MapScrollViewer.ScrollToHorizontalOffset((MapScrollViewer.ExtentWidth - viewW)/2); MapScrollViewer.ScrollToVerticalOffset((MapScrollViewer.ExtentHeight - viewH)/2); } catch {}
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch { SetMapZoom(1.0); }
    }

    private void ZoomSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        SetMapZoom(e.NewValue);
    }

    private void MapScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || true) // always zoom with wheel on map
        {
            double delta = e.Delta > 0 ? 0.12 : -0.12;
            SetMapZoom(_mapZoom + delta);
            e.Handled = true;
        }
    }

    private void MapScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            // start pan if zoomed
            _isMapPanning = true;
            _mapPanStart = e.GetPosition(MapScrollViewer);
            _mapPanOrigin = new Vector(MapScrollViewer.HorizontalOffset, MapScrollViewer.VerticalOffset);
            MapScrollViewer.CaptureMouse();
            MapImage.Cursor = Cursors.Hand;
            if (e.ClickCount == 2) { ZoomReset_Click(sender, e); }
            e.Handled = true;
        }
    }

    private void MapScrollViewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isMapPanning && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(MapScrollViewer);
            var dx = _mapPanStart.X - pos.X;
            var dy = _mapPanStart.Y - pos.Y;
            try
            {
                MapScrollViewer.ScrollToHorizontalOffset(_mapPanOrigin.X + dx);
                MapScrollViewer.ScrollToVerticalOffset(_mapPanOrigin.Y + dy);
            }
            catch {}
        }
    }

    private void MapScrollViewer_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMapPanning)
        {
            _isMapPanning = false;
            MapScrollViewer.ReleaseMouseCapture();
            MapImage.Cursor = Cursors.Hand;
        }
    }

    private void MapImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ZoomReset_Click(sender, e); e.Handled = true; }
    }

    private void MapImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_currentMap != null) Dispatcher.BeginInvoke(new Action(() => PositionMapHighlight(_currentMap)), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void PositionMapHighlight(MapInfo info)
    {
        if (info == null || !info.HasMap || info.IsRemote) { try { MapHighlight.Visibility = Visibility.Collapsed; MapPulse.Visibility = Visibility.Collapsed; } catch {} return; }
        try
        {
            // Try precise coords from coords.json
            var cr = _mapService?.GetCoords(info.Building, info.Floor, info.RoomRaw);
            double imgW = MapImage.ActualWidth;
            double imgH = MapImage.ActualHeight;
            if (imgW < 10 || imgH < 10)
            {
                if (MapImage.Source is BitmapSource bmp) { imgW = bmp.PixelWidth; imgH = bmp.PixelHeight; }
                else { imgW = 800; imgH = 600; }
            }
            double x, y, w, h;
            if (cr != null)
            {
                x = cr.x; y = cr.y; w = cr.w; h = cr.h;
            }
            else
            {
                // Fallback heuristic (as before) — centered corridor segment
                string room = info.RoomRaw ?? "";
                var m = System.Text.RegularExpressions.Regex.Match(room, @"\d+");
                double relX = 0.5, relY = 0.5;
                if (m.Success)
                {
                    var digits = m.Value;
                    if (digits.Length >= 2)
                    {
                        int last = digits[digits.Length - 1] - '0';
                        int prev = digits[digits.Length - 2] - '0';
                        relX = 0.15 + (last * 0.07);
                        relY = 0.30 + (prev * 0.06);
                        relX = Math.Clamp(relX, 0.1, 0.9);
                        relY = Math.Clamp(relY, 0.15, 0.85);
                    }
                }
                w = Math.Max(140, imgW * 0.36) / imgW;
                h = Math.Max(48, imgH * 0.16) / imgH;
                x = Math.Clamp(relX - w/2, 0, 1 - w);
                y = Math.Clamp(relY - h/2, 0, 1 - h);
                // Convert from relative already
                w = w * imgW; h = h * imgH; x = x * imgW; y = y * imgH;
                // For coords case, w/h are already relative 0-1, need to convert to pixels
                // For fallback, w/h already in pixels, x/y already in pixels
                MapHighlight.Width = w;
                MapHighlight.Height = h;
                Canvas.SetLeft(MapHighlight, x);
                Canvas.SetTop(MapHighlight, y);
                MapHighlightLabel.Text = info.ClassroomRaw?.Replace(";", "").Trim() ?? info.RoomRaw ?? $"{info.Building} {info.Floor}";
                MapHighlight.Visibility = Visibility.Visible;
                double pulseLeft = x + w/2 - 9;
                double pulseTop = y + h/2 - 9;
                Canvas.SetLeft(MapPulse, pulseLeft);
                Canvas.SetTop(MapPulse, pulseTop);
                MapPulse.Visibility = Visibility.Visible;
                MapOverlay.Width = imgW;
                MapOverlay.Height = imgH;
                try
                {
                    var scale = new ScaleTransform(1,1,9,9);
                    MapPulse.RenderTransform = scale;
                    var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 1.6, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, EasingFunction = new System.Windows.Media.Animation.SineEase() };
                    var anim2 = new System.Windows.Media.Animation.DoubleAnimation(0.85, 0.3, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                    MapPulse.BeginAnimation(UIElement.OpacityProperty, anim2);
                }
                catch {}
                return;
            }
            // Coords case: x,y,w,h are relative 0-1
            double px = x * imgW;
            double py = y * imgH;
            double pw = w * imgW;
            double ph = h * imgH;
            MapHighlight.Width = pw;
            MapHighlight.Height = ph;
            Canvas.SetLeft(MapHighlight, px);
            Canvas.SetTop(MapHighlight, py);
            MapHighlightLabel.Text = info.ClassroomRaw?.Replace(";", "").Trim() ?? info.RoomRaw ?? $"{info.Building} {info.Floor}";
            MapHighlight.Visibility = Visibility.Visible;
            double pLeft = px + pw/2 - 9;
            double pTop = py + ph/2 - 9;
            Canvas.SetLeft(MapPulse, pLeft);
            Canvas.SetTop(MapPulse, pTop);
            MapPulse.Visibility = Visibility.Visible;
            MapOverlay.Width = imgW;
            MapOverlay.Height = imgH;
            try
            {
                var scale = new ScaleTransform(1,1,9,9);
                MapPulse.RenderTransform = scale;
                var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 1.6, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, EasingFunction = new System.Windows.Media.Animation.SineEase() };
                var anim2 = new System.Windows.Media.Animation.DoubleAnimation(0.85, 0.3, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                MapPulse.BeginAnimation(UIElement.OpacityProperty, anim2);
            }
            catch {}
        }
        catch { try { MapHighlight.Visibility = Visibility.Collapsed; MapPulse.Visibility = Visibility.Collapsed; } catch {} }
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
        if (BtnYesterday != null) BtnYesterday.Style = (Style)FindResource("GhostButton");
        BtnToday.Style = (Style)FindResource("GhostButton");
        BtnTomorrow.Style = (Style)FindResource("GhostButton");
        BtnWeek.Style = (Style)FindResource("GhostButton");
        if (BtnSummary != null) BtnSummary.Style = (Style)FindResource("GhostButton");
        BtnWeekOdd.Style = (Style)FindResource("GhostButton");
        BtnWeekEven.Style = (Style)FindResource("GhostButton");
        if (_currentTab == "Yesterday" && BtnYesterday != null) BtnYesterday.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Today") BtnToday.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Tomorrow") BtnTomorrow.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Week") BtnWeek.Style = (Style)FindResource("FerryButton");
        else if (_currentTab == "Summary" && BtnSummary != null) BtnSummary.Style = (Style)FindResource("FerryButton");
        if (_weekParity == 1) BtnWeekOdd.Style = (Style)FindResource("FerryButton");
        else BtnWeekEven.Style = (Style)FindResource("FerryButton");
        WeekParityPanel.Visibility = _currentTab == "Week" ? Visibility.Visible : Visibility.Collapsed;
        ScheduleScroll.Visibility = _currentTab != "Week" && _currentTab != "Summary" ? Visibility.Visible : Visibility.Collapsed;
        WeekScroll.Visibility = _currentTab == "Week" ? Visibility.Visible : Visibility.Collapsed;
        // Summary overlay
        if (SummaryPanel != null) SummaryPanel.Visibility = _currentTab == "Summary" ? Visibility.Visible : Visibility.Collapsed;
        if (MainContentGrid != null) MainContentGrid.Visibility = _currentTab == "Summary" ? Visibility.Collapsed : Visibility.Visible;
        // BtnTeachers is action, not a tab, keep ghost
        if (BtnTeachers != null) BtnTeachers.Style = (Style)FindResource("GhostButton");
        if (BtnMapToggle != null && _currentTab != "Summary") { /* keep map toggle style */ }
    }

    private void TabYesterday_Click(object sender, RoutedEventArgs e) { _currentTab = "Yesterday"; UpdateTabButtons(); UpdateParityBadge(DateTime.Today.AddDays(-1)); RenderCurrentView(); }
    private void TabToday_Click(object sender, RoutedEventArgs e) { _currentTab = "Today"; UpdateTabButtons(); UpdateParityBadge(DateTime.Today); RenderCurrentView(); }
    private void TabTomorrow_Click(object sender, RoutedEventArgs e) { _currentTab = "Tomorrow"; UpdateTabButtons(); UpdateParityBadge(DateTime.Today.AddDays(1)); RenderCurrentView(); }
    private void TabWeek_Click(object sender, RoutedEventArgs e) { _currentTab = "Week"; UpdateTabButtons(); RenderCurrentView(); }
    private void TabSummary_Click(object sender, RoutedEventArgs e) { _currentTab = "Summary"; UpdateTabButtons(); RenderSummary(); }
    private void CloseSummary_Click(object sender, RoutedEventArgs e) { _currentTab = "Tomorrow"; UpdateTabButtons(); UpdateParityBadge(DateTime.Today.AddDays(1)); RenderCurrentView(); }
    private void Teachers_Click(object sender, RoutedEventArgs e)
    {
        if (_db == null || _i18n == null) return;
        if (GroupPicker.SelectedValue is not string gid) { StatusText.Text = _i18n.Language=="en" ? "Select group first" : "Выберите группу"; return; }
        try
        {
            var g = _db.GetGroup(gid);
            var dlg = new TeacherFinderDialog(_db, _i18n, gid);
            dlg.Owner = this;
            dlg.ShowDialog();
            StatusText.Text = g != null ? $"Преподаватели {g.Name}" : "Преподаватели";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка открытия преподавателей:\n{ex.Message}\n\n{ex.StackTrace}", "Преподаватели", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = $"Ошибка: {ex.Message}";
        }
    }
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
            // For week view, show week number based on current date's week code's parity week number
            try
            {
                var s = _db?.GetSettings();
                if (s != null && !string.IsNullOrEmpty(s.PeriodStart) && DateTime.TryParse(s.PeriodStart, out var ps))
                {
                    int wn = ParityService.GetWeekNumber(date, ps);
                    if (WeekNumberText != null) WeekNumberText.Text = _i18n.T("weekNum", wn);
                }
                else if (WeekNumberText != null) WeekNumberText.Text = "";
            }
            catch { }
            return;
        }
        // For day views, show week number
        try
        {
            var s = _db?.GetSettings();
            if (s != null && !string.IsNullOrEmpty(s.PeriodStart) && DateTime.TryParse(s.PeriodStart, out var ps2))
            {
                int wn = ParityService.GetWeekNumber(date, ps2);
                if (WeekNumberText != null) WeekNumberText.Text = _i18n.T("weekNum", wn);
            }
            else if (WeekNumberText != null) WeekNumberText.Text = "";
        }
        catch { if (WeekNumberText != null) WeekNumberText.Text = ""; }
    }

    private void RenderCurrentView()
    {
        if (_db == null) return;
        if (GroupPicker.SelectedValue is not string gid) return;
        DateTime badgeDate = _currentTab switch { "Yesterday" => DateTime.Today.AddDays(-1), "Today" => DateTime.Today, "Tomorrow" => DateTime.Today.AddDays(1), _ => DateTime.Today };
        // For Week and Summary, keep badge as today/tomorrow logic inside UpdateParityBadge
        if (_currentTab == "Yesterday" || _currentTab == "Today" || _currentTab == "Tomorrow") UpdateParityBadge(badgeDate);
        else UpdateParityBadge(_currentTab == "Week" ? DateTime.Today : DateTime.Today);
        if (_currentTab == "Week") RenderWeekView();
        else if (_currentTab == "Summary") RenderSummary();
        else RenderDayView();
        UpdateMapForNextLesson();
    }

    private void RenderSummary()
    {
        if (_db == null || SummaryStack == null) return;
        if (GroupPicker.SelectedValue is not string gid) return;
        SummaryStack.Children.Clear();
        var g = _db.GetGroup(gid);
        if (SummaryGroupText != null) SummaryGroupText.Text = g != null ? $"{g.Name} · {g.Id}" : gid;
        if (LblSummaryTitle != null) LblSummaryTitle.Text = _i18n != null ? _i18n.T("summaryTitle") : "СВОДКА";
        // Build 3 sections: odd, even, both (2 weeks)
        var allLessons = _db.GetAllLessonsForGroup(gid);
        var odd = allLessons.Where(l => l.Parity == 1).ToList();
        var even = allLessons.Where(l => l.Parity == 2).ToList();
        var both = allLessons; // combined 2 weeks
        SummaryStack.Children.Add(CreateSummarySection(_i18n != null ? _i18n.T("weekOdd") : "Нечетная", odd, "#FF98C379"));
        SummaryStack.Children.Add(CreateSummarySection(_i18n != null ? _i18n.T("weekEven") : "Четная", even, "#FF6CA5E0"));
        SummaryStack.Children.Add(CreateSummarySection(_i18n != null ? _i18n.T("summaryBoth") : "Обе недели (2 недели)", both, "#FFC5CAD3"));
        // Footer hint
        var hint = new TextBlock { Text = _i18n != null ? _i18n.T("summaryHint") : "Сводка по всем парам группы: типы, предметы, преподаватели, аудитории", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,0), FontStyle = FontStyles.Italic };
        SummaryStack.Children.Add(hint);
    }

    private Border CreateSummarySection(string title, List<Lesson> lessons, string accentHex)
    {
        var border = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0,0,0,10), Padding = new Thickness(10), BorderBrush = (Brush)FindResource("BorderDim"), Background = (Brush)FindResource("Panel") };
        var outer = new StackPanel();
        // Title with accent
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
        var dot = new TextBlock { Text = "●", Foreground = (Brush)new BrushConverter().ConvertFromString(accentHex)!, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
        var tbTitle = new TextBlock { Text = $"{title.ToUpper()} — {lessons.Count} пар", Foreground = (Brush)FindResource("Bronze"), FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(dot); titleRow.Children.Add(tbTitle);
        outer.Children.Add(titleRow);
        if (lessons.Count == 0)
        {
            outer.Children.Add(new TextBlock { Text = _i18n != null ? _i18n.T("noLessons") : "Нет занятий", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10 });
            border.Child = outer;
            return border;
        }
        // By day
        var byDay = lessons.GroupBy(l => l.DayOfWeek).OrderBy(g => g.Key).ToList();
        var dayLine = string.Join(" · ", byDay.Select(g => $"{ParityService.DayNumberToTitle(g.Key).Substring(0,2)} {g.Count()}"));
        // localize day short if en
        if (_i18n != null && _i18n.Language == "en")
        {
            dayLine = string.Join(" · ", byDay.Select(g => $"{_i18n.T(g.Key switch {1=>"mon",2=>"tue",3=>"wed",4=>"thu",5=>"fri",6=>"sat",_=>"mon"}).Substring(0,2)} {g.Count()}"));
        }
        var tbDay = new TextBlock { Text = (_i18n != null && _i18n.Language=="en" ? "By day: " : "По дням: ") + dayLine, Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,6) };
        outer.Children.Add(tbDay);
        // By type
        var byType = lessons.GroupBy(l => string.IsNullOrWhiteSpace(l.TypeRaw) ? "—" : l.TypeRaw).OrderByDescending(g => g.Count()).ToList();
        var typeLine = string.Join(" · ", byType.Select(g => $"{g.Key} {g.Count()}"));
        var tbType = new TextBlock { Text = (_i18n != null && _i18n.Language=="en" ? "By type: " : "По типу: ") + typeLine, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,6) };
        outer.Children.Add(tbType);
        // By subject — show all (was top 8 +3, now full per user request)
        var bySubj = lessons.GroupBy(l => string.IsNullOrWhiteSpace(l.SubjectRaw) ? "—" : (_overrideService?.GetDisplayName(l.SubjectRaw, l.DayOfWeek) ?? l.SubjectRaw)).OrderByDescending(g => g.Count()).ToList();
        var subjPanel = new StackPanel { Margin = new Thickness(0,0,0,6) };
        subjPanel.Children.Add(new TextBlock { Text = _i18n != null && _i18n.Language=="en" ? "By subject:" : "По предметам:", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, Margin = new Thickness(0,0,0,2) });
        foreach (var g in bySubj)
        {
            var row = new Grid { Margin = new Thickness(0,1,0,1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tbCnt = new TextBlock { Text = g.Count().ToString(), Foreground = (Brush)FindResource("Bronze"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            var tbSubj = new TextBlock { Text = g.Key, Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tbCnt, 0); Grid.SetColumn(tbSubj, 1);
            row.Children.Add(tbCnt); row.Children.Add(tbSubj);
            subjPanel.Children.Add(row);
        }
        outer.Children.Add(subjPanel);
        // By teacher — show all (was top 6)
        var teacherGroups = lessons.Where(l => !string.IsNullOrWhiteSpace(l.TeacherRaw) && l.TeacherRaw != "—").SelectMany(l => l.TeacherRaw.Split(';').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).Select(t => new { Teacher = t, Lesson = l })).GroupBy(x => x.Teacher).OrderByDescending(g => g.Count()).ToList();
        var teachPanel = new StackPanel { Margin = new Thickness(0,0,0,4) };
        teachPanel.Children.Add(new TextBlock { Text = _i18n != null && _i18n.Language=="en" ? "By teacher:" : "По преподавателям:", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, Margin = new Thickness(0,0,0,2) });
        if (teacherGroups.Count == 0)
        {
            teachPanel.Children.Add(new TextBlock { Text = "—", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10 });
        }
        else
        {
            foreach (var g in teacherGroups)
            {
                var row = new Grid { Margin = new Thickness(0,1,0,1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var tbCnt2 = new TextBlock { Text = g.Count().ToString(), Foreground = (Brush)FindResource("Patina"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
                var tbTeach = new TextBlock { Text = g.Key, Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6,0,0,0), VerticalAlignment = VerticalAlignment.Center };
                // tooltip with rooms
                var rooms = string.Join(", ", g.Select(x => x.Lesson.ClassroomRaw).Distinct().Take(3));
                tbTeach.ToolTip = rooms;
                tbCnt2.ToolTip = rooms;
                Grid.SetColumn(tbCnt2, 0); Grid.SetColumn(tbTeach, 1);
                row.Children.Add(tbCnt2); row.Children.Add(tbTeach);
                teachPanel.Children.Add(row);
            }
        }
        outer.Children.Add(teachPanel);
        // By room — show all (was top 5)
        var byRoom = lessons.Where(l => !string.IsNullOrWhiteSpace(l.ClassroomRaw)).GroupBy(l => l.ClassroomRaw).OrderByDescending(g => g.Count()).ToList();
        if (byRoom.Count > 0)
        {
            var roomLine = string.Join(" · ", byRoom.Select(g => $"{g.Key.TrimEnd(';',' ')} {g.Count()}"));
            var tbRoom = new TextBlock { Text = (_i18n != null && _i18n.Language=="en" ? "Rooms: " : "Аудитории: ") + roomLine, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,0) };
            outer.Children.Add(tbRoom);
        }
        border.Child = outer;
        return border;
    }

    private void RenderDayView()
    {
        if (_db == null || _schedule == null) return;
        if (GroupPicker.SelectedValue is not string gid) return;
        // Enable SharedSizeScope so header and all cards share column widths — fixes crooked columns
        try { Grid.SetIsSharedSizeScope(SchedulePanel, true); } catch {}
        // recompute statuses each render
        try { _homeworkService?.RecomputeAllStatuses(); } catch { }
        SchedulePanel.Children.Clear();
        EmptyText.Visibility = Visibility.Collapsed;
        DateTime date = _currentTab switch { "Yesterday" => DateTime.Today.AddDays(-1), "Today" => DateTime.Today, _ => DateTime.Today.AddDays(1) };
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
        // 8 columns: must match CreateLessonCard exactly (fix crooked columns)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30), SharedSizeGroup = "col0" }); // No
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85), SharedSizeGroup = "col1" }); // Time
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = "col2" }); // Subject
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140), SharedSizeGroup = "col3" }); // Teacher
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90), SharedSizeGroup = "col4" }); // Room
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90), SharedSizeGroup = "col5" }); // Next pair
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110), SharedSizeGroup = "col6" }); // Traffic lights
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60), SharedSizeGroup = "col7" }); // Actions
        string nextPairHeader = _i18n != null ? _i18n.T("nextPair") : "След. пара";
        string trafficHeader = "●"; // traffic light column header
        string[] headers = _i18n != null ? new[] { _i18n.T("colNo"), _i18n.T("colTime"), _i18n.T("colSubject"), _i18n.T("colTeacher"), _i18n.T("colRoom"), nextPairHeader, trafficHeader, "" } : new[] { "№", "Время", "Предмет", "Преподаватель", "Ауд./Корп.", "След. пара", "●", "" };
        for (int i = 0; i < headers.Length; i++)
        {
            var tb = new TextBlock { Text = headers[i], Foreground = (Brush)FindResource("Bronze"), FontSize = 10, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, ToolTip = i==5 ? (_i18n?.T("nextPairHint") ?? "Дата следующей пары") : i==6 ? "Близость друзей (светофор)" : null };
            if (i == 0 || i == 5 || i == 6) tb.HorizontalAlignment = HorizontalAlignment.Center;
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

        // Top grid with lesson info — SharedSizeGroup ensures header/card stay aligned (8 cols: No/Time/Subject/Teacher/Room/Next/Traffic/Actions)
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30), SharedSizeGroup = "col0" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85), SharedSizeGroup = "col1" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = "col2" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140), SharedSizeGroup = "col3" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90), SharedSizeGroup = "col4" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90), SharedSizeGroup = "col5" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110), SharedSizeGroup = "col6" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60), SharedSizeGroup = "col7" }); // actions

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
        // Next pair column: date of next occurrence of same subject + additionally for same teacher if differs
        DateTime nextFrom = _currentTab == "Yesterday" ? DateTime.Today.AddDays(-1) : _currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today;
        string nextPairText = GetNextPairDateText(l, nextFrom);
        string nextTeacherText = GetNextTeacherDateText(l, nextFrom);
        bool showTeacherNext = !string.IsNullOrWhiteSpace(l.TeacherRaw) && l.TeacherRaw != "—" && !string.IsNullOrEmpty(nextTeacherText) && nextTeacherText != "—" && nextTeacherText != nextPairText;
        var tbNextPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var tbNext = new TextBlock { Text = nextPairText, Foreground = string.IsNullOrEmpty(nextPairText) || nextPairText=="—" ? (Brush)FindResource("MarbleDim") : (Brush)FindResource("Patina"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, ToolTip = "Следующая пара по этому предмету" };
        tbNextPanel.Children.Add(tbNext);
        TextBlock? tbNextTeacher = null;
        if (showTeacherNext)
        {
            tbNextTeacher = new TextBlock { Text = nextTeacherText, Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, ToolTip = $"Следующая у {l.TeacherRaw}", Margin = new Thickness(0,1,0,0), FontStyle = FontStyles.Italic, Opacity = 0.85 };
            tbNextPanel.Children.Add(tbNextTeacher);
        }
        // Traffic lights for friends: vertical up to 5, with gradation based on score (proximity)
        var trafficPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        try
        {
            var friends = _db?.GetFriends() ?? new List<FriendGroup>();
            var settings = _db?.GetSettings();
            int strict = settings?.IntersectionStrictness ?? 25;
            DateTime iconDate = _currentTab == "Yesterday" ? DateTime.Today.AddDays(-1) : _currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today;
            // For week view, use dow-based date approximation
            if (_currentTab == "Week")
            {
                int daysAhead = (l.DayOfWeek - (int)DateTime.Today.DayOfWeek + 7) % 7;
                iconDate = DateTime.Today.AddDays(daysAhead);
            }
            var inters = _intersectionService?.GetIntersections(l, iconDate, friends, strict) ?? new List<IntersectionService.IntersectionResult>();
            bool alwaysShow = settings?.AlwaysShowAllTrafficLights ?? false;
            // Build lookup for quick check
            var interByGroup = inters.ToDictionary(x => x.FriendGroupName, x => x);
            var friendsToShow = alwaysShow ? friends.Where(f => f.Enabled).Take(5).ToList() : null;
            if (!alwaysShow && inters.Count == 0)
            {
                // нет на месте — потухший светофор (dimmed, no glow) — как просил пользователь
                var offWrap = new Grid { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var off = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E252E")), Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E343F")), StrokeThickness = 1, Opacity = 0.9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                offWrap.Children.Add(off);
                offWrap.ToolTip = "нет на месте";
                trafficPanel.Children.Add(offWrap);
                trafficPanel.ToolTip = "нет на месте";
            }
            else if (alwaysShow && friendsToShow != null)
            {
                // Always show all selected friends: up to 5, dimmed when empty, colored when present
                foreach (var fr in friendsToShow)
                {
                    if (interByGroup.TryGetValue(fr.GroupName, out var inter))
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,2,0,2), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
                        var ellipse = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, StrokeThickness = 1.2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                        Color glowColor;
                        Brush fill;
                        if (inter.Score >= 100) { fill = (Brush)FindResource("Patina"); glowColor = (Color)ColorConverter.ConvertFromString("#FF98C379"); }
                        else if (inter.Score >= 75) { fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA8E6A0")); glowColor = (Color)ColorConverter.ConvertFromString("#FFA8E6A0"); }
                        else if (inter.Score >= 50) { fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF2C55C")); glowColor = (Color)ColorConverter.ConvertFromString("#FFF2C55C"); }
                        else if (inter.Score >= 25) { fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6CA5E0")); glowColor = (Color)ColorConverter.ConvertFromString("#FF6CA5E0"); }
                        else { fill = (Brush)FindResource("Cinnabar"); glowColor = (Color)ColorConverter.ConvertFromString("#FFE06C75"); }
                        ellipse.Fill = fill;
                        ellipse.Stroke = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                        ellipse.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = glowColor, BlurRadius = inter.Score >= 100 ? 10 : inter.Score >= 75 ? 9 : inter.Score >= 50 ? 7 : 6, ShadowDepth = 0, Opacity = 0.95 };
                        var container = new Grid { Width = 12, Height = 12 };
                        container.Children.Add(ellipse);
                        var highlight = new System.Windows.Shapes.Ellipse { Width = 4, Height = 4, Fill = Brushes.White, Opacity = 0.35, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2,2,0,0), IsHitTestVisible = false };
                        container.Children.Add(highlight);
                        row.Children.Add(container);
                        string memberHint = string.IsNullOrWhiteSpace(fr.MemberNames) ? "" : $" ({fr.MemberNames})";
                        var lbl = new TextBlock { Text = $" - {fr.GroupName}{memberHint}", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6,0,0,0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 85 };
                        lbl.ToolTip = string.IsNullOrWhiteSpace(fr.MemberNames) ? null : fr.MemberNames;
                        row.Children.Add(lbl);
                        string grad = IntersectionService.ScoreToText(inter.Score);
                        string detail = $"{fr.GroupName}{memberHint} — {inter.Teacher} {inter.Room} ({grad})";
                        row.ToolTip = detail;
                        row.MouseLeftButtonUp += (s, e) =>
                        {
                            e.Handled = true;
                            try
                            {
                                var popup = new System.Windows.Controls.Primitives.Popup { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse, StaysOpen = false, AllowsTransparency = true, PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade };
                                var card = new Border { Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Bronze"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8,6,8,6), MaxWidth = 340 };
                                var sp = new StackPanel();
                                var title = new TextBlock { Text = fr.GroupName, Foreground = (Brush)FindResource("Bronze"), FontSize = 11, FontWeight = FontWeights.Bold };
                                var body = new TextBlock { Text = detail, Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,0) };
                                var hint = new TextBlock { Text = "Нажмите, чтобы показать на карте", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, FontStyle = FontStyles.Italic, Margin = new Thickness(0,6,0,0) };
                                sp.Children.Add(title); sp.Children.Add(body); sp.Children.Add(hint);
                                card.Child = sp;
                                card.MouseLeftButtonUp += (s2, e2) => { popup.IsOpen = false; try { EnsureMapVisible(); var fakeLesson = new Lesson { ClassroomRaw = inter.Room, RoomRaw = inter.Room, BuildingRaw = inter.Room.Contains("ВЦ") ? "ВЦ" : inter.Room.Contains("*") ? "УЛК" : "ГК" }; var mi = _mapService?.GetMapForLesson(fakeLesson); if (mi != null) UpdateMapDisplay(mi, iconDate, fakeLesson); StatusText.Text = $"Показана карта для {fr.GroupName} {inter.Room}"; } catch {} };
                                popup.Child = card; popup.IsOpen = true; StatusText.Text = detail;
                            }
                            catch {}
                        };
                        trafficPanel.Children.Add(row);
                    }
                    else
                    {
                        // empty — потухший
                        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,2,0,2), Background = Brushes.Transparent };
                        var offWrap = new Grid { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                        var off = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E252E")), Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E343F")), StrokeThickness = 1, Opacity = 0.9 };
                        offWrap.Children.Add(off);
                        row.Children.Add(offWrap);
                        string memberHint = string.IsNullOrWhiteSpace(fr.MemberNames) ? "" : $" ({fr.MemberNames})";
                        var lbl = new TextBlock { Text = $" - {fr.GroupName}{memberHint}", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6,0,0,0), Opacity = 0.5, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 85 };
                        row.Children.Add(lbl);
                        row.ToolTip = $"{fr.GroupName}{memberHint} — нет на месте";
                        trafficPanel.Children.Add(row);
                    }
                }
            }
            else
            {
                foreach (var inter in inters.Take(5))
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,2,0,2), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
                    // Traffic light: repainted — 12px, glossy, strong glow, traffic gradation
                    var ellipse = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, StrokeThickness = 1.2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Color glowColor;
                    Brush fill;
                    // 5 gradations per user: 100 аудитория, 75 этаж, 50 корпус, 25 в вузе (корпуса в упор — не красный), else нет на месте (handled as ·)
                    if (inter.Score >= 100) { fill = (Brush)FindResource("Patina"); glowColor = (Color)ColorConverter.ConvertFromString("#FF98C379"); } // в той же аудитории — ярко зелёный
                    else if (inter.Score >= 75) { fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA8E6A0")); glowColor = (Color)ColorConverter.ConvertFromString("#FFA8E6A0"); } // на том же этаже — светло-зелёный
                    else if (inter.Score >= 50) { fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF2C55C")); glowColor = (Color)ColorConverter.ConvertFromString("#FFF2C55C"); } // в том же корпусе — жёлтый
                    else if (inter.Score >= 25) { fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6CA5E0")); glowColor = (Color)ColorConverter.ConvertFromString("#FF6CA5E0"); } // в вузе — синий/бронза, не красный (корпуса в упор)
                    else { fill = (Brush)FindResource("Cinnabar"); glowColor = (Color)ColorConverter.ConvertFromString("#FFE06C75"); } // fallback
                    ellipse.Fill = fill;
                    ellipse.Stroke = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                    ellipse.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = glowColor, BlurRadius = inter.Score >= 100 ? 10 : inter.Score >= 75 ? 9 : inter.Score >= 50 ? 7 : 6, ShadowDepth = 0, Opacity = 0.95 };
                    // Inner highlight for 3D traffic light look
                    var container = new Grid { Width = 12, Height = 12 };
                    container.Children.Add(ellipse);
                    var highlight = new System.Windows.Shapes.Ellipse { Width = 4, Height = 4, Fill = Brushes.White, Opacity = 0.35, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2,2,0,0), IsHitTestVisible = false };
                    container.Children.Add(highlight);
                    row.Children.Add(container);
                    // Show member names if set for this group
                    var frForLabel = friends.FirstOrDefault(f => f.GroupName == inter.FriendGroupName);
                    string mHint = frForLabel != null && !string.IsNullOrWhiteSpace(frForLabel.MemberNames) ? $" ({frForLabel.MemberNames})" : "";
                    var lbl = new TextBlock { Text = $" - {inter.FriendGroupName}{mHint}", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6,0,0,0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 85 };
                    row.Children.Add(lbl);
                    string grad = IntersectionService.ScoreToText(inter.Score);
                    string detail = $"{inter.FriendGroupName}{mHint} — {inter.Teacher} {inter.Room} ({grad})";
                    row.ToolTip = detail;
                    // Click opens the same detail as a popup — “эта штучка” from screenshot
                    row.MouseLeftButtonUp += (s, e) =>
                    {
                        e.Handled = true;
                        try
                        {
                            // Styled popup near cursor
                            var popup = new System.Windows.Controls.Primitives.Popup
                            {
                                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
                                StaysOpen = false,
                                AllowsTransparency = true,
                                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
                            };
                            var card = new Border
                            {
                                Background = (Brush)FindResource("Panel"),
                                BorderBrush = (Brush)FindResource("Bronze"),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(8,6,8,6),
                                MaxWidth = 340
                            };
                            var sp = new StackPanel();
                            var title = new TextBlock { Text = inter.FriendGroupName, Foreground = (Brush)FindResource("Bronze"), FontSize = 11, FontWeight = FontWeights.Bold };
                            var body = new TextBlock { Text = detail, Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,0) };
                            var hint = new TextBlock { Text = "Нажмите, чтобы показать на карте", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, FontStyle = FontStyles.Italic, Margin = new Thickness(0,6,0,0) };
                            sp.Children.Add(title); sp.Children.Add(body); sp.Children.Add(hint);
                            card.Child = sp;
                            card.MouseLeftButtonUp += (s2, e2) =>
                            {
                                popup.IsOpen = false;
                                // Show friend's room on map
                                try
                                {
                                    EnsureMapVisible();
                                    // Find friend's lesson room and show its map
                                    var fakeLesson = new Lesson { ClassroomRaw = inter.Room, RoomRaw = inter.Room, BuildingRaw = inter.Room.Contains("ВЦ") ? "ВЦ" : inter.Room.Contains("*") ? "УЛК" : "ГК" };
                                    var mi = _mapService?.GetMapForLesson(fakeLesson);
                                    if (mi != null) UpdateMapDisplay(mi, iconDate, fakeLesson);
                                    StatusText.Text = $"Показана карта для {inter.FriendGroupName} {inter.Room}";
                                }
                                catch {}
                            };
                            popup.Child = card;
                            popup.IsOpen = true;
                            // Also show in status
                            StatusText.Text = detail;
                        }
                        catch {}
                    };
                    trafficPanel.Children.Add(row);
                }
            }
        }
        catch
        {
            var offWrap2 = new Grid { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var off2 = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E252E")), Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E343F")), StrokeThickness = 1, Opacity = 0.9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            offWrap2.Children.Add(off2);
            offWrap2.ToolTip = "нет на месте";
            trafficPanel.Children.Add(offWrap2);
        }

        // Action buttons
        var actionPanel = new StackPanel { Orientation = Orientation.Vertical };
        var btnRename = new Button { Content = "✎", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(4,2,4,2), FontSize = 10, Margin = new Thickness(0,0,0,2), ToolTip = "Переименовать" };
        btnRename.Click += (s, e) => OpenRenameDialog(l);
        var btnHw = new Button { Content = "+", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(4,2,4,2), FontSize = 10, ToolTip = "ДЗ" };
        btnHw.Click += (s, e) => OpenHomeworkDialog(l, null);
        var btnMap = new Button { Content = "◉", Style = (Style)FindResource("GhostButton"), Padding = new Thickness(4,2,4,2), FontSize = 10, Margin = new Thickness(0,2,0,0), ToolTip = "Показать на карте" };
        btnMap.Click += (s, e) =>
        {
            EnsureMapVisible();
            var mi = _mapService?.GetMapForLesson(l);
            DateTime d = _currentTab == "Yesterday" ? DateTime.Today.AddDays(-1) : _currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today;
            // for week view, dow based
            if (_currentTab == "Week")
            {
                // approximate date for dow
                int daysAhead = (l.DayOfWeek - (int)DateTime.Today.DayOfWeek + 7) % 7;
                d = DateTime.Today.AddDays(daysAhead);
            }
            UpdateMapDisplay(mi, d, l);
        };
        actionPanel.Children.Add(btnRename);
        actionPanel.Children.Add(btnHw);
        actionPanel.Children.Add(btnMap);

        Grid.SetColumn(tbNo, 0); Grid.SetColumn(tbTime, 1); Grid.SetColumn(tbSubjStack, 2); Grid.SetColumn(tbTeach, 3); Grid.SetColumn(tbRoom, 4); Grid.SetColumn(tbNextPanel, 5); Grid.SetColumn(trafficPanel, 6); Grid.SetColumn(actionPanel, 7);
        grid.Children.Add(tbNo); grid.Children.Add(tbTime); grid.Children.Add(tbSubjStack); grid.Children.Add(tbTeach); grid.Children.Add(tbRoom); grid.Children.Add(tbNextPanel); grid.Children.Add(trafficPanel); grid.Children.Add(actionPanel);

        // Context menu for right-click
        var cm = new ContextMenu();
        var miRename = new MenuItem { Header = "Переименовать" };
        miRename.Click += (s, e) => OpenRenameDialog(l);
        var miReset = new MenuItem { Header = "Сбросить к оригиналу" };
        miReset.Click += (s, e) => { var ovs = _db!.GetOverrides().Where(o => o.SubjectRawNormalized == ParityService.NormalizeSubject(l.SubjectRaw)).ToList(); foreach (var ov in ovs) _overrideService!.Remove(ov.Id); RenderCurrentView(); };
        var miHw = new MenuItem { Header = "Добавить ДЗ" };
        miHw.Click += (s, e) => OpenHomeworkDialog(l, null);
        var miMap = new MenuItem { Header = "Показать на карте" };
        miMap.Click += (s, e) =>
        {
            EnsureMapVisible();
            var mi2 = _mapService?.GetMapForLesson(l);
            DateTime d2 = _currentTab == "Yesterday" ? DateTime.Today.AddDays(-1) : _currentTab == "Today" ? DateTime.Today : _currentTab == "Tomorrow" ? DateTime.Today.AddDays(1) : DateTime.Today;
            if (_currentTab == "Week") { int daysAhead = (l.DayOfWeek - (int)DateTime.Today.DayOfWeek + 7) % 7; d2 = DateTime.Today.AddDays(daysAhead); }
            UpdateMapDisplay(mi2, d2, l);
        };
        cm.Items.Add(miRename); cm.Items.Add(miReset); cm.Items.Add(miHw); cm.Items.Add(miMap);
        outer.ContextMenu = cm;

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

    private string GetNextPairDateText(Lesson lesson, DateTime fromDate)
    {
        try
        {
            if (_db == null) return "—";
            var norm = ParityService.NormalizeSubject(lesson.SubjectRaw);
            if (string.IsNullOrWhiteSpace(norm)) return "—";
            var settings = _db.GetSettings();
            if (string.IsNullOrEmpty(settings.MyGroupId)) return "—";
            var groupId = settings.MyGroupId!;
            DateTime periodStart = DateTime.TryParse(settings.PeriodStart, out var ps) ? ps : new DateTime(DateTime.Now.Year, 9, 1);
            int weekCount = settings.WeekCount > 0 ? settings.WeekCount : 2;
            for (int offset = 1; offset <= 60; offset++)
            {
                var date = fromDate.Date.AddDays(offset);
                if (date.DayOfWeek == DayOfWeek.Sunday) continue;
                int dow = (int)date.DayOfWeek; if (dow == 0) dow = 7;
                int weekCode = ParityService.GetWeekCode(date, periodStart, weekCount);
                if (settings.ParityInvert) weekCode = weekCode == 1 ? 2 : 1;
                var lessons = _db.GetLessons(groupId, dow, weekCode);
                foreach (var ll in lessons)
                {
                    if (ParityService.NormalizeSubject(ll.SubjectRaw) == norm)
                    {
                        string fmt = _i18n != null && _i18n.Language == "en" ? date.ToString("MM-dd") : date.ToString("dd.MM");
                        string dayShort = _i18n != null ? _i18n.FormatDay(date) : date.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                        return $"{fmt} {dayShort}";
                    }
                }
            }
            return "—";
        }
        catch { return "—"; }
    }

    private string GetNextTeacherDateText(Lesson lesson, DateTime fromDate)
    {
        try
        {
            if (_db == null) return "—";
            var teacher = lesson.TeacherRaw?.Trim();
            if (string.IsNullOrWhiteSpace(teacher) || teacher == "—") return "—";
            // For teacher, search across lecturer schedule (all groups) is more accurate, but for student's group we can use group schedule's teacher
            // First try lecturer service if loaded, otherwise fallback to group schedule
            // Try to find teacher's next lesson via LecturerService if available
            // For now, use group schedule same as subject but matching teacher
            var settings = _db.GetSettings();
            if (string.IsNullOrEmpty(settings.MyGroupId)) return "—";
            var groupId = settings.MyGroupId!;
            DateTime periodStart = DateTime.TryParse(settings.PeriodStart, out var ps) ? ps : new DateTime(DateTime.Now.Year, 9, 1);
            int weekCount = settings.WeekCount > 0 ? settings.WeekCount : 2;
            // Normalize teacher for comparison (short name)
            var teachNorm = teacher.Split(';')[0].Trim().ToLowerInvariant();
            for (int offset = 1; offset <= 60; offset++)
            {
                var date = fromDate.Date.AddDays(offset);
                if (date.DayOfWeek == DayOfWeek.Sunday) continue;
                int dow = (int)date.DayOfWeek; if (dow == 0) dow = 7;
                int weekCode = ParityService.GetWeekCode(date, periodStart, weekCount);
                if (settings.ParityInvert) weekCode = weekCode == 1 ? 2 : 1;
                var lessons = _db.GetLessons(groupId, dow, weekCode);
                foreach (var ll in lessons)
                {
                    if (string.IsNullOrWhiteSpace(ll.TeacherRaw)) continue;
                    var tNorm = ll.TeacherRaw.Split(';')[0].Trim().ToLowerInvariant();
                    if (tNorm == teachNorm || ll.TeacherRaw.ToLowerInvariant().Contains(teachNorm) || teachNorm.Contains(tNorm))
                    {
                        string fmt = _i18n != null && _i18n.Language == "en" ? date.ToString("MM-dd") : date.ToString("dd.MM");
                        string dayShort = _i18n != null ? _i18n.FormatDay(date) : date.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                        return $"{fmt} {dayShort}";
                    }
                }
            }
            return "—";
        }
        catch { return "—"; }
    }

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

    // Silent self-update: download zip in background, ask once to restart into it.
    // The running EXE cannot replace itself, so a small update.bat waits for exit,
    // unpacks over the install dir and starts the app again.
    private async Task AutoUpdateFlowAsync(bool manual)
    {
        if (_db == null || _i18n == null) return;
        if (!manual && !_db.GetSettings().AutoUpdate) return;
        AutoUpdateService.UpdateInfo? info = null;
        try { info = await new AutoUpdateService().GetLatestAsync("windows"); }
        catch { if (manual) MessageBox.Show(_i18n.T("updFail"), _i18n.T("updTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (info == null || string.IsNullOrEmpty(info.ZipUrl))
        {
            if (manual) MessageBox.Show(_i18n.T("updFail"), _i18n.T("updTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!AutoUpdateService.IsNewer(info.Tag, AutoUpdateService.CurrentTagWindows))
        {
            if (manual) MessageBox.Show(_i18n.T("updNone", AutoUpdateService.CurrentTagWindows), _i18n.T("updTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            StatusText.Text = _i18n.T("updNone", info.Tag);
            return;
        }
        string zip = Path.Combine(AutoUpdateService.UpdatesDir, $"ZAPARA_{info.Tag}_win-x64.zip");
        try
        {
            if (!File.Exists(zip))
            {
                StatusText.Text = _i18n.T("updDownloading", info.Tag);
                var svc = new AutoUpdateService();
                var prog = new Progress<double>(p => Dispatcher.Invoke(() =>
                    StatusText.Text = $"{_i18n.T("updDownloading", info.Tag)} {(int)(p * 100)}%"));
                await svc.DownloadAssetAsync(info.ZipUrl, zip, prog);
            }
            var res = MessageBox.Show(_i18n.T("updReady", info.Tag), _i18n.T("updTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (res == MessageBoxResult.Yes) await ApplyUpdateAndRestartAsync(zip);
            else StatusText.Text = _i18n.T("updReady", info.Tag);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{_i18n.T("updFail")}: {ex.Message}";
            if (manual) MessageBox.Show($"{_i18n.T("updFail")}: {ex.Message}", _i18n.T("updTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Task ApplyUpdateAndRestartAsync(string zipPath)
    {
        try
        {
            string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string batPath = Path.Combine(Path.GetTempPath(), "zapara_update.bat");
            string bat =
                "@echo off\r\n" +
                "set APPDIR=" + appDir + "\r\n" +
                ":wait\r\n" +
                "tasklist /FI \"IMAGENAME eq Vograph.exe\" 2>NUL | find /I \"Vograph.exe\" >NUL\r\n" +
                "if not errorlevel 1 (timeout /t 1 /nobreak >NUL & goto wait)\r\n" +
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath '" + zipPath.Replace("'", "''") + "' -DestinationPath '\"%APPDIR%\"' -Force\"\r\n" +
                "start \"\" \"%APPDIR%\\Vograph.exe\"\r\n" +
                "del \"%~f0\"\r\n";
            File.WriteAllText(batPath, bat, System.Text.Encoding.ASCII);
            Process.Start(new ProcessStartInfo(batPath) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            Application.Current.Shutdown();
        }
        catch (Exception ex) { StatusText.Text = $"{_i18n?.T("updFail")}: {ex.Message}"; }
        return Task.CompletedTask;
    }

    private void ChkAutoUpdate_Changed(object sender, RoutedEventArgs e)
    {
        if (_db == null || _isLoading) return;
        var s = _db.GetSettings();
        s.AutoUpdate = ChkAutoUpdate.IsChecked == true;
        _db.SaveSettings(s);
        StatusText.Text = s.AutoUpdate ? _i18n?.T("updatedOk") ?? "OK" : "Автообновление выключено";
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e) => await AutoUpdateFlowAsync(manual: true);

    protected override void OnClosed(EventArgs e)
    {
        _autoRefresh?.Dispose();
        _notifyTimer?.Stop();
        _db?.Dispose();
        base.OnClosed(e);
    }
}
