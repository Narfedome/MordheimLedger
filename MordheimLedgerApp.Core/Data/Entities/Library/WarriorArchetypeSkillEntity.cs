using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Marks a Skill as restricted to a specific WarriorArchetype within its restricted warband(s)
/// - see WarbandArchetypeSkillEntity for the band-level equivalent, identical shape.</summary>
public class WarriorArchetypeSkillEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorArchetypeId { get; set; }

    public int SkillId { get; set; }
}
