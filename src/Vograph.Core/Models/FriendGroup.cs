namespace Vograph.Core.Models;

public class FriendGroup
{
    public long Id { get; set; }
    public string GroupName { get; set; } = "";
    public string ColorHex { get; set; } = "#FF6CA5E0"; // one of 5
    public bool Enabled { get; set; } = true;
}
