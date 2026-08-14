namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// Reference lookup for the rulebook's Henchmen progression-roll table (2D6, rolled immediately when
/// a Henchman group's XP crosses a milestone box - see ExperienceMilestones). The result applies to
/// the whole group (stats capped at +1 each) rather than a single warrior - purely descriptive like
/// HeroAdvanceTable, including the 10-12 "Ce gars est doué" promotion (left to the player to execute
/// manually, no rules engine in V1). TryGetTextKey returns a localization resource key rather than
/// resolved text - Core stays MAUI/localization-free.
/// Verified against the rulebook via RulesReference/Campagne.md § Expérience.
/// </summary>
public static class HenchmanAdvanceTable
{
    private static readonly int[] Rolls = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

    public static bool TryGetTextKey(int roll, out string key)
    {
        if (Array.IndexOf(Rolls, roll) < 0)
        {
            key = string.Empty;
            return false;
        }

        key = $"AdvanceHenchman{roll}";
        return true;
    }

    /// <summary>Rolls 2D6.</summary>
    public static int RollDice() => Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);
}
