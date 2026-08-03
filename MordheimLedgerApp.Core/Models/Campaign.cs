namespace MordheimLedgerApp.Core.Models;

/// <summary>Groups the warbands of a play group and their shared battle history.</summary>
public class Campaign
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
