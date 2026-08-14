using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Attaches a SpecialRule to an EquipmentItem - same shape/rationale as
/// AnimalSpecialRuleEntity.</summary>
public class EquipmentItemSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int EquipmentItemId { get; set; }

    public int SpecialRuleId { get; set; }
}
