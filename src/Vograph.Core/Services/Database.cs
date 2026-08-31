using Microsoft.Data.Sqlite;
using Vograph.Core.Models;

namespace Vograph.Core.Services;

public class Database : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _conn;

    public Database(string dbPath)
    {
        _dbPath = dbPath;
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        // Enable WAL for better concurrency
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        EnsureSchema();
    }

    public SqliteConnection Connection => _conn;

    private void EnsureSchema()
    {
        var sql = @"
CREATE TABLE IF NOT EXISTS groups (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    url TEXT,
    lastFetchedAt TEXT,
    rawXml TEXT
);
CREATE TABLE IF NOT EXISTS schedule_cache (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    groupId TEXT NOT NULL,
    dayOfWeek INTEGER NOT NULL,
    parity INTEGER NOT NULL,
    idx INTEGER NOT NULL,
    timeStart TEXT,
    timeEnd TEXT,
    subjectRaw TEXT,
    subjectNormalized TEXT,
    teacherRaw TEXT,
    roomRaw TEXT,
    buildingRaw TEXT,
    typeRaw TEXT,
    classroomRaw TEXT,
    rawXml TEXT,
    FOREIGN KEY(groupId) REFERENCES groups(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_schedule_group_day_parity ON schedule_cache(groupId, dayOfWeek, parity);
CREATE TABLE IF NOT EXISTS overrides (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    subjectRawNormalized TEXT NOT NULL,
    scope TEXT NOT NULL,
    displayName TEXT NOT NULL,
    note TEXT,
    createdAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_overrides_subject_scope ON overrides(subjectRawNormalized, scope);
CREATE TABLE IF NOT EXISTS homework (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    subjectRawNormalized TEXT NOT NULL,
    text TEXT NOT NULL,
    createdAt TEXT NOT NULL,
    targetNthOccurrence INTEGER NOT NULL,
    dueDateComputed TEXT,
    status TEXT NOT NULL,
    doneAt TEXT
);
CREATE INDEX IF NOT EXISTS idx_homework_subject ON homework(subjectRawNormalized);
CREATE TABLE IF NOT EXISTS friends (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    groupName TEXT NOT NULL,
    colorHex TEXT NOT NULL,
    enabled INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    myGroupId TEXT,
    parityInvert INTEGER NOT NULL DEFAULT 0,
    notifyTime1 TEXT,
    notifyTime2 TEXT,
    intersectionStrictness INTEGER NOT NULL DEFAULT 50,
    lastSyncAt TEXT,
    lastFetchedAt TEXT,
    weekCount INTEGER NOT NULL DEFAULT 2,
    periodTitle TEXT,
    periodStart TEXT
);
INSERT OR IGNORE INTO settings (id, parityInvert, intersectionStrictness, weekCount) VALUES (1, 0, 50, 2);
";
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public Settings GetSettings()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT myGroupId, parityInvert, notifyTime1, notifyTime2, intersectionStrictness, lastSyncAt, lastFetchedAt, weekCount, periodTitle, periodStart FROM settings WHERE id=1";
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return new Settings();
        return new Settings
        {
            MyGroupId = r.IsDBNull(0) ? null : r.GetString(0),
            ParityInvert = r.GetInt32(1) != 0,
            NotifyTime1 = r.IsDBNull(2) ? null : r.GetString(2),
            NotifyTime2 = r.IsDBNull(3) ? null : r.GetString(3),
            IntersectionStrictness = r.GetInt32(4),
            LastSyncAt = r.IsDBNull(5) ? null : DateTime.TryParse(r.GetString(5), out var dt) ? dt : null,
            LastFetchedAt = r.IsDBNull(6) ? null : r.GetString(6),
            WeekCount = r.GetInt32(7),
            PeriodTitle = r.IsDBNull(8) ? null : r.GetString(8),
            PeriodStart = r.IsDBNull(9) ? null : r.GetString(9)
        };
    }

    public void SaveSettings(Settings s)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
UPDATE settings SET
    myGroupId=@g, parityInvert=@inv, notifyTime1=@t1, notifyTime2=@t2,
    intersectionStrictness=@strict, lastSyncAt=@sync, lastFetchedAt=@lf,
    weekCount=@wc, periodTitle=@pt, periodStart=@ps
WHERE id=1";
        cmd.Parameters.AddWithValue("@g", (object?)s.MyGroupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@inv", s.ParityInvert ? 1 : 0);
        cmd.Parameters.AddWithValue("@t1", (object?)s.NotifyTime1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@t2", (object?)s.NotifyTime2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@strict", s.IntersectionStrictness);
        cmd.Parameters.AddWithValue("@sync", s.LastSyncAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lf", (object?)s.LastFetchedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@wc", s.WeekCount);
        cmd.Parameters.AddWithValue("@pt", (object?)s.PeriodTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ps", (object?)s.PeriodStart ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void UpsertGroup(Group g)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO groups (id, name, url, lastFetchedAt, rawXml)
VALUES (@id,@name,@url,@lf,@raw)
ON CONFLICT(id) DO UPDATE SET name=excluded.name, url=excluded.url, lastFetchedAt=excluded.lastFetchedAt, rawXml=excluded.rawXml";
        cmd.Parameters.AddWithValue("@id", g.Id);
        cmd.Parameters.AddWithValue("@name", g.Name);
        cmd.Parameters.AddWithValue("@url", (object?)g.Url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lf", g.LastFetchedAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@raw", (object?)g.RawXml ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public List<Group> GetAllGroups()
    {
        var list = new List<Group>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, url, lastFetchedAt FROM groups ORDER BY name";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Group
            {
                Id = r.GetString(0),
                Name = r.GetString(1),
                Url = r.IsDBNull(2) ? "" : r.GetString(2),
                LastFetchedAt = r.IsDBNull(3) ? null : DateTime.TryParse(r.GetString(3), out var dt) ? dt : null
            });
        }
        return list;
    }

    public Group? GetGroup(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, url, lastFetchedAt FROM groups WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Group { Id = r.GetString(0), Name = r.GetString(1), Url = r.IsDBNull(2) ? "" : r.GetString(2), LastFetchedAt = r.IsDBNull(3) ? null : DateTime.TryParse(r.GetString(3), out var dt) ? dt : null };
    }

    public void ClearScheduleForGroup(string groupId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM schedule_cache WHERE groupId=@gid";
        cmd.Parameters.AddWithValue("@gid", groupId);
        cmd.ExecuteNonQuery();
    }

    public void InsertLesson(Lesson l)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO schedule_cache
(groupId, dayOfWeek, parity, idx, timeStart, timeEnd, subjectRaw, subjectNormalized, teacherRaw, roomRaw, buildingRaw, typeRaw, classroomRaw, rawXml)
VALUES (@gid,@dow,@par,@idx,@ts,@te,@sub,@norm,@teach,@room,@build,@type,@cls,@raw)";
        cmd.Parameters.AddWithValue("@gid", l.GroupId);
        cmd.Parameters.AddWithValue("@dow", l.DayOfWeek);
        cmd.Parameters.AddWithValue("@par", l.Parity);
        cmd.Parameters.AddWithValue("@idx", l.Index);
        cmd.Parameters.AddWithValue("@ts", (object?)l.TimeStart ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@te", (object?)l.TimeEnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sub", (object?)l.SubjectRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@norm", (object?)l.SubjectNormalized ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@teach", (object?)l.TeacherRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@room", (object?)l.RoomRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@build", (object?)l.BuildingRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@type", (object?)l.TypeRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cls", (object?)l.ClassroomRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@raw", DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public List<Lesson> GetLessons(string groupId, int dayOfWeek, int parity)
    {
        var list = new List<Lesson>();
        using var cmd = _conn.CreateCommand();
        // parity 0 means both, but our data uses 1/2; filter accordingly
        if (parity == 0)
            cmd.CommandText = "SELECT id, groupId, dayOfWeek, parity, idx, timeStart, timeEnd, subjectRaw, subjectNormalized, teacherRaw, roomRaw, buildingRaw, typeRaw, classroomRaw FROM schedule_cache WHERE groupId=@gid AND dayOfWeek=@dow ORDER BY idx, timeStart";
        else
            cmd.CommandText = "SELECT id, groupId, dayOfWeek, parity, idx, timeStart, timeEnd, subjectRaw, subjectNormalized, teacherRaw, roomRaw, buildingRaw, typeRaw, classroomRaw FROM schedule_cache WHERE groupId=@gid AND dayOfWeek=@dow AND (parity=@par OR parity=0) ORDER BY idx, timeStart";
        cmd.Parameters.AddWithValue("@gid", groupId);
        cmd.Parameters.AddWithValue("@dow", dayOfWeek);
        if (parity != 0) cmd.Parameters.AddWithValue("@par", parity);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Lesson
            {
                Id = r.GetInt64(0),
                GroupId = r.GetString(1),
                DayOfWeek = r.GetInt32(2),
                Parity = r.GetInt32(3),
                Index = r.GetInt32(4),
                TimeStart = r.IsDBNull(5) ? "" : r.GetString(5),
                TimeEnd = r.IsDBNull(6) ? "" : r.GetString(6),
                SubjectRaw = r.IsDBNull(7) ? "" : r.GetString(7),
                SubjectNormalized = r.IsDBNull(8) ? "" : r.GetString(8),
                TeacherRaw = r.IsDBNull(9) ? "" : r.GetString(9),
                RoomRaw = r.IsDBNull(10) ? "" : r.GetString(10),
                BuildingRaw = r.IsDBNull(11) ? "" : r.GetString(11),
                TypeRaw = r.IsDBNull(12) ? "" : r.GetString(12),
                ClassroomRaw = r.IsDBNull(13) ? "" : r.GetString(13)
            });
        }
        return list;
    }

    public List<Lesson> GetAllLessonsForGroup(string groupId)
    {
        var list = new List<Lesson>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, groupId, dayOfWeek, parity, idx, timeStart, timeEnd, subjectRaw, subjectNormalized, teacherRaw, roomRaw, buildingRaw, typeRaw, classroomRaw FROM schedule_cache WHERE groupId=@gid ORDER BY dayOfWeek, parity, idx";
        cmd.Parameters.AddWithValue("@gid", groupId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Lesson
            {
                Id = r.GetInt64(0),
                GroupId = r.GetString(1),
                DayOfWeek = r.GetInt32(2),
                Parity = r.GetInt32(3),
                Index = r.GetInt32(4),
                TimeStart = r.IsDBNull(5) ? "" : r.GetString(5),
                TimeEnd = r.IsDBNull(6) ? "" : r.GetString(6),
                SubjectRaw = r.IsDBNull(7) ? "" : r.GetString(7),
                SubjectNormalized = r.IsDBNull(8) ? "" : r.GetString(8),
                TeacherRaw = r.IsDBNull(9) ? "" : r.GetString(9),
                RoomRaw = r.IsDBNull(10) ? "" : r.GetString(10),
                BuildingRaw = r.IsDBNull(11) ? "" : r.GetString(11),
                TypeRaw = r.IsDBNull(12) ? "" : r.GetString(12),
                ClassroomRaw = r.IsDBNull(13) ? "" : r.GetString(13)
            });
        }
        return list;
    }

    public long InsertOverride(Override o)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO overrides (subjectRawNormalized, scope, displayName, note, createdAt) VALUES (@s,@scope,@d,@n,@ca); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@s", o.SubjectRawNormalized);
        cmd.Parameters.AddWithValue("@scope", o.Scope);
        cmd.Parameters.AddWithValue("@d", o.DisplayName);
        cmd.Parameters.AddWithValue("@n", (object?)o.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ca", o.CreatedAt.ToString("o"));
        var id = (long)cmd.ExecuteScalar()!;
        return id;
    }

    public List<Override> GetOverrides()
    {
        var list = new List<Override>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, subjectRawNormalized, scope, displayName, note, createdAt FROM overrides";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Override
            {
                Id = r.GetInt64(0),
                SubjectRawNormalized = r.GetString(1),
                Scope = r.GetString(2),
                DisplayName = r.GetString(3),
                Note = r.IsDBNull(4) ? null : r.GetString(4),
                CreatedAt = DateTime.Parse(r.GetString(5))
            });
        }
        return list;
    }

    public void Dispose()
    {
        _conn.Dispose();
    }
}
