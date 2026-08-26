namespace MordheimLedgerApp.Core.Rules;

/// <summary>
/// One of a warrior's 9 characteristics, used by the Advance mechanization
/// (HeroAdvanceTable/HenchmanAdvanceTable.TryGetOutcome, CharacteristicIncreaseRules) to name which
/// stat a roll/choice targets without resorting to raw strings. Movement is included even though no
/// Advance table entry ever targets it directly (the rulebook's tables only ever roll/choose among
/// WS/BS/S/T/W/I/A/Ld) - it becomes reachable only via the "both binary-choice options already at
/// their racial maximum" fallback (see CharacteristicIncreaseRules.ResolveBinaryChoice), the rulebook's
/// sole way to raise some races' Movement past its usual ceiling.
/// </summary>
public enum CharacteristicField
{
    Movement,
    WeaponSkill,
    BallisticSkill,
    Strength,
    Toughness,
    Wounds,
    Initiative,
    Attacks,
    Leadership
}
