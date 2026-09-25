using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class SkillEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public SkillCategory Category { get; set; }
    public string? DescriptionKey { get; set; }
    public ContentSource Source { get; set; }

    /// <summary>Identifiant stable du contenu officiel (id slug du JSON de seed, ex. "equipment.sword"),
    /// jamais modifié ni réattribué - null pour une entrée créée par l'utilisateur.</summary>
    [Indexed]
    public string? OfficialId { get; set; }
    public string? ImagePath { get; set; }

    /// <summary>Comma-separated WarbandArchetype ids - see Skill.HatredTargetWarbandArchetypeIds.</summary>
    public string? HatredTargetWarbandArchetypeIds { get; set; }
}
