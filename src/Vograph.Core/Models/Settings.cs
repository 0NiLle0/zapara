namespace Vograph.Core.Models;

public class Settings
{
    public string? MyGroupId { get; set; }
    public bool ParityInvert { get; set; } = false;
    public string? NotifyTime1 { get; set; } // HH:mm
    public string? NotifyTime2 { get; set; }
    public int IntersectionStrictness { get; set; } = 50; // 0..100
    public string Language { get; set; } = "ru"; // 'ru' | 'en', default ru per §2
    public DateTime? LastSyncAt { get; set; }
    public string? LastFetchedAt { get; set; }
    public string? LastAutoCheckAt { get; set; } // ISO, for auto-refresh 24h timer
    public int WeekCount { get; set; } = 2;
    public string? PeriodTitle { get; set; }
    public string? PeriodStart { get; set; } // YYYY-MM-DD
}
