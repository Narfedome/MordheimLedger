using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class WarbandArchetypeEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContentSource Source { get; set; }
    public int StartingTreasury { get; set; }
    public int? MaxWarriors { get; set; }
    public string? Description { get; set; }
}
