using Vograph.Core.Models;

namespace Vograph.Core.Services;

public class IntersectionService
{
    private readonly Database _db;
    public IntersectionService(Database db) => _db = db;

    public record IntersectionResult(string FriendGroupName, string FriendColor, string Teacher, string Room, int Score, bool MatchesThreshold);

    // For each myLesson on selected day, find friend intersections
    public List<IntersectionResult> GetIntersections(Lesson myLesson, DateTime date, List<FriendGroup> friends, int strictness)
    {
        var results = new List<IntersectionResult>();
        if (friends.Count == 0) return results;

        // Determine parity for date
        var settings = _db.GetSettings();
        DateTime periodStart = DateTime.TryParse(settings.PeriodStart, out var ps) ? ps : new DateTime(DateTime.Now.Year, 9, 1);
        int weekCount = settings.WeekCount > 0 ? settings.WeekCount : 2;
        int weekCode = ParityService.GetWeekCode(date, periodStart, weekCount);
        if (settings.ParityInvert) weekCode = weekCode == 1 ? 2 : 1;

        int dow = (int)date.DayOfWeek; if (dow == 0) dow = 7;
        if (dow == 7) return results;

        // Need friend groupId resolution via Name -> Id
        var allGroups = _db.GetAllGroups();
        var groupByName = allGroups.ToDictionary(g => g.Name, g => g.Id);

        foreach (var friend in friends.Where(f => f.Enabled).Take(5))
        {
            if (!groupByName.TryGetValue(friend.GroupName, out var friendGroupId))
            {
                // Try by Id if friend stored Id instead of Name? fallback try direct
                if (_db.GetGroup(friend.GroupName) != null) friendGroupId = friend.GroupName;
                else continue;
            }
            var friendLessons = _db.GetLessons(friendGroupId, dow, weekCode);
            foreach (var fl in friendLessons)
            {
                if (!TimesOverlap(myLesson.TimeStart, myLesson.TimeEnd, fl.TimeStart, fl.TimeEnd)) continue;
                int score = 0;
                // sameBuilding +40, sameRoom +100, else sameTime 0
                bool sameRoom = !string.IsNullOrWhiteSpace(myLesson.RoomRaw) && !string.IsNullOrWhiteSpace(fl.RoomRaw) && myLesson.RoomRaw.Trim().Equals(fl.RoomRaw.Trim(), StringComparison.OrdinalIgnoreCase);
                bool sameBuilding = !string.IsNullOrWhiteSpace(myLesson.BuildingRaw) && !string.IsNullOrWhiteSpace(fl.BuildingRaw) && myLesson.BuildingRaw.Trim().Equals(fl.BuildingRaw.Trim(), StringComparison.OrdinalIgnoreCase);
                // Also consider ClassroomRaw containing same building code
                if (sameRoom) score = 100;
                else if (sameBuilding) score = 40;
                else score = 0; // same time only

                // threshold check
                bool matches = score >= strictness;
                // For threshold 0, any time overlap counts (score 0 >=0 true)
                // For threshold 100, only sameRoom (100) counts
                if (matches)
                {
                    results.Add(new IntersectionResult(friend.GroupName, friend.ColorHex, fl.TeacherRaw, fl.ClassroomRaw, score, matches));
                }
            }
        }
        return results;
    }

    public static bool TimesOverlap(string? startA, string? endA, string? startB, string? endB)
    {
        if (string.IsNullOrWhiteSpace(startA) || string.IsNullOrWhiteSpace(startB)) return false;
        if (!TimeSpan.TryParse(startA, out var sA)) return false;
        if (!TimeSpan.TryParse(startB, out var sB)) return false;
        // Use 95 min if end missing
        TimeSpan eA = TimeSpan.TryParse(endA, out var ea) ? ea : sA.Add(TimeSpan.FromMinutes(95));
        TimeSpan eB = TimeSpan.TryParse(endB, out var eb) ? eb : sB.Add(TimeSpan.FromMinutes(95));
        return sA < eB && sB < eA;
    }
}
