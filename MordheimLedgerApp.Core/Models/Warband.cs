namespace MordheimLedgerApp.Core.Models;

/// <summary>
/// A player's warband — an instance, never itself "official" content (ContentSource lives on
/// WarbandArchetype instead). WarbandArchetypeId is where Treasury's starting value came from at
/// creation; the Warband then evolves independently of it.
/// </summary>
public class Warband
{
    public int Id { get; set; }
    public int? CampaignId { get; set; }
    public int WarbandArchetypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Treasury { get; set; }
    public string? Notes { get; set; }
}
