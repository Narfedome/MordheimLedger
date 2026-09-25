using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class WarbandArchetypeEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public ContentSource Source { get; set; }

    /// <summary>Identifiant stable du contenu officiel (id slug du JSON de seed, ex. "equipment.sword"),
    /// jamais modifié ni réattribué - null pour une entrée créée par l'utilisateur.</summary>
    [Indexed]
    public string? OfficialId { get; set; }
    public WarbandGrade Grade { get; set; }
    public int StartingTreasury { get; set; }
    public int? MaxWarriors { get; set; }
    public int? MinWarriors { get; set; }
    public string? DescriptionKey { get; set; }
    public string? ImagePath { get; set; }
    public int RaceId { get; set; }
}
