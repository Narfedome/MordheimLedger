using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Marks a DramatisPersona as hireable by a specific WarbandArchetype - see
/// WarbandArchetypeHiredSwordEntity for the rationale, identical shape.</summary>
public class WarbandArchetypeDramatisPersonaEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int DramatisPersonaId { get; set; }
}
