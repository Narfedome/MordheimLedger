using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Many-to-many: an EquipmentItem (e.g. a common item like Dagger) can be a member of many
/// EquipmentLists across many warbands - see WarbandArchetypeSkillEntity for the rationale, identical
/// shape.</summary>
public class EquipmentListItemEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int EquipmentListId { get; set; }

    public int EquipmentItemId { get; set; }
}
