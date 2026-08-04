namespace MordheimLedgerApp.Services;

/// <summary>
/// Reference lookup for the rulebook's Henchmen progression-roll table (2D6, rolled immediately when
/// a Henchman group's XP crosses a milestone box - see ExperienceMilestones). The result applies to
/// the whole group (stats capped at +1 each) rather than a single warrior - purely descriptive like
/// HeroAdvanceTable, including the 10-12 "Ce gars est doué" promotion (left to the player to execute
/// manually, no rules engine in V1).
/// Verified against the rulebook via RulesReference/Campagne.md § Expérience.
/// </summary>
public static class HenchmanAdvanceTable
{
    private static readonly int[] Rolls = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

    public static bool TryGet(int roll, out string text)
    {
        if (Array.IndexOf(Rolls, roll) < 0)
        {
            text = string.Empty;
            return false;
        }

        text = LocalizationService.Instance[$"AdvanceHenchman{roll}"];
        return true;
    }

    /// <summary>Rolls 2D6 and looks up the result.</summary>
    public static (int Roll, string Text) Roll()
    {
        var roll = Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);
        TryGet(roll, out var text);
        return (roll, text);
    }
}
