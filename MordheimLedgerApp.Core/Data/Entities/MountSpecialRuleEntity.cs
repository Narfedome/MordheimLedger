using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Attaches a SpecialRule to a Mount - same shape/rationale as
/// WarriorArchetypeSpecialRuleEntity.</summary>
public class MountSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int MountId { get; set; }

    public int SpecialRuleId { get; set; }
}
