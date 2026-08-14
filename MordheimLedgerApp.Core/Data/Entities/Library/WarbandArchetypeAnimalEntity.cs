using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Marks an Animal as restricted to a specific WarbandArchetype (e.g. "Sanglier de guerre" -
/// Orques only). Same shape/rationale as WarbandArchetypeEquipmentEntity.</summary>
public class WarbandArchetypeAnimalEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int AnimalId { get; set; }
}
