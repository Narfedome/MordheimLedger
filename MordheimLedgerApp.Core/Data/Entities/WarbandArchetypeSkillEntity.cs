using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Marks a Skill as restricted to a specific WarbandArchetype - see
/// WarbandArchetypeEquipmentEntity for the rationale, identical shape.</summary>
public class WarbandArchetypeSkillEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int SkillId { get; set; }
}
