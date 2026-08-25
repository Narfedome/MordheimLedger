namespace MordheimLedgerApp.Core.Rules;

/// <summary>Plain data-in/data-out snapshot of a warrior's current 9 characteristics - deliberately
/// decoupled from Models.Warrior so CharacteristicIncreaseRules stays a pure function over primitives,
/// same Core.Rules convention as the rest of this namespace.</summary>
public sealed record CharacteristicValues(
    int Movement, int WeaponSkill, int BallisticSkill, int Strength, int Toughness,
    int Wounds, int Initiative, int Attacks, int Leadership)
{
    public int Get(CharacteristicField field) => field switch
    {
        CharacteristicField.Movement => Movement,
        CharacteristicField.WeaponSkill => WeaponSkill,
        CharacteristicField.BallisticSkill => BallisticSkill,
        CharacteristicField.Strength => Strength,
        CharacteristicField.Toughness => Toughness,
        CharacteristicField.Wounds => Wounds,
        CharacteristicField.Initiative => Initiative,
        CharacteristicField.Attacks => Attacks,
        CharacteristicField.Leadership => Leadership,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    /// <summary>Returns a copy with +1 applied to the given field - used to fold in earlier,
    /// not-yet-persisted AdvanceRollEntry resolutions from the same End of Game pass (a warrior can
    /// cross several milestones at once, see WarriorOutcomeRow.MilestoneCount) without mutating the live
    /// Warrior before the wizard is actually saved.</summary>
    public CharacteristicValues Increment(CharacteristicField field) => field switch
    {
        CharacteristicField.Movement => this with { Movement = Movement + 1 },
        CharacteristicField.WeaponSkill => this with { WeaponSkill = WeaponSkill + 1 },
        CharacteristicField.BallisticSkill => this with { BallisticSkill = BallisticSkill + 1 },
        CharacteristicField.Strength => this with { Strength = Strength + 1 },
        CharacteristicField.Toughness => this with { Toughness = Toughness + 1 },
        CharacteristicField.Wounds => this with { Wounds = Wounds + 1 },
        CharacteristicField.Initiative => this with { Initiative = Initiative + 1 },
        CharacteristicField.Attacks => this with { Attacks = Attacks + 1 },
        CharacteristicField.Leadership => this with { Leadership = Leadership + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
}

/// <summary>Plain data-in/data-out snapshot of a racial profile's 9 characteristic maximums - mirrors
/// CharacteristicValues. Every field is nullable: null means no known ceiling to compare against -
/// either a free-text override race (Movement only, e.g. Cave Squigs' "2D6"), or (all 9) the archetype
/// has no resolved RacialProfile yet (RacialProfiles.json deliberately only covers creature types with
/// confirmed official numbers - see its own doc - or the archetype never reaches the Advance step at
/// all, e.g. GainsExperience false). Treated as never-maxed rather than always-maxed in both cases:
/// "we don't know the ceiling" must never silently block Progression the way "capped at 0" would.</summary>
public sealed record CharacteristicMaxes(
    int? Movement, int? WeaponSkill, int? BallisticSkill, int? Strength, int? Toughness,
    int? Wounds, int? Initiative, int? Attacks, int? Leadership)
{
    public int? Get(CharacteristicField field) => field switch
    {
        CharacteristicField.Movement => Movement,
        CharacteristicField.WeaponSkill => WeaponSkill,
        CharacteristicField.BallisticSkill => BallisticSkill,
        CharacteristicField.Strength => Strength,
        CharacteristicField.Toughness => Toughness,
        CharacteristicField.Wounds => Wounds,
        CharacteristicField.Initiative => Initiative,
        CharacteristicField.Attacks => Attacks,
        CharacteristicField.Leadership => Leadership,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
}

/// <summary>Result of resolving a BinaryChoice Advance result (Hero roll 7, Henchman rolls 6-7) against
/// a warrior's current stats/maximums - see CharacteristicIncreaseRules.ResolveBinaryChoice.</summary>
public sealed record BinaryChoiceResolution
{
    /// <summary>Exactly one of the two original options is still eligible - apply it directly, no
    /// further player choice needed.</summary>
    public CharacteristicField? ForcedField { get; init; }

    /// <summary>Both original options are already at their maximum (or, for a Henchman, already
    /// increased) - the rulebook lets the player pick any other eligible characteristic instead
    /// (Movement included, see CharacteristicField). Null unless this situation applies.</summary>
    public IReadOnlyList<CharacteristicField>? FallbackOptions { get; init; }

    /// <summary>Both original options are still eligible - the player freely picks between them (the
    /// ordinary case, no forcing or fallback needed).</summary>
    public bool RequiresFreeChoice => ForcedField is null && FallbackOptions is null;
}

/// <summary>Pure rules for applying an Advance CharacteristicIncrease result - racial maximums and (for
/// a Henchman group) the "never twice" restriction. See AdvanceOutcome for how a 2D6 roll resolves into
/// one of these shapes.</summary>
public static class CharacteristicIncreaseRules
{
    public static bool IsAtMax(CharacteristicField field, CharacteristicValues current, CharacteristicMaxes maxes)
    {
        var max = maxes.Get(field);
        return max is not null && current.Get(field) >= max.Value;
    }

    /// <summary>Henchman-only: a group may never add +1 to the same characteristic twice, on top of
    /// the ordinary racial-maximum check.</summary>
    public static bool IsEligibleForHenchmanIncrease(CharacteristicField field,
        IReadOnlyCollection<CharacteristicField> alreadyIncreased, CharacteristicValues current, CharacteristicMaxes maxes) =>
        !alreadyIncreased.Contains(field) && !IsAtMax(field, current, maxes);

    /// <summary>Resolves a BinaryChoice result's two options against current eligibility. Pass
    /// alreadyIncreased for a Henchman group (adds the "never twice" restriction on top of racial
    /// maximums) or null for a Hero (racial maximums only).</summary>
    public static BinaryChoiceResolution ResolveBinaryChoice(CharacteristicField optionA, CharacteristicField optionB,
        CharacteristicValues current, CharacteristicMaxes maxes, IReadOnlyCollection<CharacteristicField>? alreadyIncreased = null)
    {
        bool IsEligible(CharacteristicField field) => alreadyIncreased is null
            ? !IsAtMax(field, current, maxes)
            : IsEligibleForHenchmanIncrease(field, alreadyIncreased, current, maxes);

        var aEligible = IsEligible(optionA);
        var bEligible = IsEligible(optionB);

        if (aEligible && !bEligible) return new BinaryChoiceResolution { ForcedField = optionA };
        if (bEligible && !aEligible) return new BinaryChoiceResolution { ForcedField = optionB };
        if (!aEligible && !bEligible)
        {
            var fallback = Enum.GetValues<CharacteristicField>().Where(IsEligible).ToList();
            return new BinaryChoiceResolution { FallbackOptions = fallback };
        }

        return new BinaryChoiceResolution();
    }
}
