namespace MordheimLedgerApp.Core.Rules;

/// <summary>Warband Rating (rulebook: warband strength score used for scenario selection/matched play)
/// - pure calculation, single source of truth for the formula previously duplicated (and disagreeing)
/// between WarbandDetailViewModel's inline Sum and WarbandService.GetWarbandRatingAsync. Unified
/// behavior (2026-08-27): multiplies by HeadCount (a Henchman group of N models weighs N times, matching
/// the service-side formula) and counts Sick warriors alongside Active ones (only Dead/Retired are
/// excluded - matching WarbandDetailViewModel's Heroes/Henchmen row sets, which already include Sick).</summary>
public static class WarbandRatingRules
{
    /// <summary>Rating contribution of one Warrior row. hiredSwordBaseRating non-null (a Hired Sword,
    /// see Models.Warrior.HiredSwordBaseRating) replaces the usual "20 for a Large Creature, else 5"
    /// per-model base with the catalogue's own BaseRating (e.g. Pit Fighter: 22) - isLargeCreature is
    /// ignored in that case, a Hired Sword is never also a Large Creature. headCount multiplies the
    /// whole per-model contribution (always 1 for a Hero or a Hired Sword, the living model count for a
    /// Henchman group).</summary>
    public static int WarriorContribution(bool isLargeCreature, int experience, int headCount, int? hiredSwordBaseRating)
    {
        var perModel = hiredSwordBaseRating is { } baseRating ? baseRating + experience : (isLargeCreature ? 20 : 5) + experience;
        return perModel * headCount;
    }
}
