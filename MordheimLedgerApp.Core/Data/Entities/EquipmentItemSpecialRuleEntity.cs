using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Attaches a SpecialRule to an EquipmentItem - same shape/rationale as
/// MountSpecialRuleEntity.</summary>
public class EquipmentItemSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int EquipmentItemId { get; set; }

    public int SpecialRuleId { get; set; }
}
