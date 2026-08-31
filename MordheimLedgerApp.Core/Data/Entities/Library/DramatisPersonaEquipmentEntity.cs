using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>One piece of a Dramatis Persona's fixed starting equipment (e.g. Aenur: Ithilmar Armour,
/// Elven Cloak, Ienh-Khain) - references a real EquipmentItem catalogue row, same idea as
/// HiredSwordEquipmentEntity.</summary>
public class DramatisPersonaEquipmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int DramatisPersonaId { get; set; }

    public int EquipmentItemId { get; set; }
}
