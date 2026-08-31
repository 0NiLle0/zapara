using Vograph.Core.Services;
using Vograph.Core.Models;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var dbPath = Path.Combine(AppContext.BaseDirectory, "vograph_phase3.db");
if (File.Exists(dbPath)) File.Delete(dbPath);
if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");
if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");
Console.WriteLine($"DB {dbPath}");
using var db = new Database(dbPath);
var parser = new ParserService(db);
var schedule = new ScheduleService(db);
var ovService = new OverrideService(db);
var hwService = new HomeworkService(db);
string xml = File.ReadAllText(@"C:\Users\NiLle\AppData\Local\Temp\opencode\TimetableGroup50.xml", Encoding.UTF8);
await parser.RefreshAsync(xmlOverride: xml);
Console.WriteLine("Parsed");
var settings = db.GetSettings();
settings.MyGroupId = "3313";
settings.PeriodStart = "2026-09-01";
settings.WeekCount = 2;
db.SaveSettings(settings);
Console.WriteLine($"Group {settings.MyGroupId}");

Console.WriteLine("\n--- Overrides test ---");
var subj = "лек ВЫСШ. МАТЕМАТ";
var norm = ParityService.NormalizeSubject(subj);
Console.WriteLine($"Norm {norm}");
ovService.AddOrUpdate(subj, "global", "МатАн (переименовано)", "сноска тест");
var dispGlobal = ovService.GetDisplayName(subj, 1);
Console.WriteLine($"Global display Monday: {dispGlobal} (expected МатАн)");
var subj2 = "пр ОСН РОС ГОС";
ovService.AddOrUpdate(subj2, "weekday:1", "ОРГ Monday only", null);
Console.WriteLine($"Weekday 1 display for {subj2}: {ovService.GetDisplayName(subj2,1)} (expected ОРГ)");
Console.WriteLine($"Weekday 2 display for {subj2}: {ovService.GetDisplayName(subj2,2)} (expected original)");
Console.WriteLine($"Overrides count {db.GetOverrides().Count}");

Console.WriteLine("\n--- Homework N=2 test ---");
var created = new DateTime(2026,9,2);
long hwId = hwService.AddHomework(subj, "Решить задачи 1-5", 2, created);
var hw = hwService.GetById(hwId);
Console.WriteLine($"HW N=2 created {created:yyyy-MM-dd} subject {subj} due {hw.DueDateComputed:yyyy-MM-dd dddd} (expected 2026-09-14)");
var dueExpected = new DateTime(2026,9,14);
if (hw.DueDateComputed?.Date == dueExpected.Date) Console.WriteLine("PASS N=2 due correct");
else Console.WriteLine($"FAIL due {hw.DueDateComputed} vs {dueExpected}");

Console.WriteLine("\n--- Status transitions ---");
Console.WriteLine($"HW status now (today={DateTime.Today:yyyy-MM-dd}) = {hw.Status} due {hw.DueDateComputed:yyyy-MM-dd}");
hwService.MarkDone(hwId, true);
var hwDone = hwService.GetById(hwId);
Console.WriteLine($"After mark done status {hwDone.Status} (expected done) doneAt {hwDone.DoneAt}");
hwService.MarkDone(hwId, false);
var hwUn = hwService.GetById(hwId);
Console.WriteLine($"After unmark status {hwUn.Status}");

Console.WriteLine("\n--- Rename survives refresh ---");
await parser.RefreshAsync(xmlOverride: xml);
var afterOv = db.GetOverrides();
Console.WriteLine($"Overrides after refresh {afterOv.Count} (expected 2)");
var dispAfter = ovService.GetDisplayName(subj,1);
Console.WriteLine($"Display after refresh {dispAfter} (expected МатАн (переименовано))");
if (dispAfter=="МатАн (переименовано)") Console.WriteLine("PASS rename survives");
else Console.WriteLine("FAIL");

Console.WriteLine($"Lessons after refresh {db.GetAllLessonsForGroup("3313").Count}");

Console.WriteLine("\nPhase3 verification COMPLETE");
