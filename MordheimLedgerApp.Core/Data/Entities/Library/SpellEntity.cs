using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class SpellEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public string? DescriptionKey { get; set; }

    [Indexed]
    public int MagicSchoolId { get; set; }

    public int RollValue { get; set; }
    public int? Difficulty { get; set; }
    public ContentSource Source { get; set; }

    /// <summary>Identifiant stable du contenu officiel (id slug du JSON de seed, ex. "equipment.sword"),
    /// jamais modifié ni réattribué - null pour une entrée créée par l'utilisateur.</summary>
    [Indexed]
    public string? OfficialId { get; set; }
    public string? ImagePath { get; set; }
}
