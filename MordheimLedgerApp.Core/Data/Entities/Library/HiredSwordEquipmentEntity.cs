using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>One piece of a HiredSword's fixed starting equipment (e.g. Pit Fighter: Morning star,
/// Helmet, Spiked gauntlet) - references a real EquipmentItem catalogue row, same idea as
/// EquipmentListItemEntity.</summary>
public class HiredSwordEquipmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int HiredSwordId { get; set; }

    public int EquipmentItemId { get; set; }
}
