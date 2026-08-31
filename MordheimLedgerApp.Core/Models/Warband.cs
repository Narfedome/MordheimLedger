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

    /// <summary>Free-text "next game" reminder set by an Exploration branch that can't be mechanized
    /// because it depends on the OPPONENT's identity, which the app has no concept of (e.g. Graveyard's
    /// catch-all: "the next time you play against Sisters of Sigmar or Witch Hunters, their entire
    /// warband will hate all your models" - see Models.Library.ExplorationOutcome.NextGameNoteText).
    /// Shown as a banner on WarbandDetailPage, cleared unconditionally the next time this warband's End
    /// of Game wizard is saved, whether it actually applied that game or not - same "spend it either way"
    /// idiom as PendingExplorationBonusDie. Null = no pending reminder.</summary>
    public string? NextGameNote { get; set; }

    /// <summary>Permanent, unlike NextGameNote's one-game reminder - set once and never cleared once a
    /// warband finds Entrance to the Catacombs (see Models.Library.ExplorationOutcome.
    /// GrantsCatacombReroll), granting "you may re-roll one dice when you roll on the Exploration chart"
    /// from then on. A second/subsequent entrance found doesn't grant anything further - setting this
    /// true again is naturally idempotent, no extra tracking needed. Per the user's explicit
    /// simplification: the app only shows an informational reminder in the Exploration roll step, never
    /// implements the re-roll itself - the player re-rolls their own physical die and types the new
    /// value.</summary>
    public bool HasCatacombReroll { get; set; }

    /// <summary>True between "Lancer la partie" and the following "Fin de partie" - drives which of the
    /// two actions shows on WarbandDetailPage (mutually exclusive, never both). Purely a UI toggle: no
    /// roster/inventory edit is actually blocked while true (explicit user decision, 2026-08-26 - no
    /// locking mechanism, this flag only tracks which button to show and gates nothing else). Cleared by
    /// EndOfGame, set by StartGame.</summary>
    public bool GameInProgress { get; set; }

    public string? Notes { get; set; }
}
