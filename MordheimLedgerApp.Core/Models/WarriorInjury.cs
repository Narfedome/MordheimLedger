using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>A permanent injury carried by a specific warrior (join between Warrior and the Library catalog).</summary>
public class WarriorInjury
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public Injury Item { get; set; } = null!;
}
