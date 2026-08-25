using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Attaches a SpecialRule to an Injury (e.g. Stupidity/Frenzy from Madness, 24) - same
/// shape/rationale as EquipmentItemSpecialRuleEntity.</summary>
public class InjurySpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int InjuryId { get; set; }

    public int SpecialRuleId { get; set; }
}
