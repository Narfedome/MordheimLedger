namespace MordheimLedgerApp.Core.Rules;

/// <summary>Generalised from Bertha Bestraufrung's own recruitment table (Dramatis Personae, Grade 1A) -
/// a pure lookup, not a formula: some Dramatis Personae only agree to personally join a battle when the
/// hiring warband is genuinely outmatched (represented as "enemy Rating minus own Rating"), rolling a D6
/// against a threshold that gets easier the further behind the hiring warband is. Extracted here (rather
/// than hardcoded prose on Bertha alone) so any future Dramatis Persona sharing this exact shape - see
/// DramatisPersona.RequiresRatingDisadvantage - reuses the same table instead of re-describing it, same
/// "pure/testable lookup" precedent as WyrdstoneSaleTable/HeroAdvanceTable. Still "resolve the numbers,
/// no rules engine V1 beyond that" - the actual audience-seeking roll (a normal special-character search
/// against the seeking Hero's Initiative) and the willingness roll below both stay player-rolled/manual,
/// this table only tells the player what they needed to score.</summary>
public static class RatingGapAidTable
{
    /// <summary>One breakpoint of the table: a Rating gap of at least MinDifference requires rolling
    /// RequiredRoll+ on a D6. Data, not prose - StartGameDialog renders this list directly (one row per
    /// Tier, plus a "won't come" row below the first Tier's MinDifference) instead of a hand-written resx
    /// sentence, so the on-screen explanation can never drift out of sync with GetRequiredRoll below (both
    /// read the same source of truth). Source: Bertha's own table, p.146ish (Tome of Heroes).</summary>
    public readonly record struct Tier(int MinDifference, int RequiredRoll);

    /// <summary>Ascending by MinDifference - a gap below Tiers[0].MinDifference means "won't come" (see
    /// GetRequiredRoll returning null).</summary>
    public static readonly IReadOnlyList<Tier> Tiers = new[]
    {
        new Tier(50, 6),
        new Tier(100, 5),
        new Tier(150, 4),
        new Tier(200, 3)
    };

    /// <summary>Null = won't come at all (the hiring warband isn't behind enough to need the help).
    /// Otherwise the D6 score required, easier as the gap widens.</summary>
    public static int? GetRequiredRoll(int ratingDifference)
    {
        Tier? matched = null;
        foreach (var tier in Tiers)
        {
            if (ratingDifference >= tier.MinDifference)
                matched = tier;
        }
        return matched?.RequiredRoll;
    }
}
