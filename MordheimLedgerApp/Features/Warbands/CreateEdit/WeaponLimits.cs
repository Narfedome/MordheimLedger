using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Rulebook "Starting a Warband" - weapons and armour: "up to two close combat weapons... up to
/// two different missile weapons" per warrior. Informational only, never blocks a purchase - some
/// warband special rules (e.g. Skaven "Tail Fighting") let a warrior exceed this, and the app has no
/// rules engine to model every such exception (see project conventions). Callers show a non-blocking
/// warning when ExceedsLimits returns true; the purchase itself always goes through.</summary>
internal static class WeaponLimits
{
    private const int MaxMeleeWeapons = 2;
    private const int MaxMissileWeaponTypes = 2;

    /// <summary>items = everything the warrior currently carries (after the purchase being checked).
    /// Melee weapons are capped by count (a warrior may carry two of the same weapon, e.g. dual daggers);
    /// missile/black-powder weapons are capped by distinct type ("two different missile weapons") -
    /// a brace of pistols already counts as a single missile weapon as long as it's seeded as one
    /// EquipmentItem, so no special-casing is needed here. Exactly one Dagger (EquipmentItem.
    /// IsFreeDagger) is exempt from the melee count - the rulebook's cap is "two close combat weapons IN
    /// ADDITION TO his free dagger" (singular): a second, deliberately bought dagger for dual-wielding
    /// counts normally like any other melee weapon.</summary>
    public static bool ExceedsLimits(IEnumerable<EquipmentItem> items)
    {
        var list = items.ToList();
        var meleeCount = list.Count(i => i.Category == EquipmentCategory.MeleeWeapon);
        if (list.Any(i => i.IsFreeDagger)) meleeCount--;
        var missileTypeCount = list
            .Where(i => i.Category is EquipmentCategory.MissileWeapon or EquipmentCategory.BlackPowderWeapon)
            .Select(i => i.Id)
            .Distinct()
            .Count();
        return meleeCount > MaxMeleeWeapons || missileTypeCount > MaxMissileWeaponTypes;
    }
}
