using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

/// <summary>
/// Marks an EquipmentItem as restricted to a specific WarbandArchetype (e.g. "Hache Naine" - Dwarf
/// warbands only). Absence of any row for an item means it's common/unrestricted - matches the
/// behavior of every item seeded before this concept existed. Same shape as WarriorEquipmentEntity
/// (manual join, no real SQL join support in sqlite-net-pcl) - see LibraryService's bulk-load pattern
/// (load the whole table once, filter in-memory) rather than a FindAsync per item.
/// </summary>
public class WarbandArchetypeEquipmentEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandArchetypeId { get; set; }

    public int EquipmentItemId { get; set; }
}
