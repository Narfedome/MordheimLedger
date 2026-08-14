namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// Reference lookup for the rulebook's Heroes' progression-roll table (2D6, rolled immediately when
/// a Hero's XP crosses a milestone box on the printed track - see ExperienceMilestones). Purely
/// descriptive like SeriousInjuryTable: results needing a further 1D6 (6, 8, 9) or an open choice
/// (7, CC or CT) are left to the player to resolve and apply by hand via the existing stat/skill
/// editing UI (WarriorEditDialog) - no rules engine in V1, see CLAUDE.md. TryGetTextKey returns a
/// localization resource key rather than resolved text - Core stays MAUI/localization-free.
/// Verified against the rulebook via RulesReference/Campagne.md § Expérience.
/// </summary>
public static class HeroAdvanceTable
{
    private static readonly int[] Rolls = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    private static readonly int[] SkillRolls = [2, 3, 4, 5, 10, 11, 12];

    /// <summary>True for the "Compétence" results (2-5, 10-12) - the only Advance rolls where the app
    /// lets the player pick an actual Skill from the Library and attach it directly, rather than just
    /// showing descriptive text (see EndOfGameDialogViewModel.PickAdvanceSkill).</summary>
    public static bool IsSkill(int roll) => Array.IndexOf(SkillRolls, roll) >= 0;

    public static bool TryGetTextKey(int roll, out string key)
    {
        if (Array.IndexOf(Rolls, roll) < 0)
        {
            key = string.Empty;
            return false;
        }

        key = $"AdvanceHero{roll}";
        return true;
    }

    /// <summary>Rolls 2D6.</summary>
    public static int RollDice() => Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);
}
