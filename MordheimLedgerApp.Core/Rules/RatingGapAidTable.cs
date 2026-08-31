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
    /// <summary>Null = won't come at all (the hiring warband isn't behind enough to need the help).
    /// Otherwise the D6 score required, easier as the gap widens - source: Bertha's own table, p.146ish
    /// (Tome of Heroes).</summary>
    public static int? GetRequiredRoll(int ratingDifference) => ratingDifference switch
    {
        < 50 => null,
        < 100 => 6,
        < 150 => 5,
        < 200 => 4,
        _ => 3
    };
}
