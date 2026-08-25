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

    /// <summary>Structured counterpart to TryGetTextKey - see AdvanceOutcome. Verified against the exact
    /// AdvanceHero{roll} flavor text already in AppStrings.resx (itself already checked against the
    /// rulebook): 2-5/10-12 Skill, 6 Strength-or-Attacks sub-roll, 7 WS-or-BS player choice, 8
    /// Initiative-or-Leadership sub-roll, 9 Wounds-or-Toughness sub-roll.</summary>
    public static bool TryGetOutcome(int roll, out AdvanceOutcome outcome)
    {
        outcome = roll switch
        {
            2 or 3 or 4 or 5 or 10 or 11 or 12 => new AdvanceOutcome { Kind = AdvanceKind.Skill },
            6 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.SubRoll1D6,
                OptionA = CharacteristicField.Strength,
                OptionB = CharacteristicField.Attacks
            },
            7 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.BinaryChoice,
                OptionA = CharacteristicField.WeaponSkill,
                OptionB = CharacteristicField.BallisticSkill
            },
            8 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.SubRoll1D6,
                OptionA = CharacteristicField.Initiative,
                OptionB = CharacteristicField.Leadership
            },
            9 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.SubRoll1D6,
                OptionA = CharacteristicField.Wounds,
                OptionB = CharacteristicField.Toughness
            },
            _ => null!
        };
        return outcome is not null;
    }

    /// <summary>Rolls 2D6.</summary>
    public static int RollDice() => Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);

    /// <summary>Rolls 1D6, for the roll-6/8/9 sub-rolls above (1-3 = OptionA, 4-6 = OptionB).</summary>
    public static int RollSubDie() => Random.Shared.Next(1, 7);
}
