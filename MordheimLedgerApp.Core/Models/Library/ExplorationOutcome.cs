namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// One branch of an ExplorationResult's sub-table (e.g. "Corpse" rolls a D6: 1-2 gold, 3 a dagger, 4 an
/// axe...) - only the branches worth mechanizing (Gold/Item) exist as a row; anything else stays in the
/// owning ExplorationResult.Description. Two very different dice mechanics share this same shape,
/// distinguished by ExplorationResult.RollsIndependently:
///
/// - RollsIndependently = false (most entries, e.g. Corpse, Smithy, Fletcher): a single D6 is rolled
///   once and picks exactly one mutually exclusive branch - SubRollMin/SubRollMax is the inclusive
///   range that branch owns (e.g. "1-2" -> Min=1, Max=2).
/// - RollsIndependently = true (e.g. "Hidden Treasure", "Slaughtered Warband"): every Outcome is
///   checked on its own, independently, against a "roll >= SubRollMin" threshold (a "4+" entry) -
///   several can trigger the same result. SubRollMax is unused here.
///
/// SubRollMin and SubRollMax both null means no sub-roll at all: the outcome always applies (the
/// rulebook's "Auto" entries, or an ExplorationResult with a single flat outcome and no branching).
/// </summary>
public class ExplorationOutcome
{
    public int Id { get; set; }
    public int ExplorationResultId { get; set; }

    public int? SubRollMin { get; set; }
    public int? SubRollMax { get; set; }

    public ExplorationOutcomeKind Kind { get; set; }

    /// <summary>Dice formula for a Kind.Gold outcome (e.g. "D6", "2D6x5", "D6x10"), or for a
    /// Kind.Wyrdstone outcome's bonus shard count (e.g. "D3", "D6+1") - same field reused for both
    /// rather than a redundant one, since a single ExplorationOutcome is never both at once. Parsed and
    /// rolled by the End of Game wizard, not here (Core.Models stays data-only).</summary>
    public string? GoldFormula { get; set; }

    /// <summary>English Name of an existing MordheimLedgerApp.Core.Models.Library.EquipmentItem for a
    /// Kind.Item outcome - resolved by lookup (never created) against the already-seeded Trading Post
    /// catalog, same find-by-name idiom as WarbandDetailViewModel.EndOfGame's Injury lookup.</summary>
    public string? EquipmentItemName { get; set; }

    /// <summary>Dice formula or fixed count for how many of EquipmentItemName are found (e.g. "D3", "1")
    /// - only meaningful alongside Kind.Item.</summary>
    public string? ItemQuantityFormula { get; set; }

    /// <summary>English Name of an existing SpecialRule (a "material rule" like "Gromril Weapon"/
    /// "Ithilmar Weapon" - see SpecialRules.json's CostMultiplier entries) applied on top of
    /// EquipmentItemName - e.g. the Exploration chart's "Gromril Axe" is really just a plain "Axe" with
    /// this rule attached, same idiom as WarriorEquipment.MaterialRule at purchase time. Null = the
    /// plain base item, no material.</summary>
    public string? MaterialRuleName { get; set; }

    /// <summary>Short disambiguating label shown alongside this branch - two uses: (1) a rare fallback
    /// for a branch that reads as Gold/Item in the rulebook but can't be wired up that way yet (Kind
    /// stays None, e.g. "Elven Cloak" for an item missing from the Trading Post catalog); (2) a context
    /// label on ANY Kind for an ExplorationResult whose branches are chosen by the player rather than by
    /// a sub-roll - several entries (e.g. "Straggler", "Tavern") are conditional on the warband's type
    /// or on a Leadership/Toughness test the app doesn't simulate, so every applicable branch is listed
    /// as its own Auto outcome (SubRollMin/Max both null) labelled with when it applies (e.g. "Skaven",
    /// "Test de Commandement réussi") and the player picks whichever matches, same "the player rolls
    /// physically and reports the outcome" idiom used for every dice field in the End of Game wizard.
    /// Deliberately not localized like the rest of this Library catalog: short/secondary content, not
    /// primary UI text - add a NoteKey translation slot later if that turns out to matter.</summary>
    public string? Note { get; set; }

    /// <summary>Only meaningful when the owning ExplorationResult.StatTestField is set - true = this
    /// branch applies when the chosen Hero's roll succeeded (roll &lt;= stat), false = when it failed,
    /// null = not a stat-test branch (every other Outcome in the table).</summary>
    public bool? StatTestPass { get; set; }

    /// <summary>True only for Puits' failure branch ("swallows tainted water and must miss the next
    /// game through sickness") - the End of Game wizard sets the chosen Hero's Warrior.Status to
    /// WarriorStatus.Sick when this branch resolves. False/default for every other branch, including
    /// every other stat-test failure in the table (Taverne/Bâtiment Éventré's failures don't sicken
    /// anyone, they just give less/nothing).</summary>
    public bool CausesSickness { get; set; }
}
