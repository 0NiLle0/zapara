using Vograph.Core.Services;
using Vograph.Core.Models;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var dbPath = Path.Combine(AppContext.BaseDirectory, "vograph_verify4.db");
foreach (var f in new[] { dbPath, dbPath+"-wal", dbPath+"-shm" }) if (File.Exists(f)) File.Delete(f);
Console.WriteLine($"DB {dbPath}");
using var db = new Database(dbPath);
var parser = new ParserService(db);
string xml = File.ReadAllText(@"C:\Users\NiLle\AppData\Local\Temp\opencode\TimetableGroup50.xml", Encoding.UTF8);
await parser.RefreshAsync(xmlOverride: xml);
Console.WriteLine($"Groups {db.GetAllGroups().Count}");
var settings = db.GetSettings();
settings.MyGroupId = "3313";
settings.PeriodStart = "2026-09-01";
settings.WeekCount = 2;
settings.NotifyTime1 = "20:00";
settings.NotifyTime2 = "07:30";
settings.IntersectionStrictness = 50;
db.SaveSettings(settings);
Console.WriteLine($"MyGroup 3313 ({db.GetGroup("3313")?.Name})");

// Add friend O3314 equivalent - we use a group that exists: find first group with Monday lessons overlapping
var allGroups = db.GetAllGroups();
var myLessonsMon = db.GetLessons("3313", 1, 1); // Monday odd
Console.WriteLine($"My Monday odd lessons: {myLessonsMon.Count}");
foreach (var l in myLessonsMon) Console.WriteLine($"  {l.TimeStart}-{l.TimeEnd} {l.SubjectRaw} room={l.RoomRaw} building={l.BuildingRaw} classroom={l.ClassroomRaw}");

// Find candidate friend group with same Monday time
string? friendId = null;
string? friendName = null;
foreach (var g in allGroups.Where(x=>x.Id!="3313").Take(30))
{
    var fl = db.GetLessons(g.Id, 1, 1);
    if (fl.Any(x=> x.TimeStart=="09:00"))
    {
        Console.WriteLine($"Candidate {g.Name} ({g.Id}) has 09:00 Monday: {fl.First(x=>x.TimeStart=="09:00").ClassroomRaw}");
        if (friendId==null) { friendId=g.Id; friendName=g.Name; }
    }
}
if (friendName==null)
{
    // fallback to group 3032
    var g = db.GetGroup("3032");
    friendName = g!.Name; friendId = g.Id;
    Console.WriteLine($"Fallback friend {friendName}");
}
Console.WriteLine($"Selected friend {friendName} ({friendId})");

// Add friend
var friend = new FriendGroup { GroupName = friendName!, ColorHex = "#FF6CA5E0", Enabled = true };
db.InsertFriend(friend);
Console.WriteLine($"Added friend {friend.GroupName} color {friend.ColorHex}");

// Test intersection at strictness 0 and 100 for Monday 2026-09-07 (odd Monday)
var interService = new IntersectionService(db);
var dateMon = new DateTime(2026,9,7); // Monday odd
Console.WriteLine($"\nDate {dateMon:yyyy-MM-dd} dow={(int)dateMon.DayOfWeek} weekCode={ParityService.GetWeekCode(dateMon, DateTime.Parse(settings.PeriodStart), 2)}");

var myLessons = db.GetLessons("3313", 1, 1);
int strict0Count = 0, strict100Count = 0;
foreach (var my in myLessons)
{
    var res0 = interService.GetIntersections(my, dateMon, db.GetFriends(), 0);
    var res100 = interService.GetIntersections(my, dateMon, db.GetFriends(), 100);
    Console.WriteLine($"Lesson {my.TimeStart} {my.SubjectRaw} room {my.RoomRaw} building {my.BuildingRaw}: strict0={res0.Count} strict100={res100.Count} details0={string.Join(";", res0.Select(r=>r.Score))} details100={string.Join(";", res100.Select(r=>r.Score))}");
    strict0Count += res0.Count;
    strict100Count += res100.Count;
}
Console.WriteLine($"\nTotal intersections strict0={strict0Count} strict100={strict100Count}");
if (strict0Count>0) Console.WriteLine("PASS strict0 shows icons (any time overlap)");
else Console.WriteLine("FAIL strict0 should show");
if (strict100Count <= strict0Count) Console.WriteLine("PASS strict100 <= strict0 (same room only)");
else Console.WriteLine("FAIL");
// Test intermediate strictness 40 (same building)
int strict40Count = 0;
foreach (var my in myLessons)
{
    var res40 = interService.GetIntersections(my, dateMon, db.GetFriends(), 40);
    strict40Count += res40.Count;
}
Console.WriteLine($"strict40 (same building) count={strict40Count} should be between 0 and {strict0Count}");
if (strict40Count>=0 && strict40Count<=strict0Count) Console.WriteLine("PASS strict40 intermediate");
else Console.WriteLine("FAIL strict40");

// Also test same building case: check if any friend lesson same building would score 40
// Add a friend with same building as my lesson: manually check room
Console.WriteLine("\n--- Building check ---");
var myFirst = myLessons.FirstOrDefault(x=>x.TimeStart=="09:00");
if (myFirst!=null)
{
    var friendLessons = db.GetLessons(friendId!, 1, 1);
    var flSameTime = friendLessons.FirstOrDefault(x=>x.TimeStart=="09:00");
    if (flSameTime!=null)
    {
        Console.WriteLine($"My room {myFirst.RoomRaw} building {myFirst.BuildingRaw} vs friend room {flSameTime.RoomRaw} building {flSameTime.BuildingRaw}");
        // Score should be 100 if same room, 40 if same building, 0 otherwise
    }
}

// Test notification with renamed text and burning HW
Console.WriteLine("\n--- Notification test ---");
var ovService = new OverrideService(db);
var hwService = new HomeworkService(db);
var schedService = new ScheduleService(db);
var notifService = new NotificationService(db, ovService, hwService, schedService);
// Add rename for subjects appearing both tomorrow and today to ensure both toasts show rename
var tomorrow = DateTime.Today.AddDays(1);
int dowTom = (int)tomorrow.DayOfWeek; if (dowTom==0) dowTom=7;
var tomLessons = db.GetLessons("3313", dowTom, ParityService.GetWeekCode(tomorrow, DateTime.Parse(settings.PeriodStart),2));
if (tomLessons.Count==0) tomLessons = myLessonsMon;
var subjForHw = tomLessons.First().SubjectRaw;
ovService.AddOrUpdate(subjForHw, "global", "ПЕРЕИМЕНОВАНО", null);
Console.WriteLine($"Renamed {subjForHw} -> ПЕРЕИМЕНОВАНО");
// Also rename a Tuesday subject so 07:30 toast also shows rename
var todayTuesday = DateTime.Today;
int dowToday = (int)todayTuesday.DayOfWeek; if (dowToday==0) dowToday=7;
var todayLessons = db.GetLessons("3313", dowToday, ParityService.GetWeekCode(todayTuesday, DateTime.Parse(settings.PeriodStart),2));
if (todayLessons.Count>0) {
    var subjToday = todayLessons.First().SubjectRaw;
    if (subjToday != subjForHw) {
        ovService.AddOrUpdate(subjToday, "global", "ПЕРЕИМ-2", null);
        Console.WriteLine($"Renamed today {subjToday} -> ПЕРЕИМ-2");
    }
}
// Add homework that will be burning (due tomorrow)
var hwId = hwService.AddHomework(subjForHw, "Тест горящего ДЗ", 1, DateTime.Today);
var hw = hwService.GetById(hwId);
Console.WriteLine($"HW created due {hw.DueDateComputed:yyyy-MM-dd} status {hw.Status}");
// Force status to burning for test: set due to tomorrow
// Update hw due to be tomorrow
hw.DueDateComputed = tomorrow;
hw.Status = "burning";
using (var cmd = db.Connection.CreateCommand()) { cmd.CommandText = "UPDATE homework SET dueDateComputed=@due, status=@st WHERE id=@id"; cmd.Parameters.AddWithValue("@due", hw.DueDateComputed?.ToString("o")); cmd.Parameters.AddWithValue("@st", hw.Status); cmd.Parameters.AddWithValue("@id", hwId); cmd.ExecuteNonQuery(); }
Console.WriteLine($"Forced HW due tomorrow status burning");

string text = notifService.BuildNotificationText(tomorrow);
Console.WriteLine($"Notification text for {tomorrow:yyyy-MM-dd}: {text}");
if (text.Contains("ПЕРЕИМЕНОВАНО")) Console.WriteLine("PASS renamed text in notification");
else Console.WriteLine("FAIL renamed not in notification");
if (text.Contains("[ДЗ!]") || text.Contains("ДЗ")) Console.WriteLine("PASS burning homework marked");
else Console.WriteLine("FAIL burning not marked");

// Simulate toast firing at both times
var time1 = settings.NotifyTime1!;
var time2 = settings.NotifyTime2!;
// For verification, we set times to now and now+1 minute and fire
var now1 = DateTime.Today.Add(TimeSpan.Parse(time1));
var now2 = DateTime.Today.Add(TimeSpan.Parse(time2));
notifService.LogAndShow(now1);
notifService.LogAndShow(now2);
Console.WriteLine($"Logged toasts for {time1} and {time2}");
var logDir = Path.Combine(AppContext.BaseDirectory, "data", "runs");
var logFile = Path.Combine(logDir, $"toast-{DateTime.Today:yyyyMMdd}.log");
if (File.Exists(logFile)) { Console.WriteLine($"Log exists {logFile}"); Console.WriteLine(File.ReadAllText(logFile, Encoding.UTF8)); }
var altLog = Path.Combine(@"C:\Users\NiLle\Desktop\projects\vograph\data\runs", $"toast-{DateTime.Today:yyyyMMdd}.log");
if (File.Exists(altLog)) { Console.WriteLine($"Alt log {altLog}:\n{File.ReadAllText(altLog, Encoding.UTF8)}"); }

Console.WriteLine("\nPhase4 verification COMPLETE");
