using Vograph.Core.Services;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== I18nService test ===");
var i18n = new I18nService("ru");
Console.WriteLine($"ru today={i18n.T("today")} tomorrow={i18n.T("tomorrow")} week={i18n.T("week")} oddBadge={i18n.T("oddBadge")} noLessons={i18n.T("noLessons")}");
bool ruOk = i18n.T("today")=="Сегодня" && i18n.T("tomorrow")=="Завтра";
Console.WriteLine($"RU check {(ruOk?"PASS":"FAIL")}");
i18n.SetLanguage("en");
Console.WriteLine($"en today={i18n.T("today")} tomorrow={i18n.T("tomorrow")} week={i18n.T("week")} oddBadge={i18n.T("oddBadge")} noLessons={i18n.T("noLessons")}");
bool enOk = i18n.T("today")=="Today" && i18n.T("tomorrow")=="Tomorrow";
Console.WriteLine($"EN check {(enOk?"PASS":"FAIL")}");
i18n.SetLanguage("ru");
Console.WriteLine($"switch back ru today={i18n.T("today")} {(i18n.T("today")=="Сегодня"?"PASS":"FAIL")}");

// Date formatting
var dt = new DateTime(2026,9,2);
Console.WriteLine($"ru date {i18n.FormatDate(dt)} day {i18n.FormatDay(dt)} full {i18n.FormatDayFull(dt)} parity {i18n.FormatParity(true)}");
i18n.SetLanguage("en");
Console.WriteLine($"en date {i18n.FormatDate(dt)} day {i18n.FormatDay(dt)} full {i18n.FormatDayFull(dt)} parity {i18n.FormatParity(true)}");
bool dateOk = i18n.FormatDate(dt)=="2026-09-02" && i18n.FormatDay(dt)=="Wed";
Console.WriteLine($"EN date format {(dateOk?"PASS":"FAIL")}");

Console.WriteLine("\n=== DB language persistence ===");
string dbPath = Path.Combine(Path.GetTempPath(), "vograph_i18n_test.db");
if (File.Exists(dbPath)) File.Delete(dbPath);
using (var db = new Database(dbPath))
{
    var s = db.GetSettings();
    Console.WriteLine($"default language {s.Language} {(s.Language=="ru"?"PASS":"FAIL")}");
    s.Language = "en";
    db.SaveSettings(s);
    var s2 = new Database(dbPath).GetSettings();
    Console.WriteLine($"persisted en {s2.Language} {(s2.Language=="en"?"PASS":"FAIL")}");
    // test service respects settings
    var svc = new I18nService(s2.Language);
    Console.WriteLine($"service en today {svc.T("today")} {(svc.T("today")=="Today"?"PASS":"FAIL")}");
}

Console.WriteLine("\n=== AutoRefreshService test (If-Modified-Since) ===");
string dbPath2 = Path.Combine(Path.GetTempPath(), "vograph_autorefresh_test.db");
if (File.Exists(dbPath2)) File.Delete(dbPath2);
if (File.Exists(dbPath2+"-wal")) File.Delete(dbPath2+"-wal");
using (var db = new Database(dbPath2))
{
    var parser = new ParserService(db);
    var ar = new AutoRefreshService(db, parser);
    // Ensure clean log
    string logFile = Path.Combine(AppContext.BaseDirectory, "data", "runs", $"autorefresh-{DateTime.Now:yyyyMMdd}.log");
    string altLog = Path.Combine(@"C:\Users\NiLle\Desktop\projects\vograph\data\runs", $"autorefresh-{DateTime.Now:yyyyMMdd}.log");
    try { File.Delete(logFile); } catch {}
    try { File.Delete(altLog); } catch {}
    // First check should do HEAD and log
    bool changed = await ar.CheckAsync(true);
    Console.WriteLine($"CheckAsync startup changed={changed}");
    // Check log exists
    bool logExists = File.Exists(logFile) || File.Exists(altLog);
    Console.WriteLine($"Log exists {logExists} {(logExists?"PASS":"FAIL")}");
    if (File.Exists(altLog)) Console.WriteLine(File.ReadAllText(altLog, Encoding.UTF8));
    else if (File.Exists(logFile)) Console.WriteLine(File.ReadAllText(logFile, Encoding.UTF8));
    var settings = db.GetSettings();
    Console.WriteLine($"LastAutoCheckAt {settings.LastAutoCheckAt} {(string.IsNullOrEmpty(settings.LastAutoCheckAt)?"FAIL":"PASS")}");
    // Second immediate check should be skipped (respect 24h) if not startup
    bool changed2 = await ar.CheckAsync(false);
    Console.WriteLine($"Second CheckAsync (non-startup, <24h) changed={changed2} should be false (skip) {(changed2==false?"PASS":"FAIL")}");
    ar.Dispose();
}

Console.WriteLine("\n=== Notification localization ===");
string dbPath3 = Path.Combine(Path.GetTempPath(), "vograph_notif_i18n_test.db");
if (File.Exists(dbPath3)) File.Delete(dbPath3);
using (var db = new Database(dbPath3))
{
    var parser = new ParserService(db);
    string xml = File.ReadAllText(@"C:\Users\NiLle\AppData\Local\Temp\opencode\TimetableGroup50.xml", Encoding.UTF8);
    await parser.RefreshAsync(xmlOverride: xml);
    var settings = db.GetSettings();
    settings.MyGroupId = "3313";
    settings.Language = "ru";
    db.SaveSettings(settings);
    var ov = new OverrideService(db);
    var hw = new HomeworkService(db);
    var sched = new ScheduleService(db);
    var i18nRu = new I18nService("ru");
    var notifRu = new NotificationService(db, ov, hw, sched, i18nRu);
    string textRu = notifRu.BuildNotificationText(new DateTime(2026,9,2));
    Console.WriteLine($"RU notif: {textRu}");
    bool ruNotifs = textRu.Contains("Ср") || textRu.Contains("нечет");
    Console.WriteLine($"RU contains Cyrillic {(ruNotifs?"PASS":"FAIL")}");
    var i18nEn = new I18nService("en");
    var notifEn = new NotificationService(db, ov, hw, sched, i18nEn);
    string textEn = notifEn.BuildNotificationText(new DateTime(2026,9,2));
    Console.WriteLine($"EN notif: {textEn}");
    bool enNotifs = textEn.Contains("Wed") || textEn.Contains("odd");
    Console.WriteLine($"EN contains English {(enNotifs?"PASS":"FAIL")}");
    bool langDiff = textRu != textEn;
    Console.WriteLine($"RU vs EN different {(langDiff?"PASS":"FAIL")}");
}

Console.WriteLine("\n=== Sync language ===");
string dbPath4 = Path.Combine(Path.GetTempPath(), "vograph_sync_lang_test.db");
if (File.Exists(dbPath4)) File.Delete(dbPath4);
using (var db = new Database(dbPath4))
{
    var s = db.GetSettings();
    s.Language = "en";
    db.SaveSettings(s);
    var sync = new SyncService(db);
    string json = sync.ExportToJson();
    Console.WriteLine($"Export contains language en {json.Contains("\"Language\": \"en\"")}");
    // Import to another db with ru default - we preserve receiver's choice per docs/API.md §9 (keep receiver's language)
    string dbPath5 = Path.Combine(Path.GetTempPath(), "vograph_sync_lang_test2.db");
    if (File.Exists(dbPath5)) File.Delete(dbPath5);
    using (var db2 = new Database(dbPath5))
    {
        var sync2 = new SyncService(db2);
        sync2.ImportFromJson(json);
        var s2 = db2.GetSettings();
        Console.WriteLine($"Imported language {s2.Language} {(s2.Language=="ru"?"PASS (keep receiver's)":"FAIL")} (documented keep receiver's choice)");
    }
}

Console.WriteLine("\nAll i18n/auto-refresh checks done");
