namespace MordheimLedgerApp.Services;

/// <summary>
/// Reference lookup for the rulebook's Heroes' progression-roll table (2D6, rolled immediately when
/// a Hero's XP crosses a milestone box on the printed track - see ExperienceMilestones). Purely
/// descriptive like SeriousInjuryTable: results needing a further 1D6 (6, 8, 9) or an open choice
/// (7, CC or CT) are left to the player to resolve and apply by hand via the existing stat/skill
/// editing UI (WarriorEditDialog) - no rules engine in V1, see CLAUDE.md.
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

    public static bool TryGet(int roll, out string text)
    {
        if (Array.IndexOf(Rolls, roll) < 0)
        {
            text = string.Empty;
            return false;
        }

        text = LocalizationService.Instance[$"AdvanceHero{roll}"];
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
