using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Core.Models;

/// <summary>Objet à usage unique pris au début de la partie en cours (Ombre Cramoisie, Ail, poison sur une arme, Bière de
/// Bugman pour la bande…). Vit le temps d'une partie : la fiche l'affiche (« Cette partie », caractéristiques en vert), la
/// Fin de partie retire l'objet de l'équipement puis supprime la ligne. Voir Services.SingleUseService.</summary>
public class GameItemUse
{
    public int Id { get; set; }
    public int WarbandId { get; set; }

    /// <summary>Null = toute la bande (SingleUseTarget.Warband).</summary>
    public int? WarriorId { get; set; }
    public int EquipmentItemId { get; set; }

    /// <summary>D'où vient l'exemplaire consommé : porté par un guerrier ou pris dans la réserve (un seul des deux).</summary>
    public int? WarriorEquipmentId { get; set; }
    public int? WarbandEquipmentId { get; set; }

    /// <summary>Poison : arme enduite (nom affiché, ex. « Épée »).</summary>
    public string? WeaponName { get; set; }

    /// <summary>Bonus tirés au début de partie (Ombre Cramoisie : I +2, M +1, F +1).</summary>
    public List<RolledStatModifier> StatModifiers { get; set; } = new();

    /// <summary>Coché quand même sur un guerrier d'une race « Sans effet sur » : l'objet est utilisé pour rien.</summary>
    public bool WithoutEffect { get; set; }
}
