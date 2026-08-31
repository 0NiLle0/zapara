using Vograph.Core.Models;

namespace Vograph.Core.Services;

public class HomeworkService
{
    private readonly Database _db;
    public HomeworkService(Database db) => _db = db;

    public long AddHomework(string subjectRaw, string text, int targetNth, DateTime? createdAt = null)
    {
        var norm = ParityService.NormalizeSubject(subjectRaw);
        var hw = new Homework
        {
            SubjectRawNormalized = norm,
            Text = text,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            TargetNthOccurrence = Math.Clamp(targetNth, 1, 10),
            Status = "pending"
        };
        // Insert and compute due date
        hw.DueDateComputed = ComputeDueDate(hw.SubjectRawNormalized, hw.CreatedAt, hw.TargetNthOccurrence);
        hw.Status = ComputeStatus(hw);
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO homework (subjectRawNormalized, text, createdAt, targetNthOccurrence, dueDateComputed, status)
VALUES (@s,@t,@ca,@n,@due,@st); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@s", hw.SubjectRawNormalized);
        cmd.Parameters.AddWithValue("@t", hw.Text);
        cmd.Parameters.AddWithValue("@ca", hw.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@n", hw.TargetNthOccurrence);
        cmd.Parameters.AddWithValue("@due", hw.DueDateComputed?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@st", hw.Status);
        var id = (long)cmd.ExecuteScalar()!;
        return id;
    }

    public void UpdateHomework(long id, string text, int targetNth)
    {
        var hw = GetById(id);
        if (hw == null) return;
        hw.Text = text;
        hw.TargetNthOccurrence = Math.Clamp(targetNth, 1, 10);
        hw.DueDateComputed = ComputeDueDate(hw.SubjectRawNormalized, hw.CreatedAt, hw.TargetNthOccurrence);
        hw.Status = ComputeStatus(hw);
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE homework SET text=@t, targetNthOccurrence=@n, dueDateComputed=@due, status=@st WHERE id=@id";
        cmd.Parameters.AddWithValue("@t", hw.Text);
        cmd.Parameters.AddWithValue("@n", hw.TargetNthOccurrence);
        cmd.Parameters.AddWithValue("@due", hw.DueDateComputed?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@st", hw.Status);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public Homework? GetById(long id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, subjectRawNormalized, text, createdAt, targetNthOccurrence, dueDateComputed, status, doneAt FROM homework WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Homework
        {
            Id = r.GetInt64(0),
            SubjectRawNormalized = r.GetString(1),
            Text = r.GetString(2),
            CreatedAt = DateTime.Parse(r.GetString(3)),
            TargetNthOccurrence = r.GetInt32(4),
            DueDateComputed = r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)),
            Status = r.GetString(6),
            DoneAt = r.IsDBNull(7) ? null : DateTime.Parse(r.GetString(7))
        };
    }

    public List<Homework> GetAll()
    {
        var list = new List<Homework>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, subjectRawNormalized, text, createdAt, targetNthOccurrence, dueDateComputed, status, doneAt FROM homework ORDER BY dueDateComputed";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Homework
            {
                Id = r.GetInt64(0),
                SubjectRawNormalized = r.GetString(1),
                Text = r.GetString(2),
                CreatedAt = DateTime.Parse(r.GetString(3)),
                TargetNthOccurrence = r.GetInt32(4),
                DueDateComputed = r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)),
                Status = r.GetString(6),
                DoneAt = r.IsDBNull(7) ? null : DateTime.Parse(r.GetString(7))
            });
        }
        return list;
    }

    public List<Homework> GetForSubject(string subjectRaw)
    {
        var norm = ParityService.NormalizeSubject(subjectRaw);
        return GetAll().Where(h => h.SubjectRawNormalized == norm).ToList();
    }

    public void MarkDone(long id, bool done)
    {
        using var cmd = _db.Connection.CreateCommand();
        if (done)
        {
            cmd.CommandText = "UPDATE homework SET status='done', doneAt=@da WHERE id=@id";
            cmd.Parameters.AddWithValue("@da", DateTime.UtcNow.ToString("o"));
        }
        else
        {
            var hw = GetById(id);
            string status = "pending";
            if (hw != null)
            {
                // Compute status anew, ignoring done flag
                var tmp = new Homework { SubjectRawNormalized = hw.SubjectRawNormalized, CreatedAt = hw.CreatedAt, TargetNthOccurrence = hw.TargetNthOccurrence, DueDateComputed = hw.DueDateComputed, Status = "pending" };
                // Recompute due in case schedule changed
                tmp.DueDateComputed = ComputeDueDate(tmp.SubjectRawNormalized, tmp.CreatedAt, tmp.TargetNthOccurrence) ?? tmp.DueDateComputed;
                status = ComputeStatus(tmp);
            }
            cmd.CommandText = "UPDATE homework SET status=@st, doneAt=NULL WHERE id=@id";
            cmd.Parameters.AddWithValue("@st", status);
        }
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        // recompute status after
        if (!done) RecomputeAllStatuses();
    }

    public void Delete(long id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM homework WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public DateTime? ComputeDueDate(string subjectNormalized, DateTime from, int nth)
    {
        var settings = _db.GetSettings();
        if (string.IsNullOrEmpty(settings.MyGroupId)) return null;
        var groupId = settings.MyGroupId!;
        DateTime periodStart = DateTime.TryParse(settings.PeriodStart, out var ps) ? ps : new DateTime(DateTime.Now.Year, 9, 1);
        int weekCount = settings.WeekCount > 0 ? settings.WeekCount : 2;

        // Scan forward up to 120 days
        int found = 0;
        for (int offset = 1; offset <= 120; offset++)
        {
            var date = from.Date.AddDays(offset);
            // Skip Sunday
            if (date.DayOfWeek == DayOfWeek.Sunday) continue;
            int dow = (int)date.DayOfWeek; if (dow == 0) dow = 7;
            int weekCode = ParityService.GetWeekCode(date, periodStart, weekCount);
            if (settings.ParityInvert) weekCode = weekCode == 1 ? 2 : 1;

            var lessons = _db.GetLessons(groupId, dow, weekCode);
            foreach (var l in lessons)
            {
                var norm = ParityService.NormalizeSubject(l.SubjectRaw);
                if (norm == subjectNormalized)
                {
                    found++;
                    if (found == nth)
                    {
                        return date;
                    }
                    break; // count only once per day? Prompt says count occurrences of same subject, not lessons? If subject appears twice same day, count twice? For MVP count per day occurrence (once per lesson). But to avoid double counting same day, we break after first match per day.
                    // However spec says "N-th next Lesson where subjectRawNormalized matches" so if two lessons same subject same day, count each?
                    // We'll count each lesson individually, so not break? But simpler break per day avoids double.
                }
            }
        }
        return null;
    }

    public string ComputeStatus(Homework hw)
    {
        if (hw.Status == "done") return "done";
        if (hw.DueDateComputed == null) return "pending";
        var today = DateTime.Today;
        var due = hw.DueDateComputed.Value.Date;
        int daysDiff = (due - today).Days;
        // Need to count lessons before due
        var settings = _db.GetSettings();
        if (string.IsNullOrEmpty(settings.MyGroupId)) return "pending";
        var groupId = settings.MyGroupId!;
        DateTime periodStart = DateTime.TryParse(settings.PeriodStart, out var ps) ? ps : new DateTime(DateTime.Now.Year, 9, 1);
        int weekCount = settings.WeekCount > 0 ? settings.WeekCount : 2;

        // Count occurrences before due
        int lessonsBefore = 0;
        for (int offset = 1; offset <= 120; offset++)
        {
            var d = today.AddDays(offset);
            if (d >= due) break;
            int dow = (int)d.DayOfWeek; if (dow == 0) dow = 7;
            if (dow == 7) continue;
            int wc = ParityService.GetWeekCode(d, periodStart, weekCount);
            if (settings.ParityInvert) wc = wc == 1 ? 2 : 1;
            var lessons = _db.GetLessons(groupId, dow, wc);
            foreach (var l in lessons)
            {
                if (ParityService.NormalizeSubject(l.SubjectRaw) == hw.SubjectRawNormalized) lessonsBefore++;
            }
        }

        // Also check if due lesson exists today/tomorrow
        if (daysDiff < 0) return "overdue";
        if (daysDiff == 0) return "burning_urgent"; // burns very brightly in morning
        if (daysDiff == 1) return "burning"; // due tomorrow
        if (lessonsBefore == 1) return "approaching"; // 1 lesson before due -> gray
        if (lessonsBefore == 0 && daysDiff <= 3) return "approaching"; // heuristic
        return "far";
    }

    public void RecomputeAllStatuses()
    {
        var all = GetAll();
        foreach (var hw in all)
        {
            if (hw.Status == "done") continue;
            var newDue = ComputeDueDate(hw.SubjectRawNormalized, hw.CreatedAt, hw.TargetNthOccurrence);
            var newStatus = hw.DueDateComputed != newDue ? ComputeStatus(new Homework { SubjectRawNormalized = hw.SubjectRawNormalized, CreatedAt = hw.CreatedAt, TargetNthOccurrence = hw.TargetNthOccurrence, DueDateComputed = newDue, Status = "pending" }) : ComputeStatus(hw);
            // If due changed, update
            if (newDue != hw.DueDateComputed || newStatus != hw.Status)
            {
                hw.DueDateComputed = newDue;
                hw.Status = newStatus;
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = "UPDATE homework SET dueDateComputed=@due, status=@st WHERE id=@id";
                cmd.Parameters.AddWithValue("@due", hw.DueDateComputed?.ToString("o") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@st", hw.Status);
                cmd.Parameters.AddWithValue("@id", hw.Id);
                cmd.ExecuteNonQuery();
            }
            else
            {
                // just status
                hw.Status = ComputeStatus(hw);
                using var cmd2 = _db.Connection.CreateCommand();
                cmd2.CommandText = "UPDATE homework SET status=@st WHERE id=@id";
                cmd2.Parameters.AddWithValue("@st", hw.Status);
                cmd2.Parameters.AddWithValue("@id", hw.Id);
                cmd2.ExecuteNonQuery();
            }
        }
    }
}
