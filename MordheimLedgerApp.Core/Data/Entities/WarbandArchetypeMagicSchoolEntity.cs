using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Grants a WarbandArchetype access to a MagicSchool (e.g. Undead -> Nécromancie) - a direct
/// attach, same shape/rationale as WarbandArchetypeSpecialRuleEntity, not a "restricted to" list.</summary>
public class WarbandArchetypeMagicSchoolEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int MagicSchoolId { get; set; }
}
