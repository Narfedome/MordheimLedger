using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Join row between a Warrior and an EquipmentItem — see Models.WarriorEquipment.</summary>
public class WarriorEquipmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorId { get; set; }

    public int EquipmentItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public int? MaterialSpecialRuleId { get; set; }
    public int? FoundValueOverride { get; set; }
}
