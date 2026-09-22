using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>Attaches a SpecialRule specific to one Dramatis Persona (e.g. "Causes Fear" on Marianna) -
/// many-to-many, same shape as HiredSwordSpecialRuleEntity.</summary>
public class DramatisPersonaSpecialRuleEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int DramatisPersonaId { get; set; }

    public int SpecialRuleId { get; set; }
}
