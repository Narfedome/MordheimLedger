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
    public string? Notes { get; set; }
}
