using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Marks a Mount as restricted to a specific WarbandArchetype (e.g. "Sanglier de guerre" -
/// Orques only). Same shape/rationale as WarbandArchetypeEquipmentEntity.</summary>
public class WarbandArchetypeMountEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int MountId { get; set; }
}
