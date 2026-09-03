using Vograph.Core.Services;
using Vograph.Core.Models;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vograph", "vograph.db");
var db = new Database(dbPath);
var sched = new ScheduleService(db);
var map = new MapService(db, sched);

string[] tests = new[] {
    "331*;",
    "324;",
    "ВЦ 372*;",
    "ВЦ 280;",
    "дистанционно",
    "507*а;",
    "313а;",
    "270*(фест);",
    "450;",
    "526*;",
    "101;",
    "",
    "ВЦ 281; ВЦ 283;",
    "СК 2",
};

Console.WriteLine("=== Map Resolve Tests ===");
foreach(var t in tests) {
    var info = map.Resolve(t);
    if (info == null) Console.WriteLine($"'{t}' -> NULL");
    else Console.WriteLine($"'{t}' -> b='{info.Building}' floor={info.Floor} title='{info.Title}' hasMap={info.HasMap} url='{info.Url}' remote={info.IsRemote} note='{info.Note}' local='{info.LocalPath}'");
}

Console.WriteLine("\n=== All Maps ===");
foreach(var m in map.GetAllMaps()) {
    Console.WriteLine($"{m.Building} {m.Floor} -> {m.Url} localExists={File.Exists(m.LocalPath)}");
}

Console.WriteLine("\n=== Next Lesson for 3313 now===");
var (lesson, date) = map.GetNextLesson("3313", DateTime.Now);
if (lesson != null) {
    Console.WriteLine($"next: {date:yyyy-MM-dd} {lesson.TimeStart}-{lesson.TimeEnd} {lesson.SubjectRaw} cls='{lesson.ClassroomRaw}'");
    var mi = map.GetMapForLesson(lesson);
    Console.WriteLine($"map -> {mi?.Title} url={mi?.Url} local={mi?.LocalPath} hasMap={mi?.HasMap}");
    // try ensure cached for one
    if (mi != null && mi.HasMap) {
        Console.WriteLine("Ensuring cached...");
        var path = await map.EnsureCachedAsync(mi);
        Console.WriteLine($"cached path: {path} exists={File.Exists(path ?? "")}");
    }
} else {
    Console.WriteLine("no next lesson");
}

Console.WriteLine("\n=== Next Lesson for first group ===");
var groups = db.GetAllGroups();
if (groups.Count>0) {
    var g = groups.First();
    var (l2, d2) = map.GetNextLesson(g.Id, DateTime.Now);
    if (l2 != null) Console.WriteLine($"group {g.Name} {g.Id} next: {l2.SubjectRaw} cls={l2.ClassroomRaw}");
    else Console.WriteLine($"group {g.Name} {g.Id} next: null");
}

Console.WriteLine("\n=== Download all maps ===");
await map.EnsureAllMapsCachedAsync(null, new Progress<string>(s => Console.WriteLine(s)));
foreach(var m in map.GetAllMaps()) Console.WriteLine($"{m.Title} cached={File.Exists(m.LocalPath)} len={(File.Exists(m.LocalPath)?new FileInfo(m.LocalPath).Length:0)}");

Console.WriteLine("DONE");
