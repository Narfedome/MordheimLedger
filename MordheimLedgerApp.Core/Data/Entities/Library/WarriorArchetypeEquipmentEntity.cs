using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Marks an EquipmentItem as restricted to a specific WarriorArchetype within its restricted
/// warband(s) or EquipmentList membership - see WarriorArchetypeSkillEntity for the rationale,
/// identical shape.</summary>
public class WarriorArchetypeEquipmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorArchetypeId { get; set; }

    public int EquipmentItemId { get; set; }
}
