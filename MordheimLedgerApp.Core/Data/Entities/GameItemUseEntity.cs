using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Voir Models.GameItemUse.</summary>
public class GameItemUseEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandId { get; set; }

    [Indexed]
    public int? WarriorId { get; set; }
    public int EquipmentItemId { get; set; }
    public int? WarriorEquipmentId { get; set; }
    public int? WarbandEquipmentId { get; set; }
    public string? WeaponName { get; set; }

    /// <summary>« Initiative:2;Movement:1 » - voir GameItemUse.StatModifiers.</summary>
    public string? StatModifiers { get; set; }
    public bool WithoutEffect { get; set; }
}
