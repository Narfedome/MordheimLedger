namespace MordheimLedgerApp.Core.Rules;

/// <summary>Rulebook weapon-purchase pricing: the free first dagger ("in addition to his free dagger")
/// and optional material upgrades (Gromril, Ithilmar...) that multiply the base cost. Previously
/// duplicated across EquipmentPick.Cost, MaterialChoice, and both WarriorEditDialogViewModel/
/// WarbandEditDialogViewModel.AddEquipment - consolidated here.</summary>
public static class EquipmentPricing
{
    /// <summary>True only for the first Dagger a warrior/group picks up - the rulebook's free dagger is
    /// singular, so any further Dagger purchase (dual-wielding) is priced normally. Callers additionally
    /// require no material was chosen on this pick before treating it as actually free (a Gromril/
    /// Ithilmar dagger is a deliberate upgrade, never the free baseline) - see CalculateCost.</summary>
    public static bool IsFreeDaggerEligible(bool isFreeDaggerItem, bool alreadyCarriesFreeDagger) =>
        isFreeDaggerItem && !alreadyCarriesFreeDagger;

    /// <summary>Base cost multiplied by the chosen material's CostMultiplier (no material/Normal = ×1),
    /// or 0 when this purchase is the free dagger.</summary>
    public static int CalculateCost(int baseCost, int? materialCostMultiplier, bool isFree) =>
        isFree ? 0 : baseCost * (materialCostMultiplier ?? 1);
}
