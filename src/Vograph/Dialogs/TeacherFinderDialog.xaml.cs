using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vograph.Core.Services;
using Vograph.Helpers;

namespace Vograph.Dialogs;

public partial class TeacherFinderDialog : Window
{
    private readonly Database _db;
    private readonly I18nService _i18n;
    private readonly string _groupId;
    private readonly LecturerService _lectService;
    private readonly HashSet<string> _myTeacherIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _myTeacherShortNames = new(StringComparer.OrdinalIgnoreCase);
    private string _search = "";
    private string _subjectFilter = "";
    private bool _onlyMy = true;

    public TeacherFinderDialog(Database db, I18nService i18n, string groupId)
    {
        InitializeComponent();
        _db = db;
        _i18n = i18n;
        _groupId = groupId;
        _lectService = new LecturerService(db);
        SourceInitialized += (s, e) => DarkModeHelper.EnableDarkTitleBar(this);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var g = _db.GetGroup(_groupId);
            LblGroup.Text = g != null ? $"Группа {g.Name}" : _groupId;
            LblCount.Text = _i18n.Language == "en" ? "Loading teachers..." : "Загрузка преподавателей...";
            DetailsPlaceholder.Text = _i18n.Language == "en" ? "Loading..." : "Загрузка...";
        // Collect my teachers from student's group lessons for filter
        try
        {
            var myLessons = _db.GetAllLessonsForGroup(_groupId);
            foreach (var l in myLessons)
            {
                if (string.IsNullOrWhiteSpace(l.TeacherRaw) || l.TeacherRaw == "—") continue;
                foreach (var t in l.TeacherRaw.Split(';').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                {
                    _myTeacherShortNames.Add(t);
                    // also add normalized parts for matching
                    var parts = t.Split(' ');
                    if (parts.Length > 0) _myTeacherShortNames.Add(parts[0]);
                }
            }
        }
        catch {}

        // Load lecturer schedule (cached + fetch)
        try
        {
            await _lectService.LoadAsync();
        }
        catch (Exception ex)
        {
            LblCount.Text = $"Ошибка загрузки: {ex.Message}";
        }

        // Build myTeacherIds set by matching ShortNames to LecturerInfo
        try
        {
            foreach (var lect in _lectService.Lecturers)
            {
                // lect.Name is full, e.g. "Барт Елена Леонидовна", match to short "Барт Е.Л."
                foreach (var shortName in _myTeacherShortNames)
                {
                    // shortName like "Барт Е.Л." -> last name is before space
                    var lastName = shortName.Split(' ')[0].TrimEnd('.');
                    if (!string.IsNullOrEmpty(lastName) && lect.Name.Contains(lastName, StringComparison.OrdinalIgnoreCase))
                    {
                        _myTeacherIds.Add(lect.Id);
                        _myTeacherIds.Add(lect.Name);
                        break;
                    }
                    if (lect.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase)) _myTeacherIds.Add(lect.Id);
                }
            }
            // also add short names themselves for direct match when lecturer XML missing
            foreach (var s in _myTeacherShortNames) _myTeacherIds.Add(s);
        }
        catch {}

        // Fill subject filter from lecturer lessons (all subjects)
        var subjects = _lectService.Lessons.Select(l => l.DisciplineRaw).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
        if (subjects.Count == 0)
        {
            // fallback to group lessons subjects
            subjects = _db.GetAllLessonsForGroup(_groupId).Select(l => l.SubjectRaw).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
        }
        subjects.Insert(0, _i18n.Language == "en" ? "All subjects" : "Все предметы");
        CmbSubjectFilter.ItemsSource = subjects;
        CmbSubjectFilter.SelectedIndex = 0;
        ChkOnlyMy.IsChecked = true;
        _onlyMy = true;
        TxtSearch.Text = "";
        RefreshTeacherList();
        ApplyI18n();
        UpdateCount();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки преподавателей:\n{ex.Message}", "Преподаватели", MessageBoxButton.OK, MessageBoxImage.Error);
            LblCount.Text = $"Ошибка: {ex.Message}";
        }
    }

    private void UpdateCount()
    {
        LblCount.Text = $"{_lectService.Lecturers.Count} преподавателей всего, {_lectService.Lessons.Count} пар в расписании преподов" + (_onlyMy ? $" · мои: {_myTeacherIds.Count}" : "");
        if (_i18n.Language == "en") LblCount.Text = $"{_lectService.Lecturers.Count} lecturers, {_lectService.Lessons.Count} lessons" + (_onlyMy ? $" · mine: {_myTeacherIds.Count}" : "");
    }

    private void ApplyI18n()
    {
        Title = _i18n.Language == "en" ? "Teachers" : "Преподаватели";
        LblTitle.Text = _i18n.Language == "en" ? "TEACHERS" : "ПРЕПОДАВАТЕЛИ";
        TxtSearch.ToolTip = _i18n.Language == "en" ? "Search by teacher or subject" : "Поиск по преподавателю или предмету";
        BtnClose.Content = _i18n.T("cancel") == "Отмена" ? "Закрыть" : "Close";
        DetailsPlaceholder.Text = _i18n.Language == "en" ? "Select a teacher on the left" : "Выберите преподавателя слева";
        if (ChkOnlyMy != null) ChkOnlyMy.Content = _i18n.Language == "en" ? "Only mine" : "Только мои";
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = TxtSearch.Text?.Trim().ToLowerInvariant() ?? "";
        RefreshTeacherList();
    }

    private void SubjectFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        _subjectFilter = CmbSubjectFilter.SelectedItem as string ?? "";
        if (_subjectFilter == "Все предметы" || _subjectFilter == "All subjects") _subjectFilter = "";
        RefreshTeacherList();
    }

    private void OnlyMy_Changed(object sender, RoutedEventArgs e)
    {
        _onlyMy = ChkOnlyMy.IsChecked == true;
        RefreshTeacherList();
        UpdateCount();
    }

    private void RefreshTeacherList()
    {
        TeacherListPanel.Children.Clear();
        if (!_lectService.IsLoaded)
        {
            TeacherListPanel.Children.Add(new TextBlock { Text = _i18n.Language == "en" ? "Loading..." : "Загрузка...", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, Margin = new Thickness(0,10,0,0), HorizontalAlignment = HorizontalAlignment.Center });
            return;
        }
        var lecturers = _lectService.Lecturers.AsEnumerable();
        if (_onlyMy)
        {
            lecturers = lecturers.Where(l => _myTeacherIds.Contains(l.Id) || _myTeacherIds.Contains(l.Name) || _myTeacherShortNames.Any(s => l.Name.Contains(s.Split(' ')[0], StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrEmpty(_search))
        {
            lecturers = lecturers.Where(l => l.Name.ToLowerInvariant().Contains(_search) || l.Id.Contains(_search) || (l.Kafedra != null && l.Kafedra.ToLowerInvariant().Contains(_search)) || _lectService.GetLessonsForLecturer(l.Id).Any(ll => ll.DisciplineRaw.ToLowerInvariant().Contains(_search)));
        }
        if (!string.IsNullOrEmpty(_subjectFilter))
        {
            lecturers = lecturers.Where(l => _lectService.GetLessonsForLecturer(l.Id).Any(ll => ll.DisciplineRaw.Equals(_subjectFilter, StringComparison.OrdinalIgnoreCase) || ll.SubjectRaw.Equals(_subjectFilter, StringComparison.OrdinalIgnoreCase)));
        }
        var filtered = lecturers.OrderBy(l => l.Name).ToList();

        foreach (var lect in filtered)
        {
            var lessons = _lectService.GetLessonsForLecturer(lect.Id);
            var count = lessons.Count;
            var kaf = string.IsNullOrWhiteSpace(lect.Kafedra) ? "" : $" · {lect.Kafedra.Trim()}";
            var subjects = string.Join(", ", lessons.Select(l => l.DisciplineRaw).Distinct().Take(2));
            if (lessons.Select(l => l.DisciplineRaw).Distinct().Count() > 2) subjects += $" +{lessons.Select(l => l.DisciplineRaw).Distinct().Count() - 2}";

            var border = new Border
            {
                Style = (Style)FindResource("Card"),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(7),
                Background = (Brush)FindResource("PanelAlt"),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderBrush = _myTeacherIds.Contains(lect.Id) || _myTeacherShortNames.Any(s => lect.Name.Contains(s.Split(' ')[0], StringComparison.OrdinalIgnoreCase)) ? (Brush)FindResource("Bronze") : (Brush)FindResource("BorderDim"),
                BorderThickness = _myTeacherIds.Contains(lect.Id) ? new Thickness(1) : new Thickness(1)
            };
            var stack = new StackPanel();
            var tbName = new TextBlock { Text = lect.Name, Foreground = (Brush)FindResource("Marble"), FontSize = 11, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var tbMeta = new TextBlock { Text = $"{count} пар{kaf} · {subjects}", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,2,0,0) };
            var odd = lessons.Count(l => l.Parity == 1);
            var even = lessons.Count(l => l.Parity == 2);
            var tbParity = new TextBlock { Text = $"нечет {odd} / чет {even} · {lect.Id}", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, Margin = new Thickness(0,2,0,0) };
            if (_i18n.Language == "en") tbParity.Text = $"odd {odd} / even {even} · {lect.Id}";
            stack.Children.Add(tbName);
            stack.Children.Add(tbMeta);
            stack.Children.Add(tbParity);
            border.Child = stack;
            var lectCopy = lect;
            border.MouseLeftButtonUp += (s, e) => ShowTeacherDetails(lectCopy);
            if (TeacherListPanel.Children.Count == 0)
                Dispatcher.BeginInvoke(new Action(() => ShowTeacherDetails(lectCopy)), System.Windows.Threading.DispatcherPriority.Loaded);
            TeacherListPanel.Children.Add(border);
        }
        if (filtered.Count == 0)
        {
            TeacherListPanel.Children.Add(new TextBlock { Text = _i18n.Language == "en" ? "No teachers found" : "Не найдено", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, Margin = new Thickness(0,10,0,0), HorizontalAlignment = HorizontalAlignment.Center });
        }
        // Update count to show filtered
        LblCount.Text = $"{filtered.Count} / {_lectService.Lecturers.Count}" + (_onlyMy ? " (мои)" : "");
        if (_i18n.Language == "en") LblCount.Text = $"{filtered.Count} / {_lectService.Lecturers.Count}" + (_onlyMy ? " (mine)" : "");
    }

    private void ShowTeacherDetails(LecturerInfo lect)
    {
        DetailsPanel.Children.Clear();
        var lessons = _lectService.GetLessonsForLecturer(lect.Id);
        var header = new TextBlock { Text = lect.Name, Foreground = (Brush)FindResource("Marble"), FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,2), TextWrapping = TextWrapping.Wrap };
        DetailsPanel.Children.Add(header);
        var kafText = new TextBlock { Text = string.IsNullOrWhiteSpace(lect.Kafedra) ? $"ID {lect.Id}" : $"Каф. {lect.Kafedra} · ID {lect.Id}", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10, Margin = new Thickness(0,0,0,4) };
        DetailsPanel.Children.Add(kafText);
        bool isMy = _myTeacherIds.Contains(lect.Id) || _myTeacherIds.Contains(lect.Name);
        var myBadge = new TextBlock { Text = isMy ? (_i18n.Language=="en" ? "Teaches your group" : "Ведет у вашей группы") : (_i18n.Language=="en" ? "Not in your group" : "Не ведет у вашей группы"), Foreground = isMy ? (Brush)FindResource("Patina") : (Brush)FindResource("MarbleDim"), FontSize = 9, FontStyle = FontStyles.Italic, Margin = new Thickness(0,0,0,8) };
        DetailsPanel.Children.Add(myBadge);

        if (lessons.Count == 0)
        {
            DetailsPanel.Children.Add(new TextBlock { Text = _i18n.Language=="en" ? "No lessons" : "Нет пар", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 11, Margin = new Thickness(0,10,0,0) });
            return;
        }

        // Group by subject
        var bySubj = lessons.GroupBy(l => l.DisciplineRaw).OrderBy(g => g.Key).ToList();
        var tbSubs = new TextBlock { Text = $"{bySubj.Count} предмет(ов): {string.Join(", ", bySubj.Select(g => g.Key).Take(5))}" + (bySubj.Count>5 ? $" +{bySubj.Count-5}" : ""), Foreground = (Brush)FindResource("MarbleDim"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) };
        DetailsPanel.Children.Add(tbSubs);

        foreach (var grp in bySubj)
        {
            var subjBorder = new Border { Background = (Brush)FindResource("PanelAlt"), BorderBrush = (Brush)FindResource("BorderDim"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(7), Margin = new Thickness(0,0,0,8) };
            var stack = new StackPanel();
            var subjTitle = new TextBlock { Text = grp.Key, Foreground = (Brush)FindResource("Bronze"), FontSize = 11, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            stack.Children.Add(subjTitle);
            var ordered = grp.OrderBy(l => l.DayOfWeek).ThenBy(l => l.Parity).ThenBy(l => l.TimeStart).ToList();
            foreach (var l in ordered)
            {
                string day = ParityService.DayNumberToTitle(l.DayOfWeek);
                if (_i18n.Language == "en") day = _i18n.T(l.DayOfWeek switch {1=>"mon",2=>"tue",3=>"wed",4=>"thu",5=>"fri",6=>"sat",_=>"mon"});
                string parity = l.Parity == 1 ? (_i18n.Language=="en" ? "odd" : "нечет") : l.Parity == 2 ? (_i18n.Language=="en" ? "even" : "чет") : "both";
                string when = $"{day} {l.TimeStart}-{l.TimeEnd} ({parity})";
                string where = string.IsNullOrWhiteSpace(l.ClassroomRaw) ? "—" : l.ClassroomRaw.Trim();
                string groups = l.Groups.Count > 0 ? string.Join(", ", l.Groups.Select(g => g.Number).Where(n => !string.IsNullOrEmpty(n)).Take(4)) : "";
                if (l.Groups.Count > 4) groups += $" +{l.Groups.Count-4}";
                string groupPart = string.IsNullOrEmpty(groups) ? "" : $" · группы {groups}";
                if (_i18n.Language == "en") groupPart = string.IsNullOrEmpty(groups) ? "" : $" · groups {groups}";
                string bld = string.IsNullOrWhiteSpace(l.BuildingRaw) ? "" : $" · {l.BuildingRaw}";
                string mapHint = MapHint(l);
                var row = new TextBlock { Text = $"{when} · ауд. {where}{bld}{groupPart} {mapHint}", Foreground = (Brush)FindResource("Marble"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,3,0,0) };
                if (_i18n.Language == "en") row.Text = $"{when} · room {where}{bld}{groupPart} {mapHint}";
                // Highlight if this lesson is for student's group
                bool isMyGroup = l.Groups.Any(g => g.IdGroup == _groupId || g.Number == _db.GetGroup(_groupId)?.Name);
                if (isMyGroup) row.FontWeight = FontWeights.SemiBold;
                if (isMyGroup) row.Foreground = (Brush)FindResource("Patina");
                stack.Children.Add(row);
            }
            var tbCount = new TextBlock { Text = $"{ordered.Count} пар по этому предмету", Foreground = (Brush)FindResource("MarbleDim"), FontSize = 9, Margin = new Thickness(0,4,0,0), FontStyle = FontStyles.Italic };
            if (_i18n.Language == "en") tbCount.Text = $"{ordered.Count} lessons for this subject";
            stack.Children.Add(tbCount);
            subjBorder.Child = stack;
            DetailsPanel.Children.Add(subjBorder);
        }
        var total = new TextBlock { Text = $"Всего у {lect.Name}: {lessons.Count} пар (по всем группам)", Foreground = (Brush)FindResource("Patina"), FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,4,0,0), TextWrapping = TextWrapping.Wrap };
        if (_i18n.Language == "en") total.Text = $"Total for {lect.Name}: {lessons.Count} lessons (all groups)";
        DetailsPanel.Children.Add(total);
        // Also show my group lessons count
        var myCount = lessons.Count(l => l.Groups.Any(g => g.IdGroup == _groupId));
        if (myCount > 0)
        {
            var myText = new TextBlock { Text = $"Из них у вашей группы {_db.GetGroup(_groupId)?.Name ?? _groupId}: {myCount} пар", Foreground = (Brush)FindResource("Bronze"), FontSize = 9, Margin = new Thickness(0,2,0,0) };
            if (_i18n.Language == "en") myText.Text = $"Of those for your group {_db.GetGroup(_groupId)?.Name ?? _groupId}: {myCount} lessons";
            DetailsPanel.Children.Add(myText);
        }
    }

    private string MapHint(LecturerLesson l)
    {
        bool hasStar = l.ClassroomRaw?.Contains("*") ?? false;
        string building = l.ClassroomRaw?.Contains("ВЦ") == true ? "ВЦ" : hasStar ? "УЛК" : "ГК";
        if (l.ClassroomRaw?.ToLower().Contains("дистанционно") == true) return "(дистанционно)";
        var m = System.Text.RegularExpressions.Regex.Match(l.RoomRaw ?? "", @"\d+");
        int floor = 1;
        if (m.Success && m.Value.Length > 0) floor = int.Parse(m.Value[0].ToString());
        return $"· {building} {floor} этаж";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
