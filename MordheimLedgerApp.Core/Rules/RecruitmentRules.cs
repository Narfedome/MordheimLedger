namespace MordheimLedgerApp.Core.Rules;

/// <summary>Rulebook "Starting a Warband" roster caps - pure decision logic. The caller (App layer,
/// see WarbandEditDialogViewModel) is responsible for building the localized error message and
/// updating UI state; these helpers only answer yes/no.</summary>
public static class RecruitmentRules
{
    /// <summary>Can one more warrior of this type be added right now? Mirrors the three guards on the
    /// recruit "+" button: per-type MaxCount, band-wide MaxWarriors roster cap, and (for a brand-new
    /// warband) remaining treasury vs. cost - an existing warband being edited skips the treasury
    /// check.</summary>
    public static bool CanRecruit(int currentCountForType, int? maxCountForType, int currentTotalWarriors,
        int? maxWarriors, bool isExistingWarband, int remainingTreasury, int cost)
    {
        if (maxCountForType is { } max && currentCountForType >= max) return false;
        if (maxWarriors is { } maxTotal && currentTotalWarriors >= maxTotal) return false;
        if (!isExistingWarband && remainingTreasury < cost) return false;
        return true;
    }

    /// <summary>Roster large enough overall (WarbandArchetype.MinWarriors)?</summary>
    public static bool MeetsMinWarriors(int total, int? minWarriors) => total >= (minWarriors ?? 0);

    /// <summary>A mandatory warrior type (WarriorArchetype.MinCount, e.g. a unique leader) present in
    /// sufficient numbers? Types with no minimum (MinCount null or 0) are always satisfied.</summary>
    public static bool MeetsMinCount(int count, int? minCount) => minCount is not > 0 || count >= minCount;

    /// <summary>Gold left to spend during warband creation. In "Bande existante" mode (importing a
    /// warband already played on paper) the figure is whatever the player typed in directly
    /// (treasuryOverride) and is never decremented by recruits/purchases - only a brand-new warband's
    /// remaining treasury is startingTreasury minus everything spent so far.</summary>
    public static int CalculateRemainingTreasury(int startingTreasury, int totalSpent, bool isExistingWarband,
        int treasuryOverride) => isExistingWarband ? treasuryOverride : startingTreasury - totalSpent;

    /// <summary>Prisoners' "other warbands" catch-all branch (see Models.Library.ExplorationOutcome.
    /// GrantsOptionalEquippedHenchman): the freed prisoner joins as a Henchman at no recruitment cost -
    /// only the equipment needed to match the chosen group's current loadout must fit the treasury,
    /// unlike CanRecruit above (which also gates on an archetype Cost and roster caps).</summary>
    public static bool CanAffordEquippedHenchman(int availableTreasury, int equipmentCost) =>
        availableTreasury >= equipmentCost;
}
