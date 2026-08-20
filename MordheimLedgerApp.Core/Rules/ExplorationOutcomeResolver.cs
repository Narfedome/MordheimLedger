using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// Which ExplorationOutcome branch of an already-triggered ExplorationResult applies, given whatever
/// the player has entered so far (a sub-roll, a stat test, a shared die also used for a bonus check).
/// Extracted from EndOfGameDialogViewModel (2026-08-18) after two bugs slipped through undetected there
/// - this logic is real rules resolution, not UI orchestration, and belongs where MordheimLedgerApp.Tests
/// can actually reach it (same "rules-to-Core" precedent as SeriousInjuryTable/HeroAdvanceTable, see
/// CLAUDE.md). ExplorationChart (dice count/multiples/shards) resolves WHICH ExplorationResult a roll
/// triggers; this class takes over once that's known, resolving WHICH of its Outcomes applies. Pure
/// functions only - the caller still owns all UI state (which field is bound to what, whether a roll has
/// been entered yet).
/// </summary>
public static class ExplorationOutcomeResolver
{
    /// <summary>The single branch without a sub-roll (SubRollMin null) - covers a result with exactly one
    /// flat outcome (e.g. Ruined Hovels) and the "always applies" half of a result that also has a
    /// sub-roll-gated bonus branch on the same die (e.g. Shop's gold, see ResolveBonusItemOutcome). Null
    /// for a result gated behind a stat test instead (see ResolveStatTestOutcome) - its Auto-shaped
    /// branches (Pass/Fail) must never resolve on their own before the test is actually made. Same
    /// deferral for a result gated behind a paired-dice double check (see ResolveDoubleRollOutcome) -
    /// e.g. Merchant's House's Gold/Item branches must wait for both dice, not resolve to whichever
    /// happens to come first in Outcomes.</summary>
    public static ExplorationOutcome? ResolveAutoOutcome(ExplorationResult result) =>
        result.StatTestField is not null || result.RequiresDoubleRoll ? null : result.Outcomes.FirstOrDefault(o => o.SubRollMin is null);

    /// <summary>The mutually-exclusive branch a single sub-roll picks (e.g. Corpse: 1-2 gold, 3 dagger,
    /// 4 axe...) - null if no branch's range contains the roll (shouldn't happen for a well-formed sub-
    /// roll table, since the rulebook's ranges are always exhaustive over 1-6).</summary>
    public static ExplorationOutcome? ResolveSubRollOutcome(ExplorationResult result, int subRoll) =>
        result.Outcomes.FirstOrDefault(o => o.SubRollMin.HasValue && subRoll >= o.SubRollMin && subRoll <= o.SubRollMax);

    /// <summary>A stat test (e.g. Well: roll &lt;= Toughness) compares an already-entered roll to an
    /// already-known stat - computing Pass/Fail here is arithmetic on values the player already
    /// supplied/the roster already has, not a random decision made on the player's behalf. Delegates to
    /// ExplorationChart.PassesStatTest for the actual comparison (including the "a 1D6 roll of 6 always
    /// fails" exception) - null if the result isn't actually stat-test-gated.</summary>
    public static ExplorationOutcome? ResolveStatTestOutcome(ExplorationResult result, int roll, int statValue)
    {
        if (result.StatTestField is not { } field) return null;
        var passed = ExplorationChart.PassesStatTest(field, roll, statValue);
        return result.Outcomes.FirstOrDefault(o => o.StatTestPass == passed);
    }

    /// <summary>Shop (2,2) is the one entry in the whole chart where an Auto branch (gold) and a
    /// sub-roll-gated branch (a bonus item) share the SAME die - "on a roll of 1 you ALSO find a Lucky
    /// Charm", not an alternative branch. <paramref name="resolvedAutoOutcome"/> must itself be the
    /// result's Auto Gold branch - passing anything else (e.g. Corpse's sub-roll-selected Gold branch)
    /// always returns null. This is a hard parameter shape, not a bool the caller could get wrong: bug
    /// found 2026-08-18 was exactly this check missing, so a Corpse gold roll of "4" spuriously matched
    /// Corpse's own sub-roll-4 Axe branch (same numeric value, completely unrelated die).</summary>
    public static ExplorationOutcome? ResolveBonusItemOutcome(ExplorationResult result, ExplorationOutcome? resolvedAutoOutcome, int roll)
    {
        if (resolvedAutoOutcome is not { Kind: ExplorationOutcomeKind.Gold, SubRollMin: null }) return null;
        return result.Outcomes.FirstOrDefault(o =>
            o.Kind == ExplorationOutcomeKind.Item && o.SubRollMin.HasValue && roll >= o.SubRollMin && roll <= o.SubRollMax);
    }

    /// <summary>Merchant's House (Maison du Marchand) is the one entry gated behind a paired 2D6 double
    /// check instead of a sub-roll or stat test - normally 2D6x5 gc, but a double instead grants the
    /// Order of Freetraders symbol. Comparing two already-entered dice for equality is arithmetic, not a
    /// decision made for the player - only meaningful when ExplorationResult.RequiresDoubleRoll is set,
    /// null otherwise (mirrors ResolveStatTestOutcome's shape for a Pass/Fail-style pair).</summary>
    public static ExplorationOutcome? ResolveDoubleRollOutcome(ExplorationResult result, int die1, int die2)
    {
        if (!result.RequiresDoubleRoll) return null;
        var isDouble = die1 == die2;
        return result.Outcomes.FirstOrDefault(o => o.RequiresDoubleRoll == isDouble);
    }

    /// <summary>Shattered Building (Bâtiment Éventré) is the one entry with an ADDITIONAL stat test for a
    /// bonus Outcome, on top of its Auto branch (D3 wyrdstone, always found) - "in addition, take a
    /// Leadership test... if passed, a Wardog joins", not an alternative branch (contrast
    /// ResolveStatTestOutcome, which GATES the whole resolution instead of adding to it). Only a Pass
    /// branch needs to exist in Outcomes - a Fail (or no roll yet) simply resolves to null, same "nothing
    /// happens" as every other stat-test failure with no consequence. Only meaningful when
    /// ExplorationResult.BonusStatTestField is set, null otherwise.</summary>
    public static ExplorationOutcome? ResolveBonusStatTestOutcome(ExplorationResult result, int roll, int statValue)
    {
        if (result.BonusStatTestField is not { } field) return null;
        var passed = ExplorationChart.PassesStatTest(field, roll, statValue);
        return result.Outcomes.FirstOrDefault(o => o.StatTestPass == passed);
    }
}
