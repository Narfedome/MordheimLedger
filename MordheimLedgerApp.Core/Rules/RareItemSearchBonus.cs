using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Total bonus to a future Rare Item search roll from whatever a Warrior currently carries -
/// e.g. the Jewelsmith's gems (Bijoutier) grant +1 each if kept instead of sold (see EquipmentItem.
/// GrantsRareItemSearchBonus). The Rare Item search feature itself isn't built yet (Trading Post) -
/// this helper exists so the bonus already works, live, the moment that feature calls it - same
/// carried-equipment idiom as SkillEligibility.EffectiveAllowedCategories, nothing baked into Warrior
/// at find time.</summary>
public static class RareItemSearchBonus
{
    public static int EffectiveBonus(Warrior warrior) =>
        warrior.Equipment.Sum(e => e.Item.GrantsRareItemSearchBonus ?? 0);
}
