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
    /// threshold - see ExplorationOutcome's doc comment for the full mechanic. Some entries (e.g.
    /// "Straggler", "Tavern") have no sub-roll at all: every Outcome is Auto (SubRollMin/Max null) and
    /// the player just picks whichever applies (their warband's type, or the result of a Leadership/
    /// Toughness test the app doesn't simulate) - RollsIndependently is largely moot for these since
    /// there's no die involved either way, left false/default.</summary>
    public bool RollsIndependently { get; set; }

    /// <summary>Non-null = this result requires choosing a Hero and comparing a D6 roll against this
    /// stat (e.g. Puits/Toughness, Taverne and Bâtiment Éventré/Leadership) - the wizard shows a Hero
    /// picker + roll field instead of (or alongside) the usual sub-roll, computes pass/fail itself
    /// (comparing an already-known stat to an already-entered roll is arithmetic, not a decision made
    /// for the player - see EndOfGameDialogViewModel), and picks the Outcome whose StatTestPass matches.
    /// Null = no stat test, the vast majority of entries.</summary>
    public ExplorationStatField? StatTestField { get; set; }

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
