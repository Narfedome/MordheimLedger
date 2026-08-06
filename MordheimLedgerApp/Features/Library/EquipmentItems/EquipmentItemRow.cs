using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Resources.Icons;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

/// <summary>
/// Tuile de grille (EquipmentItemView) : IsSelected est portée par la ligne elle-même
/// (SelectionMode="None" sur le CollectionView), pas la sélection native - cf.
/// SelectableGridItemBorderStyle pour la raison (teinte colorAccent Android non évitable via
/// VisualStateManager seul).
/// </summary>
public partial class EquipmentItemRow : ObservableObject
{
    public EquipmentItem Item { get; }

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - "CO"/"GC" en toutes
    /// lettres plutôt qu'une icône (pièces trop peu distinguable à la taille d'une tuile). Rarity absent
    /// (= Common) n'ajoute rien, pas de "R0" trompeur.</summary>
    public string CostDisplay
    {
        get
        {
            var abbr = LocalizationService.Instance["LibGoldCrownsAbbr"];
            return Item.Rarity.HasValue ? $"{Item.Cost} {abbr} · R{Item.Rarity}" : $"{Item.Cost} {abbr}";
        }
    }

    /// <summary>Icône de tuile par Category plutôt qu'un unique glyphe "Coins" pour tout le catalogue
    /// (pas pertinent pour une arme/armure) - un seul glyphe générique "Box" pour Divers, catégorie trop
    /// hétérogène (livres, potions, reliques...) pour un glyphe dédié.</summary>
    public string CategoryIcon => Item.Category switch
    {
        EquipmentCategory.MeleeWeapon => RpgFont.RaSword,
        EquipmentCategory.MissileWeapon => RpgFont.RaCrossbow,
        EquipmentCategory.BlackPowderWeapon => RpgFont.RaRifle,
        EquipmentCategory.Ammunition => RpgFont.RaAmmoBag,
        EquipmentCategory.Armour => RpgFont.RaVest,
        _ => SolidFont.Box
    };

    public string CategoryIconFont => Item.Category == EquipmentCategory.MiscellaneousEquipment ? "FontSolid" : "RpgAwesome";

    public EquipmentItemRow(EquipmentItem item) => Item = item;
}
