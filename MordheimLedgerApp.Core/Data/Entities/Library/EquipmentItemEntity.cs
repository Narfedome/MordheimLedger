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

    /// <summary>Only populated when Category is Animal - see EquipmentItem's equivalent fields.</summary>
    public int? Movement { get; set; }
    public int? WeaponSkill { get; set; }
    public int? BallisticSkill { get; set; }
    public int? Strength { get; set; }
    public int? Toughness { get; set; }
    public int? Wounds { get; set; }
    public int? Initiative { get; set; }
    public int? Attacks { get; set; }
    public int? Leadership { get; set; }
}
