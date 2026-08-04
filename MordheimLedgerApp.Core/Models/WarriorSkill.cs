using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>A skill or spell learned by a specific warrior (join between Warrior and the Library catalog).</summary>
public class WarriorSkill
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public Skill Item { get; set; } = null!;
}
