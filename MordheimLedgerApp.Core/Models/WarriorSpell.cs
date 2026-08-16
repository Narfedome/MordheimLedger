using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>A spell/prayer/ritual learned by a specific warrior (join between Warrior and the Spell
/// catalog) - a caster does NOT know its whole magic school table at once, it learns entries one at a
/// time via Advance rolls (see RulesReference/Campagne.md § Progression, "2-5 → Compétence (ou nouveau
/// sort aléatoire si Sorcier)"), same idea as WarriorSkill.</summary>
public class WarriorSpell
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public Spell Item { get; set; } = null!;

    /// <summary>Passe-plat vers Item.RollDisplay ("3 - Fireball"), pas juste Item.Name - contrairement à
    /// WarriorInjury.Name/WarriorSkill.Name, un sort connu affiche aussi le jet qui l'a déterminé (livre
    /// des règles : sort de départ/nouveau sort tirés au hasard, pas choisis - voir Spell.RollDisplay). Le
    /// seul consommateur de cette propriété est l'affichage en puce (ChipListView/ChipView, WarbandDetailPage/
    /// WarriorEditDialog) - pas de risque de double-préfixage ailleurs.</summary>
    public string Name => Item.RollDisplay;
}
