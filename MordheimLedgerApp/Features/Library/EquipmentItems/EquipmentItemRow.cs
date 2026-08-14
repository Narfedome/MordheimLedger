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

    /// <summary>Toujours géré à la main par EquipmentItemViewModel (CRUD : une seule ligne à la fois via
    /// SelectedRow ; picker : Quantity &gt; 0) - jamais dérivé automatiquement de Quantity, pour ne pas
    /// coupler la sélection simple de l'onglet Library au mécanisme de quantité, propre au picker.</summary>
    [ObservableProperty]
    private bool isSelected;

    /// <summary>Combien d'exemplaires de cet objet le joueur a choisis d'acheter dans cette session de
    /// picker (mode multi-sélection uniquement, voir EquipmentItemViewModel.Select/IncrementQuantity/
    /// DecrementQuantity) - 0 = pas sélectionné. Permet d'acheter plusieurs exemplaires du même objet
    /// (ex. 2 épées longues) sans fermer/rouvrir le picker. Toujours 0 dans l'onglet Library (CRUD).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CostDisplay))]
    [NotifyPropertyChangedFor(nameof(TotalCost))]
    [NotifyPropertyChangedFor(nameof(CanDecrement))]
    private int quantity;

    /// <summary>Le stepper +/- reste affiché en permanence en mode picker (même à Quantity == 0, "+"
    /// sélectionne et incrémente en un seul geste) - CanDecrement grise juste "-" tant qu'il n'y a rien à
    /// retirer, plutôt que de masquer tout le stepper avant la première sélection.</summary>
    public bool CanDecrement => Quantity > 0;

    /// <summary>True when this row is the Dagger and this picker session is a warband purchase
    /// (EquipmentItemViewModel.AvailableGold set) for a target that doesn't already carry one
    /// (EquipmentItemViewModel.AlreadyHasFreeDagger) - shows "Gratuit"/"Free" instead of the catalog
    /// price. Never true in the Library's own Trading Post tab (no AvailableGold there), where the tile
    /// should keep showing the plain catalog price - see EquipmentItemViewModel.ApplyFilter.</summary>
    public bool IsFreeForThisPurchase { get; }

    /// <summary>Coût total de CETTE tuile pour la Quantity actuelle - contribue à
    /// EquipmentItemViewModel.BudgetDisplay et à ConfirmSelection (via
    /// Enumerable.Repeat(Item, Quantity)). Pour un objet éligible à la dague gratuite, seul le premier
    /// exemplaire est gratuit ; tout exemplaire supplémentaire compte au prix normal (voir
    /// EquipmentItem.IsFreeDagger) - pour tout autre objet, simple Quantity × Cost.</summary>
    public int TotalCost => IsFreeForThisPurchase ? Math.Max(0, Quantity - 1) * Item.Cost : Quantity * Item.Cost;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - "CO"/"GC" en toutes
    /// lettres plutôt qu'une icône (pièces trop peu distinguable à la taille d'une tuile). Rarity absent
    /// (= Common) n'ajoute rien, pas de "R0" trompeur. Pour un objet éligible à la dague gratuite, affiche
    /// le coût TOTAL de la tuile (pas un prix unitaire) une fois Quantity ≥ 2, puisque seul le premier
    /// exemplaire est gratuit - "Gratuit" tant que Quantity ≤ 1.</summary>
    public string CostDisplay
    {
        get
        {
            if (IsFreeForThisPurchase)
            {
                return Quantity <= 1
                    ? LocalizationService.Instance["LibFreePh"]
                    : $"{TotalCost} {LocalizationService.Instance["LibGoldCrownsAbbr"]}";
            }

            var abbr = LocalizationService.Instance["LibGoldCrownsAbbr"];
            var cost = Item.CostRandomMax is { } max ? $"{Item.Cost}-{Item.Cost + max}" : Item.Cost.ToString();
            return Item.Rarity.HasValue ? $"{cost} {abbr} · R{Item.Rarity}" : $"{cost} {abbr}";
        }
    }

    /// <summary>Icône de tuile par Category plutôt qu'un unique glyphe "Coins" pour tout le catalogue
    /// (pas pertinent pour une arme/armure) - même mapping que EquipmentCategoryIconConverter (puces
    /// objet ailleurs dans l'app), à garder synchronisé.</summary>
    public string CategoryIcon => Item.Category switch
    {
        EquipmentCategory.MeleeWeapon => RpgFont.RaSword,
        EquipmentCategory.MissileWeapon => RpgFont.RaCrossbow,
        EquipmentCategory.BlackPowderWeapon => RpgFont.RaMusket,
        EquipmentCategory.Ammunition => RpgFont.RaArrowCluster,
        EquipmentCategory.Armour => RpgFont.RaVest,
        _ => RpgFont.RaPotion
    };

    public string CategoryIconFont => "RpgAwesome";

    public EquipmentItemRow(EquipmentItem item, bool isFreeForThisPurchase = false)
    {
        Item = item;
        IsFreeForThisPurchase = isFreeForThisPurchase;
    }
}
