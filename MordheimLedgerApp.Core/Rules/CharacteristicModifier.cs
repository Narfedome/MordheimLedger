using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Adds delta to one of a Warrior's 9 characteristics - explicit switch rather than reflection,
/// same style as the rest of Core. No floor/ceiling check (a characteristic can drop below 0 as on a
/// real warband sheet; racial maximums are enforced upstream, see CharacteristicIncreaseRules). Shared
/// by the End of Game wizard (Serious Injury penalties) and the warband wizard's free mode (injuries
/// recorded for a warband imported from paper, 2026-09-24).</summary>
public static class CharacteristicModifier
{
    public static void Apply(Warrior warrior, CharacteristicField field, int delta)
    {
        switch (field)
        {
            case CharacteristicField.Movement: warrior.Movement += delta; break;
            case CharacteristicField.WeaponSkill: warrior.WeaponSkill += delta; break;
            case CharacteristicField.BallisticSkill: warrior.BallisticSkill += delta; break;
            case CharacteristicField.Strength: warrior.Strength += delta; break;
            case CharacteristicField.Toughness: warrior.Toughness += delta; break;
            case CharacteristicField.Wounds: warrior.Wounds += delta; break;
            case CharacteristicField.Initiative: warrior.Initiative += delta; break;
            case CharacteristicField.Attacks: warrior.Attacks += delta; break;
            case CharacteristicField.Leadership: warrior.Leadership += delta; break;
        }
    }
}
