using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Marks a Mutation as restricted to a specific WarbandArchetype (e.g. Kermesse du Chaos's
/// "Bénédictions de Nurgle"). Same shape/rationale as WarbandArchetypeEquipmentEntity.</summary>
public class WarbandArchetypeMutationEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int MutationId { get; set; }
}
