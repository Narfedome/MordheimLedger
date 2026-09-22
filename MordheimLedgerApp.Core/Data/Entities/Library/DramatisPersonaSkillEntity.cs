using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>One skill a Dramatis Persona already knows (e.g. Aenur: Strike to Injure) - references a
/// real Skill catalogue row, same shape as DramatisPersonaEquipmentEntity/DramatisPersonaSpecialRuleEntity.</summary>
public class DramatisPersonaSkillEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int DramatisPersonaId { get; set; }

    public int SkillId { get; set; }
}
