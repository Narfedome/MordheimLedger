using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>A permanent injury carried by a specific warrior (join between Warrior and the Library catalog).</summary>
public class WarriorInjury
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public Injury Item { get; set; } = null!;

    /// <summary>Passe-plat vers Item.Name - ChipView (composant de puce partagé) lie son Label
    /// directement sur Name, quel que soit le type réel qu'on lui passe.</summary>
    public string Name => Item.Name;
}
