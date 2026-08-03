namespace MordheimLedgerApp.Core.Models;

/// <summary>A single narrated log line for a Warband (auto-generated from actions, or a manual note).</summary>
public class HistoryEntry
{
    public int Id { get; set; }
    public int WarbandId { get; set; }
    public DateTime Date { get; set; }
    public string Text { get; set; } = string.Empty;
}
