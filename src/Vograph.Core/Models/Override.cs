namespace Vograph.Core.Models;

public class Override
{
    public long Id { get; set; }
    public string SubjectRawNormalized { get; set; } = "";
    public string Scope { get; set; } = ""; // "global" or "weekday:3"
    public string DisplayName { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    // Keep original value for rollback/diff is implied via SubjectRawNormalized key
}
