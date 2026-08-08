using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>An equipment item carried by a specific warrior (join between Warrior and the catalog, e.g. "6 bullets").</summary>
public class WarriorEquipment
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public EquipmentItem Item { get; set; } = null!;
    public int Quantity { get; set; } = 1;

    /// <summary>Null = Item's plain Cost applies. Non-null = a material (e.g. "Gromril", "Ithilmar")
    /// was chosen for this specific carried weapon at purchase time - see SpecialRule.CostMultiplier for
    /// the price math and WarriorEditDialogViewModel.AddEquipment for where it's applied. Any
    /// SpecialRule with CostMultiplier set is eligible, not just those two - the picker lists whatever
    /// the Library catalog currently has.</summary>
    public SpecialRule? MaterialRule { get; set; }
}
