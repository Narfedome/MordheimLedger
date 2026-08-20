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

    /// <summary>Set true when a past Exploration result granted "an extra dice next time you roll on the
    /// Exploration chart, discard one" (so far only Straggler's "any other warband" branch - see
    /// Models.Library.ExplorationOutcome.GrantsNextExplorationBonusDie) - consumed as a one-time +1 to
    /// Core.Rules.ExplorationChart.ComputeDiceCount's bonusDice the NEXT time the End of Game wizard's
    /// Exploration step opens for this warband, then cleared regardless of whether a new dice actually
    /// changed anything (same "spend it either way" idiom as any other found-but-unused resource).</summary>
    public bool PendingExplorationBonusDie { get; set; }

    public string? Notes { get; set; }
}
