using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

public class CampaignEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
