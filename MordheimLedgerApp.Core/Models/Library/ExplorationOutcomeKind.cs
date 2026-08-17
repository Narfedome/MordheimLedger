namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>What an ExplorationOutcome mechanically does, if anything - see ExplorationOutcome. Gold,
/// Item and Wyrdstone are surfaced by the End of Game wizard (added to the warband's treasury/shard
/// stock, or offered for equipping); None is a branch of a sub-table that grants something else (a
/// free Henchman, Experience, a permanent warband trait...) too bespoke/conditional to model, left as
/// descriptive text on the owning ExplorationResult.</summary>
public enum ExplorationOutcomeKind
{
    None = 0,
    Gold = 1,
    Item = 2,

    /// <summary>Bonus wyrdstone shards found on top of the main dice-sum calculation (e.g. "Shattered
    /// Building", "The Pit", "Hidden Treasure") - uses ExplorationOutcome.GoldFormula for its dice
    /// formula too (e.g. "D3", "D6+1"), same field reused rather than a redundant one.</summary>
    Wyrdstone = 3
}
