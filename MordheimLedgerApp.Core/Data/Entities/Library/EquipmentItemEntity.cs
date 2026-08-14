using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class EquipmentItemEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public EquipmentCategory Category { get; set; }
    public int Cost { get; set; }
    public int? Rarity { get; set; }
    public int? CostRandomMax { get; set; }
    public string? DescriptionKey { get; set; }
    public ContentSource Source { get; set; }
    public string? ImagePath { get; set; }
    public bool IsFreeDagger { get; set; }
}
