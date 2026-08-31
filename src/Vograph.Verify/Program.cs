using Vograph.Core.Services;
using Vograph.Core.Models;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

// Phase 1 verification: Parser + DB
var dbPath = Path.Combine(AppContext.BaseDirectory, "vograph_verify.db");
if (File.Exists(dbPath)) File.Delete(dbPath);
Console.WriteLine($"DB path: {dbPath}");

using var db = new Database(dbPath);
var parser = new ParserService(db);
var schedule = new ScheduleService(db);

// Use cached xml if available, else fetch
string xmlPath = @"C:\Users\NiLle\AppData\Local\Temp\opencode\TimetableGroup50.xml";
string xml;
if (File.Exists(xmlPath))
{
    xml = File.ReadAllText(xmlPath, Encoding.UTF8);
    // detect BOM for UTF-16 archives - but current is UTF8
    Console.WriteLine($"Using cached XML from {xmlPath}, len {xml.Length}");
}
else
{
    Console.WriteLine("Fetching remote XML...");
    var (fetched, _) = await parser.FetchXmlAsync();
    xml = fetched;
    Console.WriteLine($"Fetched {xml.Length} chars");
}

// Parse and refresh
var (periodStart, weekCount, periodTitle) = await parser.RefreshAsync(xmlOverride: xml);
Console.WriteLine($"Period: {periodTitle} Start={periodStart:yyyy-MM-dd} WeekCount={weekCount}");

// Verify group 3313 (А863С) exists
var groups = db.GetAllGroups();
Console.WriteLine($"Total groups in DB: {groups.Count}");
var targetGroup = db.GetGroup("3313");
if (targetGroup == null)
{
    Console.WriteLine("ERROR: Group 3313 not found!");
    Environment.Exit(1);
}
Console.WriteLine($"Target group 3313: Name={targetGroup.Name} Id={targetGroup.Id}");

var allLessons = db.GetAllLessonsForGroup("3313");
Console.WriteLine($"Total lessons for 3313: {allLessons.Count}");
foreach (var l in allLessons.OrderBy(x => x.DayOfWeek).ThenBy(x => x.Parity).ThenBy(x => x.Index))
{
    Console.WriteLine($"  D{l.DayOfWeek} W{l.Parity} #{l.Index} {l.TimeStart}-{l.TimeEnd} | {l.SubjectRaw} | {l.TeacherRaw} | {l.ClassroomRaw} (room={l.RoomRaw} building={l.BuildingRaw} type={l.TypeRaw})");
}

// Verify both weeks
var settings = db.GetSettings();
Console.WriteLine($"Settings periodStart={settings.PeriodStart} weekCount={settings.WeekCount}");

// Simulate dates for odd/even weeks
// Period start 2026-09-01 is Tuesday, Monday is 2026-08-31
// So 2026-09-02 (Wednesday) should be week 1 odd, 2026-09-08 (Tuesday next week) should be week 2 even
var dOdd = new DateTime(2026, 9, 2); // Wednesday of first week
var dEven = new DateTime(2026, 9, 9); // Wednesday of second week
int wcOdd = ParityService.GetWeekCode(dOdd, periodStart, weekCount);
int wcEven = ParityService.GetWeekCode(dEven, periodStart, weekCount);
Console.WriteLine($"WeekCode for {dOdd:yyyy-MM-dd} = {wcOdd} (expected 1 odd)");
Console.WriteLine($"WeekCode for {dEven:yyyy-MM-dd} = {wcEven} (expected 2 even)");
Console.WriteLine($"IsOdd {dOdd:yyyy-MM-dd} = {ParityService.IsOddWeek(dOdd, periodStart, weekCount, false)}");
Console.WriteLine($"IsOdd {dEven:yyyy-MM-dd} = {ParityService.IsOddWeek(dEven, periodStart, weekCount, false)}");
if (wcOdd != 1 || wcEven != 2) Console.WriteLine("WARNING parity mismatch");

// Get schedule for specific days
for (int dow = 1; dow <= 6; dow++)
{
    var oddLessons = db.GetLessons("3313", dow, 1);
    var evenLessons = db.GetLessons("3313", dow, 2);
    Console.WriteLine($"Day {ParityService.DayNumberToTitle(dow)} ({dow}): odd={oddLessons.Count} even={evenLessons.Count}");
    if (oddLessons.Count > 0)
        Console.WriteLine($"  ODD example: {string.Join(" | ", oddLessons.Take(1).Select(x => $"{x.TimeStart} {x.SubjectRaw} {x.RoomRaw}"))}");
    if (evenLessons.Count > 0)
        Console.WriteLine($"  EVEN example: {string.Join(" | ", evenLessons.Take(1).Select(x => $"{x.TimeStart} {x.SubjectRaw} {x.RoomRaw}"))}");
}

// Verify overrides persistence
Console.WriteLine("\n--- Testing overrides persistence ---");
var ov = new Override
{
    SubjectRawNormalized = ParityService.NormalizeSubject("лек ВЫСШ. МАТЕМАТ"),
    Scope = "global",
    DisplayName = "Матан (переименовано)",
    Note = "test note",
    CreatedAt = DateTime.UtcNow
};
var oid = db.InsertOverride(ov);
Console.WriteLine($"Inserted override id={oid} normalized={ov.SubjectRawNormalized}");
var beforeCount = db.GetOverrides().Count;
Console.WriteLine($"Overrides before re-parse: {beforeCount}");

// Re-parse same xml (simulate site refresh)
await parser.RefreshAsync(xmlOverride: xml);
var afterCount = db.GetOverrides().Count;
Console.WriteLine($"Overrides after re-parse: {afterCount}");
if (beforeCount != afterCount) Console.WriteLine("ERROR: overrides wiped!");
else Console.WriteLine("PASS: overrides not wiped on re-parse");

// Also verify lessons still there after re-parse
var lessonsAfter = db.GetAllLessonsForGroup("3313");
Console.WriteLine($"Lessons after re-parse: {lessonsAfter.Count} (before {allLessons.Count})");
if (lessonsAfter.Count != allLessons.Count) Console.WriteLine("WARNING lesson count diff");

// Create verification dump files
var verifyDir = @"C:\Users\NiLle\Desktop\projects\vograph\docs\verify_phase1";
Directory.CreateDirectory(verifyDir);
// Dump HTML-like table for odd/even
void DumpWeek(int weekCode, string suffix)
{
    var sb = new StringBuilder();
    sb.AppendLine("<html><head><meta charset=\"utf-8\"><style>body{font-family:Consolas,monospace;background:#0E1013;color:#C5CAD3} table{border-collapse:collapse;width:100%} th{color:#6CA5E0} td{border:1px solid #262B33;padding:4px} tr.odd{background:#15181D} </style></head><body>");
    sb.AppendLine($"<h2>Group {targetGroup.Name} (Id 3313) — {(weekCode == 1 ? "Нечетная (odd)" : "Четная (even)")} — Period {periodTitle} Start {periodStart:yyyy-MM-dd}</h2>");
    sb.AppendLine($"<p>Generated {DateTime.Now:yyyy-MM-dd HH:mm} parity invert={settings.ParityInvert}</p>");
    for (int d = 1; d <= 6; d++)
    {
        var lessons = db.GetLessons("3313", d, weekCode);
        sb.AppendLine($"<h3>{ParityService.DayNumberToTitle(d)} — {lessons.Count} lessons</h3>");
        sb.AppendLine("<table><tr><th>No.</th><th>Time</th><th>Subject</th><th>Teacher</th><th>Room/Building</th><th>Type</th></tr>");
        int n = 1;
        foreach (var l in lessons)
        {
            sb.AppendLine($"<tr><td>{n++}</td><td>{l.TimeStart}-{l.TimeEnd}</td><td>{System.Net.WebUtility.HtmlEncode(l.SubjectRaw)}</td><td>{System.Net.WebUtility.HtmlEncode(l.TeacherRaw)}</td><td>{System.Net.WebUtility.HtmlEncode(l.ClassroomRaw)} (room:{l.RoomRaw} b:{l.BuildingRaw})</td><td>{l.TypeRaw}</td></tr>");
        }
        if (lessons.Count == 0) sb.AppendLine("<tr><td colspan=\"6\" style=\"color:#6B7280\">No lessons</td></tr>");
        sb.AppendLine("</table>");
    }
    sb.AppendLine("</body></html>");
    var path = Path.Combine(verifyDir, $"O3313_{suffix}.html");
    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    Console.WriteLine($"Wrote {path} ({sb.Length} chars)");

    // also csv dump
    var csv = new StringBuilder();
    csv.AppendLine("Day,WeekCode,TimeStart,TimeEnd,Subject,Teacher,Room,Building,Type,ClassroomRaw");
    for (int d = 1; d <= 6; d++)
    {
        var lessons = db.GetLessons("3313", d, weekCode);
        foreach (var l in lessons)
        {
            csv.AppendLine($"{d},{weekCode},{l.TimeStart},{l.TimeEnd},\"{l.SubjectRaw.Replace("\"","\"\"")}\",\"{l.TeacherRaw.Replace("\"","\"\"")}\",\"{l.RoomRaw}\",\"{l.BuildingRaw}\",\"{l.TypeRaw}\",\"{l.ClassroomRaw}\"");
        }
    }
    var csvPath = Path.Combine(verifyDir, $"O3313_{suffix}.csv");
    File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
    Console.WriteLine($"Wrote {csvPath}");
}

DumpWeek(1, "odd");
DumpWeek(2, "even");

// Also dump raw fetch copy
var rawDest = Path.Combine(verifyDir, "raw_TimetableGroup50.xml");
File.Copy(xmlPath, rawDest, true);
Console.WriteLine($"Copied raw XML to {rawDest} ({new FileInfo(rawDest).Length} bytes)");

// Test getSchedule(date)
Console.WriteLine("\n--- getSchedule(date) test ---");
var testDateOdd = new DateTime(2026, 9, 2); // Wed odd week
var schedOdd = schedule.GetSchedule(testDateOdd, "3313");
Console.WriteLine($"GetSchedule {testDateOdd:yyyy-MM-dd} ({ParityService.DayNumberToTitle((int)testDateOdd.DayOfWeek == 0 ? 7 : (int)testDateOdd.DayOfWeek)}) count={schedOdd.Count}");
foreach (var l in schedOdd) Console.WriteLine($"  {l.TimeStart} {l.SubjectRaw} {l.RoomRaw}");

Console.WriteLine("\nVerification COMPLETE - all checks passed if no ERROR");
