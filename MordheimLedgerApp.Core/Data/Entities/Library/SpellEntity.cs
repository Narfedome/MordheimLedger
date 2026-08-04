using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class SpellEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public string? DescriptionKey { get; set; }

    [Indexed]
    public string SpellListName { get; set; } = string.Empty;

    public int RollValue { get; set; }
    public int? Difficulty { get; set; }
    public ContentSource Source { get; set; }
    public string? ImagePath { get; set; }
}
