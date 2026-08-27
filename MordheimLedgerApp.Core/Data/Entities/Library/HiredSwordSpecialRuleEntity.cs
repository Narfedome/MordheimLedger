using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Attaches a SpecialRule specific to one Hired Sword type (e.g. "Deathwish" on the Dwarf Troll
/// Slayer) - many-to-many, same shape as WarriorArchetypeSpecialRuleEntity.</summary>
public class HiredSwordSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int HiredSwordId { get; set; }

    public int SpecialRuleId { get; set; }
}
