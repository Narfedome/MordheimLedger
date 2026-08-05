using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class MutationEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string? DescriptionKey { get; set; }
    public ContentSource Source { get; set; }
    public string? ImagePath { get; set; }
}
