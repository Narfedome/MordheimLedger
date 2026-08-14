using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Attaches a band-wide SpecialRule (applies to every warrior in the band, e.g. "Autonome" for
/// Ostlanders) to a WarbandArchetype - many-to-many, same shape as WarbandArchetypeEquipmentEntity.
/// Distinct from WarriorArchetypeSpecialRuleEntity, which attaches rules to one warrior type only.</summary>
public class WarbandArchetypeSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int SpecialRuleId { get; set; }
}
