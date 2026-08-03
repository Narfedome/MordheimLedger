using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

public class HistoryEntryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandId { get; set; }

    public DateTime Date { get; set; }
    public string Text { get; set; } = string.Empty;
}
