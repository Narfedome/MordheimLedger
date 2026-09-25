using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class ExplorationResultEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int DiceCount { get; set; }
    public int Value { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public string? ShortDescriptionKey { get; set; }
    public ContentSource Source { get; set; }

    /// <summary>Identifiant stable du contenu officiel (id slug du JSON de seed, ex. "equipment.sword"),
    /// jamais modifié ni réattribué - null pour une entrée créée par l'utilisateur.</summary>
    [Indexed]
    public string? OfficialId { get; set; }
    public bool RollsIndependently { get; set; }
    public ExplorationStatField? StatTestField { get; set; }
    public bool StatTestTargetsLeader { get; set; }
    public string? AutoPassStatTestWarbandArchetypeNamesCsv { get; set; }
    public bool RequiresDoubleRoll { get; set; }
    public ExplorationStatField? BonusStatTestField { get; set; }
    public bool RequiresSentHero { get; set; }
}
