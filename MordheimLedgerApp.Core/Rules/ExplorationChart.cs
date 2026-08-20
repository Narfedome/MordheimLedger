using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// Reference lookup for the rulebook's Post-Battle "Income" procedure: how many D6 a warband rolls,
/// which single Exploration chart entry (if any) a roll triggers, and how many wyrdstone shards the
/// dice sum is worth. Pure dice-mechanics helpers, no dependency on ExplorationResult/Warband - the
/// End of Game wizard resolves the (DiceCount, Value) pair this returns against the seeded
/// ExplorationResult catalog (see Models.Library.ExplorationResult).
/// </summary>
public static class ExplorationChart
{
    /// <summary>1D6 per surviving Hero (never Henchmen) who didn't go out of action, +1D6 if the
    /// warband won its last battle, +bonusDice from other sources (skills/equipment/a pending
    /// Exploration bonus die) - hard-capped at 6 dice even if every source combined would allow
    /// more ("you must pick a maximum of six dice out of all the dice you roll").</summary>
    public static int ComputeDiceCount(int survivingHeroCount, bool wonLastGame, int bonusDice = 0) =>
        Math.Min(survivingHeroCount + (wonLastGame ? 1 : 0) + bonusDice, 6);

    public static int RollDie() => Random.Shared.Next(1, 7);

    /// <summary>Rolls the dice for a stat test (Puits/Toughness, Bâtiment Éventré's bonus Leadership
    /// test...) - 2D6 for a Commandement/Leadership test specifically (RulesReference "Tests de
    /// Commandement", p.23: "jet de 2D6, réussi si le total ≤ Cd" - an explicit exception, not the
    /// general rule below), 1D6 for every other stat (RulesReference "Tests de caractéristique": "jet de
    /// 1D6, réussi si résultat ≤ valeur de la caractéristique"). Used by both StatTestField (Puits) and
    /// BonusStatTestField (Bâtiment Éventré) - same rule regardless of which mechanism is asking.</summary>
    public static int RollStatTest(ExplorationStatField field) =>
        field == ExplorationStatField.Leadership ? RollDie() + RollDie() : RollDie();

    /// <summary>Whether a stat test roll succeeds - roll ≤ stat, EXCEPT a "Tests de caractéristique"
    /// roll of exactly 6 is ALWAYS an automatic failure regardless of the stat value (RulesReference
    /// "Tests de caractéristiques", p.23: "Sur un résultat de 6, le test est automatiquement raté, quelle
    /// que soit la valeur de la caractéristique"). That exception belongs to the general 1D6 rule only -
    /// "Tests de Commandement" (2D6, see RollStatTest) states no such exception, so a Leadership roll of
    /// 6 (a perfectly ordinary 2D6 sum, e.g. 2+4 or 3+3) is compared normally.</summary>
    public static bool PassesStatTest(ExplorationStatField field, int roll, int statValue)
    {
        if (field != ExplorationStatField.Leadership && roll == 6) return false;
        return roll <= statValue;
    }

    /// <summary>Finds the single Exploration chart entry (if any) triggered by a set of dice results -
    /// "choose the most numerous multiples if you score more than one set" (a triple always beats a
    /// double, regardless of face value), and "in case of two doubles or triples, look up the highest
    /// result" (tie on count broken by the higher face value). Never returns more than one entry - a
    /// roll triggers at most one row of the chart. Null = no two dice share the same face.</summary>
    public static (int DiceCount, int Value)? DetectMultiples(IReadOnlyList<int> dice)
    {
        var best = dice.GroupBy(d => d)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .FirstOrDefault();

        return best is null ? null : (best.Count(), best.Key);
    }

    /// <summary>Number Of Wyrdstone Shards Found table - keyed on the sum of every die rolled,
    /// independent of any multiples triggered by the same roll.</summary>
    public static int ShardsFound(int diceSum) => diceSum switch
    {
        <= 5 => 1,
        <= 11 => 2,
        <= 17 => 3,
        <= 24 => 4,
        <= 30 => 5,
        <= 35 => 6,
        _ => 7
    };
}
