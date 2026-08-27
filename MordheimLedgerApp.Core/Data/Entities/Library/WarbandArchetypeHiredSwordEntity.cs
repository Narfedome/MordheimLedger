using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Marks a HiredSword as hireable by a specific WarbandArchetype - see
/// WarbandArchetypeSkillEntity for the rationale, identical shape.</summary>
public class WarbandArchetypeHiredSwordEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int HiredSwordId { get; set; }
}
