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

    private static readonly int[] PromotionRolls = [10, 11, 12];

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

    /// <summary>True for the "this guy's gone up in the world" promotion rolls (10-12) - see
    /// PromotionRules.</summary>
    public static bool IsPromotion(int roll) => Array.IndexOf(PromotionRolls, roll) >= 0;

    /// <summary>Structured counterpart to TryGetTextKey - see AdvanceOutcome. Verified against the exact
    /// AdvanceHenchman{roll} flavor text already in AppStrings.resx: 2-4 Initiative (fixed), 5 Strength
    /// (fixed), 6-7 WS-or-BS player choice, 8 Attacks (fixed), 9 Leadership (fixed), 10-12 Promotion.</summary>
    public static bool TryGetOutcome(int roll, out AdvanceOutcome outcome)
    {
        outcome = roll switch
        {
            2 or 3 or 4 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.FixedSingle,
                FixedField = CharacteristicField.Initiative
            },
            5 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.FixedSingle,
                FixedField = CharacteristicField.Strength
            },
            6 or 7 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.BinaryChoice,
                OptionA = CharacteristicField.WeaponSkill,
                OptionB = CharacteristicField.BallisticSkill
            },
            8 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.FixedSingle,
                FixedField = CharacteristicField.Attacks
            },
            9 => new AdvanceOutcome
            {
                Kind = AdvanceKind.CharacteristicIncrease,
                ChoiceMode = CharacteristicChoiceMode.FixedSingle,
                FixedField = CharacteristicField.Leadership
            },
            10 or 11 or 12 => new AdvanceOutcome { Kind = AdvanceKind.Promotion },
            _ => null!
        };
        return outcome is not null;
    }

    /// <summary>Rolls 2D6.</summary>
    public static int RollDice() => Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);
}
