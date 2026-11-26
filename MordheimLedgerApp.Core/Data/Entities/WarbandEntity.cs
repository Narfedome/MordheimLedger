using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

public class WarbandEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int? CampaignId { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Treasury { get; set; }
    public int WyrdstoneShards { get; set; }
    public bool PendingExplorationBonusDie { get; set; }
    public string? NextGameNote { get; set; }
    public bool HasCatacombReroll { get; set; }
    public bool GameInProgress { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    /// <summary>CSV des valeurs des bandes adverses saisies au début de partie (voir Warband.OpponentRatings).</summary>
    public string? OpponentRatings { get; set; }
    public string? Notes { get; set; }
}
