using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class EquipmentListEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public string NameKey { get; set; } = string.Empty;
    public ContentSource Source { get; set; }

    /// <summary>Identifiant stable du contenu officiel (id slug du JSON de seed, ex. "equipment.sword"),
    /// jamais modifié ni réattribué - null pour une entrée créée par l'utilisateur.</summary>
    [Indexed]
    public string? OfficialId { get; set; }
}
