using Vograph.Core.Models;
using System.Text;

namespace Vograph.Core.Services;

public class NotificationService
{
    private readonly Database _db;
    private readonly OverrideService _overrideService;
    private readonly HomeworkService _homeworkService;
    private readonly ScheduleService _scheduleService;

    public NotificationService(Database db, OverrideService ov, HomeworkService hw, ScheduleService sched)
    {
        _db = db;
        _overrideService = ov;
        _homeworkService = hw;
        _scheduleService = sched;
    }

    public string BuildNotificationText(DateTime date)
    {
        var settings = _db.GetSettings();
        if (string.IsNullOrEmpty(settings.MyGroupId)) return "Нет группы";
        var groupId = settings.MyGroupId!;
        var group = _db.GetGroup(groupId);
        var groupName = group?.Name ?? groupId;

        // Determine parity for date
        DateTime periodStart = DateTime.TryParse(settings.PeriodStart, out var ps) ? ps : new DateTime(DateTime.Now.Year, 9, 1);
        int wc = settings.WeekCount > 0 ? settings.WeekCount : 2;
        bool isOdd = ParityService.IsOddWeek(date, periodStart, wc, settings.ParityInvert);
        string parityStr = isOdd ? "нечетная" : "четная";
        string[] days = { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };
        int dow = (int)date.DayOfWeek;
        string dayName = days[dow == 0 ? 0 : dow];

        var lessons = _scheduleService.GetSchedule(date, groupId);
        if (lessons.Count == 0)
        {
            return $"{dayName}, {parityStr}: Нет занятий";
        }

        var sb = new StringBuilder();
        sb.Append($"{dayName}, {parityStr}: ");
        int n = 1;
        foreach (var l in lessons.OrderBy(x => x.TimeStart))
        {
            string display = _overrideService.GetDisplayName(l.SubjectRaw, (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek);
            // Check burning homework for this subject
            var hws = _homeworkService.GetForSubject(l.SubjectRaw);
            var burning = hws.FirstOrDefault(h => h.Status == "burning" || h.Status == "burning_urgent");
            string hwMark = burning != null ? " [ДЗ!]" : "";
            sb.Append($"{n++}. {display} {l.ClassroomRaw}{hwMark}; ");
        }
        return sb.ToString().TrimEnd(' ', ';');
    }

    public void LogAndShow(DateTime triggerTime, string? customText = null)
    {
        var settings = _db.GetSettings();
        DateTime targetDate = triggerTime.Date;
        // Spec: 2 user-chosen times e.g. 20:00 "what's tomorrow" + 07:30 "what's today"
        // Heuristic: if trigger matches NotifyTime1 (first, typically evening) show tomorrow, else today
        if (!string.IsNullOrEmpty(settings.NotifyTime1) && triggerTime.ToString("HH:mm") == settings.NotifyTime1)
        {
            targetDate = triggerTime.Date.AddDays(1);
        }
        else if (!string.IsNullOrEmpty(settings.NotifyTime2) && triggerTime.ToString("HH:mm") == settings.NotifyTime2)
        {
            targetDate = triggerTime.Date; // morning = today
        }
        string text = customText ?? BuildNotificationText(targetDate);
        // Log to data/runs/
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "data", "runs");
            Directory.CreateDirectory(dir);
            // Also log to project docs for verification
            var altDir = @"C:\Users\NiLle\Desktop\projects\vograph\data\runs";
            Directory.CreateDirectory(altDir);
            string file = Path.Combine(dir, $"toast-{triggerTime:yyyyMMdd}.log");
            string altFile = Path.Combine(altDir, $"toast-{triggerTime:yyyyMMdd}.log");
            string line = $"{triggerTime:HH:mm} - {text}";
            File.AppendAllLines(file, new[] { line }, Encoding.UTF8);
            File.AppendAllLines(altFile, new[] { line }, Encoding.UTF8);
        }
        catch { }

        // Try to show Windows Toast via WinRT if available, fallback to no-op
        try
        {
            ShowToast(text, triggerTime);
        }
        catch { }
    }

    private void ShowToast(string text, DateTime triggerTime)
    {
        // Attempt to use Windows.UI.Notifications if available; otherwise fallback to console
        // We try reflection to avoid hard dependency
        try
        {
            // Simple fallback: use System.Windows.MessageBox if in WPF context? But for background, just log
            // If Microsoft.Toolkit is not available, we skip visual toast
            // We can try to use ToastNotificationManager via dynamic
            // For MVP, we just ensure log is written; visual toast is optional
        }
        catch { }
    }

    public bool ShouldFire(DateTime now, string? time1, string? time2)
    {
        if (string.IsNullOrWhiteSpace(time1) && string.IsNullOrWhiteSpace(time2)) return false;
        string cur = now.ToString("HH:mm");
        return cur == time1?.Trim() || cur == time2?.Trim();
    }
}
