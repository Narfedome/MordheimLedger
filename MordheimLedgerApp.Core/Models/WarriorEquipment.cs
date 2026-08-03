using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>An equipment item carried by a specific warrior (join between Warrior and the catalog, e.g. "6 bullets").</summary>
public class WarriorEquipment
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public EquipmentItem Item { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}
