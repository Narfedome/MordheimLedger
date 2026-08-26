namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// One entry of the rulebook's Exploration chart (Post-Battle Sequence, Income section) - triggered
/// when a warband's Exploration roll (see the End of Game wizard) lands on two or more dice showing the
/// same value. DiceCount/Value identify which entry a given roll maps to (e.g. three 5's -&gt;
/// DiceCount=3, Value=5 -&gt; "Market Hall"). Description carries the full rulebook text verbatim (every
/// branch included) as the authoritative reference, shown to the player for most entries - see
/// ShortDescription for the one case where the wizard shows something else instead. Outcomes covers the
/// branches worth mechanizing (gold/wyrdstone/a specific EquipmentItem, and - since 2026-08-20, see
/// Straggler - leader Experience and a free Henchman recruit too) - permanent warband traits, unlocked
/// skill lists etc. still stay pure text, same "no rules engine" boundary used for combat/stat rules
/// elsewhere, not extended to treasury/equipment/roster bookkeeping which the app already models well
/// (decision confirmed 2026-08-17, reversing an earlier "descriptive text only" take once it became
/// clear the boundary was meant for combat rules specifically).
///
/// Seeded from the rulebook only for now - no Library editor screen yet (same precedent as
/// SeriousInjuryTable/HeroAdvanceTable: reference content consumed by the wizard). Source is kept ready
/// for one regardless, same Official/Modified/Custom convention as the rest of the Library.
/// </summary>
public class ExplorationResult
{
    public int Id { get; set; }

    /// <summary>2 = double, 3 = triple, 4 = four of a kind, 5 = five of a kind, 6 = six of a kind.</summary>
    public int DiceCount { get; set; }

    /// <summary>1-6, the repeated die face.</summary>
    public int Value { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Just the shared intro sentence every branch follows (e.g. Straggler: "Your warband
    /// encounters one of the survivors of Mordheim, who has lost his sanity along with all his worldly
    /// possessions.") - null for every entry except a "conditioned by warband identity" result (see
    /// ExplorationOutcome.RestrictedToWarbandArchetypeNames/BranchText). The wizard's Result step shows
    /// this + the resolved branch's own BranchText instead of the full multi-branch Description for
    /// those entries (retrofitted 2026-08-20 after showing the full Description there turned out to
    /// bury the one branch that actually applies to the playing warband among three others that don't -
    /// see EndOfGameDialogViewModel.ExplorationResultDescriptionText). Description itself is untouched,
    /// still the authoritative full-text reference.</summary>
    public string? ShortDescription { get; set; }

    /// <summary>Translation slot backing Name/Description/ShortDescription - persistence-only, not for
    /// display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }
    public string? ShortDescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>False (most entries) = a single roll picks exactly one mutually exclusive Outcome.
    /// True (e.g. "Hidden Treasure") = every Outcome is checked independently against its own
    /// threshold - see ExplorationOutcome's doc comment for the full mechanic. "Straggler" also sets
    /// this (every Outcome Auto, SubRollMin/Max null) for a THIRD, unrelated shape - see
    /// ExplorationOutcome.RestrictedToWarbandArchetypeNames/Core.Rules.ExplorationOutcomeResolver.
    /// ResolveWarbandOutcome. "Tavern" does NOT set this any more (2026-08-20): it's a plain
    /// StatTestField-gated test like Well, just leader-targeted - see StatTestTargetsLeader.</summary>
    public bool RollsIndependently { get; set; }

    /// <summary>Non-null = this result requires comparing a D6 (or 2D6 for Leadership) roll against this
    /// stat (e.g. Puits/Toughness, Taverne/Leadership) - the wizard shows a roll field (plus, for Puits,
    /// a Hero picker - see StatTestTargetsLeader) instead of (or alongside) the usual sub-roll, computes
    /// pass/fail itself (comparing an already-known stat to an already-entered roll is arithmetic, not a
    /// decision made for the player - see EndOfGameDialogViewModel), and picks the Outcome whose
    /// StatTestPass matches. Null = no stat test, the vast majority of entries.</summary>
    public ExplorationStatField? StatTestField { get; set; }

    /// <summary>True = this StatTestField-gated test always targets the warband's leader (Warrior.
    /// IsLeader), never a Hero the player picks (e.g. Taverne/Commandement: "The warband's leader must
    /// take a Leadership test") - the wizard sets StatTestHero automatically instead of showing
    /// StatTestEligibleHeroes' picker (see EndOfGameDialogViewModel.ShowStatTestHeroPicker). If the
    /// leader isn't available this game (dead/sick/out of action), the test is simply skipped, no
    /// blocking error - same "unavailable, not forced" idiom as BonusStatTestField's leader lookup.
    /// False (e.g. Puits/Endurance) = the player picks who takes the test. Meaningless when StatTestField
    /// is null.</summary>
    public bool StatTestTargetsLeader { get; set; }

    /// <summary>English WarbandArchetype.Name(s) that automatically pass this StatTestField-gated test,
    /// no roll needed (e.g. Taverne: "Undead, Witch Hunter and Sisters of Sigmar warbands automatically
    /// pass this test") - the wizard resolves straight to the Outcome whose StatTestPass is true as soon
    /// as the result triggers, skipping the roll field entirely (see EndOfGameDialogViewModel.
    /// StatTestAutoPasses). Empty (almost every entry) = no such exception, the test always requires a
    /// roll. Plain string reference resolved at consumption time, same idiom as
    /// ExplorationOutcome.RestrictedToWarbandArchetypeNames - fixed rulebook content, no editor.</summary>
    public List<string> AutoPassStatTestWarbandArchetypeNames { get; set; } = new();

    /// <summary>True = this result's Auto branches are mutually exclusive on whether a paired 2D6 roll
    /// shows a double (e.g. Merchant's House - Maison du Marchand: normal roll -&gt; 2D6x5 gc, a double
    /// -&gt; the Order of Freetraders symbol instead) - defers ResolveAutoOutcome exactly like
    /// StatTestField does, until the wizard's two-dice input resolves which Outcome.RequiresDoubleRoll
    /// value applies (see Core.Rules.ExplorationOutcomeResolver.ResolveDoubleRollOutcome). False (the
    /// vast majority) = no such check, StatTestField/RollsIndependently/plain sub-roll resolve as usual.</summary>
    public bool RequiresDoubleRoll { get; set; }

    /// <summary>Non-null = this result ALSO offers an additional stat test for a bonus Outcome, on top
    /// of whatever its Auto/sub-roll branches already resolve (e.g. Shattered Building - Bâtiment
    /// Éventré: D3 wyrdstone always, "in addition" a Leadership test that may grant a Wardog) - contrast
    /// StatTestField, which GATES the whole resolution instead of adding to it. Always tests the
    /// warband LEADER specifically (Warrior.IsLeader), never a Hero the player picks - no other rulebook
    /// entry needs a different target yet. See Core.Rules.ExplorationOutcomeResolver.
    /// ResolveBonusStatTestOutcome. Null (the vast majority) = no such bonus test.</summary>
    public ExplorationStatField? BonusStatTestField { get; set; }

    /// <summary>True = this result requires choosing a Hero to send in BEFORE its sub-roll can resolve
    /// (e.g. the Pit - La Fosse: "you can send one Hero to search for wyrdstone... on a 1 he's devoured")
    /// - unlike StatTestField/BonusStatTestField, no stat is compared, the Hero is simply put at risk by
    /// whichever branch the sub-roll picks (see ExplorationOutcome.CausesDeath). Sending is OPTIONAL per
    /// the rulebook ("if you wish") - the wizard just leaves the sub-roll hidden and the branch
    /// unresolved (no reward, no risk) until a Hero is actually chosen, never forcing a pick. False (the
    /// vast majority) = no such requirement, the sub-roll (if any) is available immediately.</summary>
    public bool RequiresSentHero { get; set; }

    public List<ExplorationOutcome> Outcomes { get; set; } = new();
}
