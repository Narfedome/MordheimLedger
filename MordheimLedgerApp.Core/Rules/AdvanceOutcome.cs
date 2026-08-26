namespace MordheimLedgerApp.Core.Rules;

/// <summary>What kind of Advance result a 2D6 roll produced - see HeroAdvanceTable/
/// HenchmanAdvanceTable.TryGetOutcome.</summary>
public enum AdvanceKind
{
    /// <summary>Pick a new Skill from an available table, or (Hero Wizard only) a new permanent Spell
    /// instead - see EndOfGameDialogViewModel.Advance's skill-or-spell buttons.</summary>
    Skill,

    /// <summary>+1 to one characteristic - which one, and how it's resolved, is given by ChoiceMode/
    /// OptionA/OptionB/FixedField below.</summary>
    CharacteristicIncrease,

    /// <summary>Henchman-only (10-12): one group member becomes a Hero - see PromotionRules.</summary>
    Promotion
}

/// <summary>How a CharacteristicIncrease result's target field is determined - only meaningful when
/// AdvanceOutcome.Kind is CharacteristicIncrease.</summary>
public enum CharacteristicChoiceMode
{
    /// <summary>A further 1D6 decides between OptionA (1-3) and OptionB (4-6) - the player has no
    /// choice, just resolves the die (Hero rolls 6/8/9).</summary>
    SubRoll1D6,

    /// <summary>The player freely picks between OptionA and OptionB (Hero roll 7, Henchman rolls 6-7) -
    /// see CharacteristicIncreaseRules.ResolveBinaryChoice for what happens when one or both are
    /// already at their racial maximum (Henchman: or already increased).</summary>
    BinaryChoice,

    /// <summary>A single specific characteristic, no choice or sub-roll involved (Henchman rolls
    /// 2-4/5/8/9).</summary>
    FixedSingle
}

/// <summary>Structured result of resolving a Hero/Henchman Advance 2D6 roll against
/// HeroAdvanceTable/HenchmanAdvanceTable - drives EndOfGameDialogViewModel.Advance's UI (which
/// sub-roll/choice/skill-or-spell affordance to show) and WarbandDetailViewModel.EndOfGame's actual
/// stat mutation. Deliberately additive alongside the existing TryGetTextKey/IsSkill/RollDice (still
/// used for the flavor-text subtitle) rather than replacing them.</summary>
public sealed record AdvanceOutcome
{
    public required AdvanceKind Kind { get; init; }
    public CharacteristicChoiceMode? ChoiceMode { get; init; }

    /// <summary>SubRoll1D6: the field on a sub-roll of 1-3. BinaryChoice: one of the two options the
    /// player picks between. Null for FixedSingle/Skill/Promotion.</summary>
    public CharacteristicField? OptionA { get; init; }

    /// <summary>SubRoll1D6: the field on a sub-roll of 4-6. BinaryChoice: the other option. Null for
    /// FixedSingle/Skill/Promotion.</summary>
    public CharacteristicField? OptionB { get; init; }

    /// <summary>FixedSingle only - the one characteristic this result increases, no choice involved.</summary>
    public CharacteristicField? FixedField { get; init; }
}
