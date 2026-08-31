using Vograph.Core.Models;

namespace Vograph.Core.Services;

public class OverrideService
{
    private readonly Database _db;
    public OverrideService(Database db) => _db = db;

    public string GetDisplayName(string subjectRaw, int dayOfWeek)
    {
        var norm = ParityService.NormalizeSubject(subjectRaw);
        var overrides = _db.GetOverrides();
        // global overrides weekday
        var global = overrides.FirstOrDefault(o => o.SubjectRawNormalized == norm && o.Scope == "global");
        if (global != null) return global.DisplayName;
        var weekday = overrides.FirstOrDefault(o => o.SubjectRawNormalized == norm && o.Scope == $"weekday:{dayOfWeek}");
        if (weekday != null) return weekday.DisplayName;
        return subjectRaw;
    }

    public Override? GetOverride(string subjectRaw, string scope)
    {
        var norm = ParityService.NormalizeSubject(subjectRaw);
        return _db.GetOverrides().FirstOrDefault(o => o.SubjectRawNormalized == norm && o.Scope == scope);
    }

    public long AddOrUpdate(string subjectRaw, string scope, string displayName, string? note)
    {
        var norm = ParityService.NormalizeSubject(subjectRaw);
        var existing = GetOverride(subjectRaw, scope);
        if (existing != null)
        {
            // update: delete and reinsert? simple delete
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM overrides WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", existing.Id);
            cmd.ExecuteNonQuery();
        }
        var ov = new Override
        {
            SubjectRawNormalized = norm,
            Scope = scope,
            DisplayName = displayName,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };
        return _db.InsertOverride(ov);
    }

    public void Remove(long id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM overrides WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public string? GetNote(string subjectRaw, int dayOfWeek)
    {
        var norm = ParityService.NormalizeSubject(subjectRaw);
        var overrides = _db.GetOverrides();
        var global = overrides.FirstOrDefault(o => o.SubjectRawNormalized == norm && o.Scope == "global");
        if (global != null) return global.Note;
        var weekday = overrides.FirstOrDefault(o => o.SubjectRawNormalized == norm && o.Scope == $"weekday:{dayOfWeek}");
        return weekday?.Note;
    }
}
