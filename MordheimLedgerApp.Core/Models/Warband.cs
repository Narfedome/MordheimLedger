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

    /// <summary>Persistent stock of found-but-unsold wyrdstone shards - the rulebook explicitly does
    /// not require selling immediately after a battle ("you may want to hoard it and sell it later").
    /// Increased by the End of Game wizard's Exploration step, decreased by its Sell Wyrdstone step
    /// (the only place shards convert to Treasury gold).</summary>
    public int WyrdstoneShards { get; set; }

    public string? Notes { get; set; }
}
