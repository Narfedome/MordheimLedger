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
    [NotifyPropertyChangedFor(nameof(CanIncrement))]
    private int quantity;

    /// <summary>True pour la tuile "Réserve" épinglée en haut du picker (EquipmentItemViewModel.
    /// ReserveQuantities) - une instance SÉPARÉE de celle de la catégorie normale pour le même Item
    /// (jamais la même, voir ApplyFilter) : la Réserve est un stock limité et gratuit (retour utilisateur
    /// 2026-09-21 - "la réserve c'est un nombre d'objet limité qu'on a d'avance et qui ne coûte rien,
    /// l'achat lui est dispo pour autant d'item que l'on souhaite tant qu'on a les golds"), donc bornée à
    /// ReserveAvailable et toujours affichée "Gratuit" - contrairement à la tuile de catégorie normale du
    /// même objet, qui reste un achat classique sans plafond, au prix catalogue. Les deux peuvent être
    /// choisies en même temps (ex. 2 épées prises en réserve + 1 achetée), chacune contribuant sa propre
    /// Quantity à la liste retournée par ConfirmSelection.</summary>
    public bool IsReserveRow { get; }

    /// <summary>Combien d'exemplaires sont réellement en réserve - plafond de Quantity pour une tuile
    /// IsReserveRow, jamais consulté sinon (0 par défaut, sans effet sur une tuile de catégorie
    /// normale).</summary>
    public int ReserveAvailable { get; }

    /// <summary>Matériau réellement associé à l'exemplaire en réserve (ex. Gromril, ou une Épée Ornée
    /// trouvée à l'Exploration) - null si le stock est une variante ordinaire sans matériau. Retour
    /// utilisateur 2026-09-21 - "dans le sélecteur on affiche aussi si il y a un matériau sur l'arme" :
    /// affiché en suffixe sur NameDisplay, jamais choisi par le joueur ici (voir AddRecruitEquipment's
    /// ResolveAvailableReserveMaterial, qui l'attache automatiquement au pick à la confirmation - même
    /// simplification aux deux endroits : un seul matériau représentatif si plusieurs variantes
    /// coexistaient en réserve pour le même Item, cas rare non géré séparément).</summary>
    public SpecialRule? ReserveMaterialRule { get; }

    /// <summary>Nom affiché sur la tuile - suffixé "(abréviation du matériau)" pour une tuile Réserve dont
    /// le stock a un matériau (ex. "Épée (Ornée)"), comme WarbandEquipment/WarriorEquipment.NameDisplay
    /// ailleurs dans l'app. Jamais suffixé pour une tuile de catégorie normale (achat neuf, sans matériau
    /// choisi ici).</summary>
    public string NameDisplay => ReserveMaterialRule?.Abbreviation is { Length: > 0 } abbr ? $"{Item.Name} ({abbr})" : Item.Name;

    /// <summary>Le stepper +/- reste affiché en permanence en mode picker (même à Quantity == 0, "+"
    /// sélectionne et incrémente en un seul geste) - CanDecrement grise juste "-" tant qu'il n'y a rien à
    /// retirer, plutôt que de masquer tout le stepper avant la première sélection. CanIncrement grise "+"
    /// une fois ReserveAvailable atteint pour une tuile Réserve (jamais de plafond pour un achat
    /// normal).</summary>
    public bool CanDecrement => Quantity > 0;
    public bool CanIncrement => !IsReserveRow || Quantity < ReserveAvailable;

    /// <summary>True when this row is the Dagger and this picker session is a warband purchase
    /// (EquipmentItemViewModel.AvailableGold set) for a target that doesn't already carry one
    /// (EquipmentItemViewModel.AlreadyHasFreeDagger) - shows "Gratuit"/"Free" instead of the catalog
    /// price. Never true in the Library's own Trading Post tab (no AvailableGold there), where the tile
    /// should keep showing the plain catalog price - see EquipmentItemViewModel.ApplyFilter.</summary>
    public bool IsFreeForThisPurchase { get; }

    /// <summary>Coût total de CETTE tuile pour la Quantity actuelle - contribue à
    /// EquipmentItemViewModel.BudgetDisplay et à ConfirmSelection (via
    /// Enumerable.Repeat(Item, Quantity)). Toujours 0 pour une tuile Réserve (stock déjà possédé, jamais
    /// facturé). Pour un objet éligible à la dague gratuite, seul le premier exemplaire est gratuit ; tout
    /// exemplaire supplémentaire compte au prix normal (voir EquipmentItem.IsFreeDagger) - pour tout autre
    /// objet, simple Quantity × Cost.</summary>
    public int TotalCost => IsReserveRow ? 0 : IsFreeForThisPurchase ? Math.Max(0, Quantity - 1) * Item.Cost : Quantity * Item.Cost;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - "CO"/"GC" en toutes
    /// lettres plutôt qu'une icône (pièces trop peu distinguable à la taille d'une tuile). Rarity absent
    /// (= Common) n'ajoute rien, pas de "R0" trompeur. Toujours "Gratuit" pour une tuile Réserve. Pour un
    /// objet éligible à la dague gratuite, affiche le coût TOTAL de la tuile (pas un prix unitaire) une
    /// fois Quantity ≥ 2, puisque seul le premier exemplaire est gratuit - "Gratuit" tant que Quantity ≤ 1.</summary>
    public string CostDisplay
    {
        get
        {
            if (IsReserveRow) return LocalizationService.Instance["LibFreePh"];

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
        EquipmentCategory.Animal => RpgFont.RaPawprint,
        EquipmentCategory.Consumable => RpgFont.RaVial,
        EquipmentCategory.DrugsAndPoisons => RpgFont.RaPoisonCloud,
        EquipmentCategory.MagicalArtefact => RpgFont.RaRuneStone,
        _ => RpgFont.RaPotion
    };

    public string CategoryIconFont => "RpgAwesome";

    public EquipmentItemRow(EquipmentItem item, bool isFreeForThisPurchase = false)
    {
        Item = item;
        IsFreeForThisPurchase = isFreeForThisPurchase;
    }

    /// <summary>Tuile "Réserve" - voir IsReserveRow's own doc.</summary>
    public EquipmentItemRow(EquipmentItem item, int reserveAvailable, SpecialRule? reserveMaterialRule = null)
    {
        Item = item;
        IsReserveRow = true;
        ReserveAvailable = reserveAvailable;
        ReserveMaterialRule = reserveMaterialRule;
    }
}
