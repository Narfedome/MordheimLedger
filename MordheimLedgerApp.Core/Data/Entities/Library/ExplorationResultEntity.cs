using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class ExplorationResultEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int DiceCount { get; set; }
    public int Value { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public ContentSource Source { get; set; }
    public bool RollsIndependently { get; set; }
    public ExplorationStatField? StatTestField { get; set; }
    public bool RequiresDoubleRoll { get; set; }
}
