using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>A spell/prayer/ritual learned by a specific warrior (join between Warrior and the Spell
/// catalog) - a caster does NOT know its whole SpellListName table at once, it learns entries one at a
/// time via Advance rolls (see RulesReference/Campagne.md § Progression, "2-5 → Compétence (ou nouveau
/// sort aléatoire si Sorcier)"), same idea as WarriorSkill.</summary>
public class WarriorSpell
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public Spell Item { get; set; } = null!;
}
