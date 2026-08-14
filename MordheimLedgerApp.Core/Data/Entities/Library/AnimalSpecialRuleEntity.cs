using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Attaches a SpecialRule to an Animal - same shape/rationale as
/// WarriorArchetypeSpecialRuleEntity.</summary>
public class AnimalSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int AnimalId { get; set; }

    public int SpecialRuleId { get; set; }
}
