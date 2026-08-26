using System.Text.RegularExpressions;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// Rolls the small dice-formula language used throughout the Exploration chart's ExplorationOutcome
/// fields (GoldFormula/ItemQuantityFormula/RecruitQuantityFormula) - "D6", "2D6", "D3", "D6+1",
/// "D6x10", "2D6x5", or a flat integer like "100". Grammar: optional dice count (default 1), "D",
/// side count, optional "x" (multiply the dice sum) or "+" (add a flat bonus) suffix - never both,
/// the rulebook's formulas only ever use one. A flat integer with no "D" is a fixed amount, not a
/// roll (e.g. Fighting Arena's "100 gc").
/// </summary>
public static partial class DiceFormula
{
    [GeneratedRegex(@"^(?:(\d*)D(\d+)(?:([x+])(\d+))?|(\d+))$")]
    private static partial Regex Pattern();

    public static int Roll(string formula)
    {
        var match = Pattern().Match(formula.Trim());
        if (!match.Success)
            throw new FormatException($"Invalid dice formula: '{formula}'");

        if (match.Groups[5].Success)
            return int.Parse(match.Groups[5].Value);

        var diceCount = match.Groups[1].Value.Length > 0 ? int.Parse(match.Groups[1].Value) : 1;
        var sides = int.Parse(match.Groups[2].Value);

        var sum = 0;
        for (var i = 0; i < diceCount; i++)
            sum += Random.Shared.Next(1, sides + 1);

        if (!match.Groups[3].Success) return sum;

        var operand = int.Parse(match.Groups[4].Value);
        return match.Groups[3].Value == "x" ? sum * operand : sum + operand;
    }

    /// <summary>Applies a formula's post-processing (the "x"/"+" suffix) to an ALREADY-KNOWN sum of dice
    /// values instead of rolling fresh ones - e.g. Merchant's House (Maison du Marchand): the player
    /// already typed both dice of "2D6x5" to check for a double (see ExplorationResult.
    /// RequiresDoubleRoll), so asking for a second, redundant roll to also determine the gold amount
    /// would contradict the rulebook - one 2D6 roll serves both purposes. diceValues should match the
    /// formula's own dice count, but nothing here enforces it (the caller already knows how many dice
    /// this formula expects).</summary>
    public static int Apply(string formula, IReadOnlyList<int> diceValues)
    {
        var match = Pattern().Match(formula.Trim());
        if (!match.Success)
            throw new FormatException($"Invalid dice formula: '{formula}'");

        if (match.Groups[5].Success)
            return int.Parse(match.Groups[5].Value);

        var sum = diceValues.Sum();
        if (!match.Groups[3].Success) return sum;

        var operand = int.Parse(match.Groups[4].Value);
        return match.Groups[3].Value == "x" ? sum * operand : sum + operand;
    }
}
