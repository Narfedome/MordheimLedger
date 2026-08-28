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

    /// <summary>Short disambiguating label shown alongside this branch - an internal/technical tag (e.g.
    /// "Skavens : vente aux agents du Clan Eshin") used where a compact identifier is enough - not shown
    /// to the player as-is any more (see BranchText for that). Deliberately not localized: short/
    /// secondary content, French-only today regardless of UI language, acceptable since nothing user-
    /// facing reads it directly.</summary>
    public string? Note { get; set; }

    /// <summary>The rulebook's own full sentence for THIS branch alone (e.g. "A Skaven warband can sell
    /// the straggler to agents of Clan Eshin and gain 2D6 gc.") - only meaningful for a "conditioned by
    /// warband identity" result (RestrictedToWarbandArchetypeNames), shown in the wizard instead of the
    /// Note label so the player reads a real sentence, properly localized, rather than a terse tag.
    /// Contrast ExplorationResult.ShortDescription (the shared intro sentence every branch follows) -
    /// together they replace the full multi-branch Description on the wizard's Result step (see
    /// EndOfGameDialogViewModel.ExplorationResultDescriptionText). Retrofitted 2026-08-20 after Note
    /// turned out to matter for real UI display, not just an internal tag as originally planned - see
    /// BranchTextKey for the translation slot.</summary>
    public string? BranchText { get; set; }

    /// <summary>Translation slot backing BranchText - persistence-only, not for display.</summary>
    public string? BranchTextKey { get; set; }

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

    /// <summary>Dice formula for Experience distributed among ALL Heroes, as the player chooses to
    /// split it (e.g. Prisoners' Possessed branch: "D3 Experience distributed amongst their Heroes") -
    /// contrast GrantsLeaderExperience, a flat amount to the single leader only with no distribution
    /// choice. The wizard shows a roll field for the total (still the player's own physical roll, never
    /// auto-rolled) plus a +/- stepper per Hero (see EndOfGameDialogViewModel.
    /// DistributedExperienceRemaining, which must reach exactly 0 before the player can continue - a
    /// mockup confirmed this shape with the user 2026-08-20). Null for every other branch.</summary>
    public string? GrantsDistributedHeroExperienceFormula { get; set; }

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

    /// <summary>True only for Prisoners' "other warbands" catch-all branch ("one prisoner may join the
    /// warband as a new Henchman if you can equip him") - contrast GrantsFreeHenchmanArchetypeName (a
    /// FIXED archetype from the book, e.g. Zombie): here the player picks which of the warband's OWN
    /// existing Henchman groups the freed prisoner joins (RAW is ambiguous for a warband with no human
    /// Henchman group - Elves, Dwarfs... - resolved by simply listing whichever groups THIS warband
    /// actually has, no race gate). The recruit itself is free (no archetype Cost deducted, unlike a
    /// normal recruit) - only the cost of replicating the chosen group's CURRENT equipment loadout
    /// (Warrior.Equipment, shared per group) must fit the warband's treasury, confirmed via mockup
    /// (2026-08-21). Coexists with Kind.Gold on this same branch (the 2D6 escort fee) - not
    /// Kind.None-exclusive like GrantsDistributedHeroExperienceFormula/GrantsFreeHenchmanArchetypeName.
    /// False/default for every other branch.</summary>
    public bool GrantsOptionalEquippedHenchman { get; set; }

    /// <summary>Localized reminder shown as a banner on the warband's detail page (Warband.NextGameNote)
    /// from the moment this branch resolves until the NEXT End of Game is played - so far only
    /// Graveyard's catch-all branch ("the next time you play against Sisters of Sigmar or Witch Hunters,
    /// their entire warband will hate all your models"). A genuine "next game" consequence the app can't
    /// otherwise track (no opponent-identity concept exists) - kept as a plain reminder rather than a
    /// mechanized effect, same "no rules engine" boundary as everything else this simple. Null for every
    /// other branch.</summary>
    public string? NextGameNoteText { get; set; }

    /// <summary>Translation slot backing NextGameNoteText - persistence-only, not for display.</summary>
    public string? NextGameNoteTextKey { get; set; }

    /// <summary>True only for Shrine's Sisters of Sigmar/Witch Hunters branch ("she gains gc from her
    /// patrons and a blessing: one chosen weapon always wounds Undead or Possessed models on a to-wound
    /// roll of 2+") - coexists with Kind.Gold on this same branch (same 3D6 as the catch-all, only the
    /// blessing differs), same "independent of Kind" idiom as GrantsOptionalEquippedHenchman. The player
    /// picks one of a Hero's own already-carried weapons (never a Henchman group's - a group's Equipment
    /// is shared across several models, not a single weapon to bless - nor the warband's unassigned
    /// stash) via EndOfGameDialogViewModel.WeaponBlessingOptions; the chosen WarriorEquipment.MaterialRule
    /// is set to the "Blessed Weapon" SpecialRule (see SpecialRules.json), same mechanism as a Gromril/
    /// Ithilmar purchase rather than a bespoke bool flag - reuses the existing chip/abbreviation display
    /// as-is. False/default for every other branch.</summary>
    public bool GrantsWeaponBlessing { get; set; }

    /// <summary>True only for Entrance to the Catacombs' single universal branch ("from now on, you may
    /// re-roll one dice when you roll on the Exploration chart... a second and subsequent catacomb
    /// entrance does not grant additional re-rolls") - sets Warband.HasCatacombReroll permanently, unlike
    /// NextGameNoteText's one-game reminder. Per the user's explicit simplification (2026-08-21): the
    /// wizard only shows an informational reminder in the Exploration roll step (same idiom as
    /// PendingExplorationBonusDie's reminder) - the player re-rolls their own physical die and types the
    /// new value themselves, no dedicated re-roll button/logic. False/default for every other branch.</summary>
    public bool GrantsCatacombReroll { get; set; }

    /// <summary>True only for "Returning a Favour" ((3,6) - "you gain the services of any one Hired
    /// Sword... for the duration of the next battle, free of charge") - same "independent of Kind" idiom
    /// as GrantsOptionalEquippedHenchman/GrantsWeaponBlessing. Unlike GrantsFreeHenchmanArchetypeName (a
    /// FIXED archetype from the book), the player picks which Hired Sword type to engage at resolution
    /// time (EndOfGameDialogViewModel.Exploration.cs), since that's a real choice offered by the book
    /// ("choose from those available to your warband"), not a fixed name - resolved via
    /// WarbandService.RecruitHiredSwordAsync (free, HireCost not deducted) with Warrior.
    /// HiredSwordUpkeepPrepaid set so the very next End of Game's upkeep step shows it already covered.
    /// False/default for every other branch.</summary>
    public bool GrantsFreeHiredSword { get; set; }
}
