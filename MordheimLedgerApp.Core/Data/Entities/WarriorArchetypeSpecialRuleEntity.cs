using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Attaches a SpecialRule specific to one warrior type (e.g. "Chef" on a Captain) to a
/// WarriorArchetype - many-to-many, same shape as WarbandArchetypeSpecialRuleEntity but scoped to a
/// single archetype rather than the whole band.</summary>
public class WarriorArchetypeSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorArchetypeId { get; set; }

    public int SpecialRuleId { get; set; }
}
