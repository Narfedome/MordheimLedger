using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Join row between a Warband and an EquipmentItem, unassigned to any warrior — see
/// Models.WarbandEquipment.</summary>
public class WarbandEquipmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandId { get; set; }

    public int EquipmentItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public int? MaterialSpecialRuleId { get; set; }
    public int? SellMultiplier { get; set; }
}
