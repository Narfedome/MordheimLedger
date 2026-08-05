using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class MagicSchoolEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public string? DescriptionKey { get; set; }
    public ContentSource Source { get; set; }
    public string? ImagePath { get; set; }
}
