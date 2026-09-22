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

    /// <summary>Resale price at the Trading Post ("Buy/Sell Equipment") - rulebook: "Warriors can
    /// automatically sell equipment for half its listed price. In the case of rare equipment and weapons
    /// which have a variable price, the warband receives half of the basic cost only" - always the BASE
    /// catalog cost × material multiplier, HALVED, never a FoundValueOverride roll even for a
    /// variable-price find (distinct from WarbandEquipment.SellValue, an older/narrower mechanic gated by
    /// IsSellable at FULL price for a different purpose - see its own doc, kept unchanged, 2026-09-05
    /// retour utilisateur explicite : les deux coexistent). Integer division rounds down (livre des
    /// règles - jamais de demi-couronne).</summary>
    public static int CalculateSellPrice(int baseCost, int? materialCostMultiplier) =>
        (baseCost * (materialCostMultiplier ?? 1)) / 2;
}
