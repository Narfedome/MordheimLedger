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

    /// <summary>Dice formula for this Kind.Item outcome's found resale value, when it isn't just the
    /// catalog EquipmentItem.Cost as-is (e.g. Jewelsmith's Quartz Stones "D6x5 gc", Ruby "D6x15 gc") -
    /// rolled once by the player at find time, same idiom as GoldFormula, then stored per-instance on
    /// the resulting Models.WarbandEquipment row (see WarbandEquipment.FoundValueOverride) since a
    /// single catalog Cost can't represent a value that varies per find. Null (the common case) = Item.
    /// Cost/MaterialRuleName pricing applies unchanged (e.g. Jewelsmith's fixed-value Amethyst/
    /// Necklace).</summary>
    public string? FoundValueFormula { get; set; }

    /// <summary>English Name of an existing SpecialRule (a "material rule" like "Gromril Weapon"/
    /// "Ithilmar Weapon"/"Ornate Weapon" - see SpecialRules.json's CostMultiplier entries) applied on top
    /// of EquipmentItemName (and SecondaryEquipmentItemName, when present - same material for both, e.g.
    /// Overturned Cart's ornate sword+dagger) - e.g. the Exploration chart's "Gromril Axe" is really just
    /// a plain "Axe" with this rule attached, same idiom as WarriorEquipment.MaterialRule at purchase
    /// time. A material with SpecialRule.IsResaleUpgrade set additionally makes the resulting
    /// Models.WarbandEquipment row sellable (see IWarbandService.SellWarbandItemAsync) - reusing the same
    /// CostMultiplier rather than a separate ad-hoc resale field. Null = the plain base item, no
    /// material.</summary>
    public string? MaterialRuleName { get; set; }

    /// <summary>A second item granted by the same branch alongside the first, independent of Kind (e.g.
    /// Overturned Cart 5-6, Kind.Item: "a jewelled sword AND dagger"; Alchemist's Laboratory, Kind.Gold:
    /// gold AND an Alchemist's Notebook - see EquipmentItem.GrantsSkillCategory) - null for every other
    /// branch. Kept as a real catalog item rather than an invented bundle SKU so it can be
    /// equipped/sold separately; always found in quantity 1, unlike ItemQuantityFormula which only
    /// governs the primary EquipmentItemName.</summary>
    public string? SecondaryEquipmentItemName { get; set; }

    /// <summary>The player's choice between EquipmentItemName and THIS item instead - only one is ever
    /// granted (an "OR", unlike SecondaryEquipmentItemName's "AND"), e.g. Armourer 1-2: "D3 Shields or
    /// Bucklers (choose which)". Null for every other branch. Shares ItemQuantityFormula/MaterialRuleName
    /// with EquipmentItemName - the choice only picks which catalog item, not a different quantity or
    /// material.</summary>
    public string? AlternativeEquipmentItemName { get; set; }

    /// <summary>Short disambiguating label shown alongside this branch - two uses: (1) a rare fallback
    /// for a branch that reads as Gold/Item in the rulebook but can't be wired up that way yet (Kind
    /// stays None, e.g. "Elven Cloak" for an item missing from the Trading Post catalog); (2) a context
    /// label on ANY Kind for an ExplorationResult whose branches are conditional on the warband's type
    /// (e.g. "Skaven", "Undead") - one Auto outcome (SubRollMin/Max both null) per applicable warband,
    /// see RestrictedToWarbandArchetypeNames for how the wizard now picks the right one automatically
    /// (2026-08-20 - supersedes an earlier plan, described in an older revision of this comment, to show
    /// every branch and let the player pick manually: confirmed with the user that the rulebook's "a
    /// Skaven warband CAN..." phrasing means the branch is strictly determined by warband identity, not a
    /// free choice among all of them). Deliberately not localized like the rest of this Library catalog:
    /// short/secondary content, not primary UI text - add a NoteKey translation slot later if that turns
    /// out to matter.</summary>
    public string? Note { get; set; }

    /// <summary>English WarbandArchetype.Name(s) this branch is restricted to (e.g. "Skaven of Clan
    /// Eshin") - only meaningful when the owning ExplorationResult.RollsIndependently is true and at
    /// least one sibling Outcome also sets this (a "branch determined by warband identity" result:
    /// Straggler, Prisoners, Graveyard, Shrine's blessing). Empty = the catch-all branch for every
    /// warband not more specifically claimed by a sibling - see Core.Rules.ExplorationOutcomeResolver.
    /// ResolveWarbandOutcome. Plain string list resolved by name at consumption time, same idiom as
    /// EquipmentItemName/MaterialRuleName - this is fixed rulebook content with no editor, so no join
    /// table/Id resolution is needed (unlike EquipmentItem/Skill/Mutation's editable
    /// RestrictedToWarbandArchetypeIds).</summary>
    public List<string> RestrictedToWarbandArchetypeNames { get; set; } = new();

    /// <summary>True only for Straggler's "any other warband" branch ("interrogate the man... next time
    /// you roll on the Exploration chart, roll one dice more than usual and discard any one dice") - the
    /// End of Game wizard sets Warband.PendingExplorationBonusDie when this branch resolves, consumed as
    /// a +1 to Core.Rules.ExplorationChart.ComputeDiceCount's bonusDice the NEXT time this warband opens
    /// the End of Game wizard (see EndOfGameDialogViewModel.ExplorationDiceCount). False/default for
    /// every other branch.</summary>
    public bool GrantsNextExplorationBonusDie { get; set; }

    /// <summary>Fixed Experience granted directly to the warband's leader (Warrior.IsLeader), no roll
    /// involved - so far only Straggler's Possessed branch ("the leader gains +1 Experience"). Null for
    /// every other branch. Silently skipped (no error) if the leader isn't available this game (dead/
    /// sick/out of action), same "unavailable, not blocking" idiom as BonusStatTestLeader.</summary>
    public int? GrantsLeaderExperience { get; set; }

    /// <summary>English WarriorArchetype.Name of a Henchman that joins the warband for free - so far
    /// only Straggler's Undead branch ("gain a Zombie at no cost"). Resolved against the CURRENT
    /// warband's own roster (never another warband's), same plain-string-reference idiom as
    /// EquipmentItemName. If the warband already has a Henchman group of this same archetype, the new
    /// recruit merges into it (HeadCount + 1) rather than creating a separate row - Zombie-type Henchmen
    /// (CanUseEquipment false) never carry equipment that could tell two groups of the same archetype
    /// apart, so they're always the same group regardless. Null for every other branch.</summary>
    public string? GrantsFreeHenchmanArchetypeName { get; set; }

    /// <summary>Only meaningful when the owning ExplorationResult.StatTestField is set - true = this
    /// branch applies when the chosen Hero's roll succeeded (roll &lt;= stat), false = when it failed,
    /// null = not a stat-test branch (every other Outcome in the table).</summary>
    public bool? StatTestPass { get; set; }

    /// <summary>Only meaningful when the owning ExplorationResult.RequiresDoubleRoll is set - true =
    /// this branch applies when the paired 2D6 roll shows a double (e.g. Merchant's House's Order of
    /// Freetraders symbol), false/default = the normal branch. Same Pass/Fail-style pairing as
    /// StatTestPass, just keyed on a raw double instead of a stat comparison.</summary>
    public bool RequiresDoubleRoll { get; set; }

    /// <summary>True only for Puits' failure branch ("swallows tainted water and must miss the next
    /// game through sickness") - the End of Game wizard sets the chosen Hero's Warrior.Status to
    /// WarriorStatus.Sick when this branch resolves. False/default for every other branch, including
    /// every other stat-test failure in the table (Taverne/Bâtiment Éventré's failures don't sicken
    /// anyone, they just give less/nothing).</summary>
    public bool CausesSickness { get; set; }

    /// <summary>True only for the Pit's (La Fosse) "devoured" sub-roll branch (1 on a D6: "the Hero is
    /// devoured by the guardians of the Pit and never seen again") - the End of Game wizard sets the
    /// sent Hero's Warrior.Status to WarriorStatus.Dead when this branch resolves, same idiom as
    /// CausesSickness. Only ever meaningful alongside ExplorationResult.RequiresSentHero (the branch's
    /// consequence targets whichever Hero the player chose to send, not a Hero picked for a stat test).
    /// False/default for every other branch.</summary>
    public bool CausesDeath { get; set; }

    /// <summary>True marks a branch that grants a random entry from the rulebook's fixed 6-item Magical
    /// Artefacts table (Villa d'un Noble's 5-6 sub-roll; also referenced, but not yet wizard-wired
    /// since it needs Groupe B/RollsIndependently support, by Trésor Caché's "Artefact Magique (5+)"
    /// row) rather than a single fixed EquipmentItemName - the actual item only resolves once the
    /// player makes a SECOND, dedicated D6 roll on that table (see Core.Rules.MagicalArtefactTable).
    /// EquipmentItemName stays null on a branch like this. False/default for every other branch.</summary>
    public bool TriggersArtefactRoll { get; set; }
}
