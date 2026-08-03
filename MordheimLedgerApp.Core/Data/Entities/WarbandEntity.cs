using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

public class WarbandEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WarbandType { get; set; } = string.Empty;
    public int Treasury { get; set; }
    public bool IsCustom { get; set; }
    public string? Notes { get; set; }
}
