using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Une ligne vendable à l'étape Achat/Vente - une ligne de la réserve (ReserveKey : réserve
/// d'avant la partie, trouvaille d'Exploration, objet du scénario, équipement rendu par un renvoi - voir
/// EndOfGamePageViewModel.BuildSellableCandidates) ou un objet porté par un guerrier ACTIF (CarriedItem).
/// Jamais un objet ACHETÉ pendant cette même Fin de Partie : annuler un achat retire simplement le pick,
/// ce n'est jamais une "vente" (retour utilisateur 2026-09-05 - "si on fait un achat et que finalement on
/// change d'avis, on considère que l'objet n'est pas acheté"). Une trouvaille de cette partie, elle, EST
/// vendable (retour utilisateur 2026-09-21), cohérent avec le livre des règles ("trade in weapons and
/// equipment... swapped around the warband").
///
/// **Vente partielle** (retour utilisateur 2026-09-21, revu par rapport à une première version "ligne
/// entière uniquement" - "une sélection avec un affichage en tuile comme dans le codex, avec - et + comme
/// sur les équipements, mais à la différence qu'on ne peut pas ajouter des équipements inexistants, juste
/// réduire ou non") : SelectedQuantity (stepper sur SellEquipmentSelectorPage) va de 0 à OwnedQuantity,
/// jamais au-delà - contrairement au stepper d'achat standard (EquipmentItemView.xaml), qui n'a pas de
/// plafond réel côté catalogue. Vendre moins que OwnedQuantity laisse le reste en place (réserve ou
/// porté) ; voir EndOfGamePageViewModel.Apply.ApplyEquipmentTradingAsync pour la réduction de quantité
/// à Terminer (supprime la ligne existante puis recrée le reliquat, aucune méthode de service dédiée à la
/// réduction partielle n'existe).</summary>
public partial class SellableEquipmentCandidate : ObservableObject
{
    /// <summary>Clé de la ligne de réserve vendue (null pour un objet porté) - une vente confirmée reste
    /// dans PendingSales et BuildReserve la retranche de CETTE ligne (stade AfterSales) ; l'annuler revient
    /// simplement à la retirer de PendingSales. Une clé plutôt que la ligne elle-même : la réserve est
    /// reconstruite à chaque lecture (voir EndOfGamePageViewModel.Reserve.cs).</summary>
    public ReserveLineKey? ReserveKey { get; }

    public WarriorEquipment? CarriedItem { get; }
    public string? CarrierName { get; }

    public EquipmentItem Item { get; }
    public SpecialRule? MaterialRule { get; }

    /// <summary>Combien de cette ligne sont disponibles à la vente dans CE picker (stock réel moins ce qui
    /// est déjà dans PendingSales pour cette même source - voir EndOfGamePageViewModel.EquipmentTrading.
    /// cs's BuildSellableCandidates, reconstruite à chaque ouverture du picker) - plafond du stepper
    /// SelectedQuantity, jamais dépassable.</summary>
    public int OwnedQuantity { get; }

    /// <summary>1 pour tout sauf l'équipement PORTÉ par un groupe d'Hommes de main (effectif > 1) - dans ce
    /// cas, WarriorEquipment.Quantity (OwnedQuantity ici) est une quantité PAR MODÈLE (ex. "Hache" = 1 par
    /// membre, voir WarriorEquipment.NameDisplay/GetTopUpBreakdown pour le même principe), donc CHAQUE
    /// exemplaire "par modèle" retiré du groupe correspond en réalité à Effectif objets physiques quittant
    /// la bande à la fois - retour utilisateur 2026-09-22 - "la vente ne comptabilise qu'une arme et pas
    /// l'arme×effectif". SelectedQuantity reste en unités "par modèle" (borne OwnedQuantity/leftover
    /// inchangés, cohérent avec ApplyEquipmentTradingAsync qui retranche directement de
    /// WarriorEquipment.Quantity) - seul ce multiplicateur convertit en or/quantité RÉELLEMENT
    /// vendue.</summary>
    public int SaleQuantityMultiplier { get; }

    public string NameDisplay { get; }

    /// <summary>Alias - ChipView lie son Label directement sur Name, même idiome que WarbandEquipment/
    /// WarriorEquipment.</summary>
    public string Name => NameDisplay;

    /// <summary>Quantité RÉELLEMENT retirée de la bande (SelectedQuantity × SaleQuantityMultiplier) -
    /// distincte de SelectedQuantity pour un groupe d'Hommes de main, voir SaleQuantityMultiplier's own
    /// doc.</summary>
    public int TotalPhysicalQuantitySold => SelectedQuantity * SaleQuantityMultiplier;

    /// <summary>Nom affiché dans l'Historique de la partie une fois la vente appliquée à Terminer -
    /// suffixé "× N" quand la vente est PARTIELLE (TotalPhysicalQuantitySold &gt; 1), pour ne pas laisser
    /// croire que toute la pile part alors qu'il en reste en réserve/porté.</summary>
    public string SoldLabel => TotalPhysicalQuantitySold > 1 ? $"{NameDisplay} × {TotalPhysicalQuantitySold}" : NameDisplay;

    /// <summary>"Réserve" pour un objet non porté (stash ou trouvaille d'Exploration, les deux affichent le
    /// même libellé), sinon le nom du guerrier qui le porte - utilisé comme nom de section dans
    /// SellEquipmentSelectorViewModel.Groups (retour utilisateur 2026-09-21 - "un affichage à la codex,
    /// avec la séparation par héros, avec la réserve tout en haut"), plus utile à répéter sur chaque tuile
    /// une fois que le groupe le porte déjà (voir UnitSellPriceDisplay, qui a pris sa place sur la
    /// tuile).</summary>
    public string SourceLabel => CarrierName ?? LocalizationService.Instance["EndOfGameEquipmentTradingStashSource"];

    /// <summary>Prix de vente PAR CRAN de stepper (pas SellPrice, qui porte sur SelectedQuantity au complet)
    /// - affiché sur chaque tuile de SellEquipmentSelectorPage pour aider à décider quoi vendre, à la place
    /// du SourceLabel désormais redondant avec l'en-tête de section (Groups). Multiplié par
    /// SaleQuantityMultiplier pour un groupe d'Hommes de main (chaque cran retire Effectif objets, pas 1),
    /// jamais pour tout le reste (multiplicateur toujours 1).</summary>
    public string UnitSellPriceDisplay => $"{EquipmentPricing.CalculateSellPrice(Item.Cost, MaterialRule?.CostMultiplier) * SaleQuantityMultiplier} {LocalizationService.Instance["LibGoldCrownsAbbr"]}";

    /// <summary>Réserve plutôt que porté par un guerrier.</summary>
    public bool IsFromStash => CarriedItem is null;

    /// <summary>Combien de cette ligne on vend réellement - stepper borné [0, OwnedQuantity] sur
    /// SellEquipmentSelectorPage (IncrementCommand/DecrementCommand).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIncrement))]
    [NotifyPropertyChangedFor(nameof(CanDecrement))]
    [NotifyPropertyChangedFor(nameof(SellPrice))]
    [NotifyPropertyChangedFor(nameof(IsSelected))]
    [NotifyPropertyChangedFor(nameof(CountDisplay))]
    [NotifyPropertyChangedFor(nameof(SoldLabel))]
    private int selectedQuantity;

    public bool CanIncrement => SelectedQuantity < OwnedQuantity;
    public bool CanDecrement => SelectedQuantity > 0;

    /// <summary>Une ligne compte comme "sélectionnée" (chip visible, liseré de la tuile) dès qu'au moins un
    /// exemplaire est mis en vente - plus une bool à part (retirée avec la vente partielle), calculée
    /// directement depuis SelectedQuantity.</summary>
    public bool IsSelected => SelectedQuantity > 0;

    /// <summary>"2/5" affiché au centre du stepper de chaque tuile de SellEquipmentSelectorPage.</summary>
    public string CountDisplay => $"{SelectedQuantity}/{OwnedQuantity}";

    /// <summary>Livre des règles - "half its listed price"/"half of the basic cost only" pour un prix
    /// variable : toujours le coût de BASE du catalogue (jamais FoundValueOverride/le jet trouvé), pour
    /// la quantité RÉELLEMENT mise en vente (TotalPhysicalQuantitySold, jamais OwnedQuantity) - voir
    /// Core.Rules.EquipmentPricing.CalculateSellPrice.</summary>
    public int SellPrice => EquipmentPricing.CalculateSellPrice(Item.Cost, MaterialRule?.CostMultiplier) * TotalPhysicalQuantitySold;

    public SellableEquipmentCandidate(ReserveLine reserveLine, int ownedQuantity)
    {
        ReserveKey = reserveLine.Key;
        Item = reserveLine.Item;
        MaterialRule = reserveLine.MaterialRule;
        OwnedQuantity = ownedQuantity;
        SaleQuantityMultiplier = 1;
        NameDisplay = reserveLine.NameDisplay;
    }

    public SellableEquipmentCandidate(WarriorEquipment carriedItem, string carrierName, int ownedQuantity, int saleQuantityMultiplier = 1)
    {
        CarriedItem = carriedItem;
        CarrierName = carrierName;
        Item = carriedItem.Item;
        MaterialRule = carriedItem.MaterialRule;
        OwnedQuantity = ownedQuantity;
        SaleQuantityMultiplier = saleQuantityMultiplier;
        // Jamais carriedItem.NameDisplay tel quel : celui-ci suffixe déjà " x{Quantity}" pour le stock
        // TOTAL porté (voir WarriorEquipment.NameDisplay) - une vente partielle a sa propre quantité
        // (SelectedQuantity, voir SoldLabel/CountDisplay), le nom de base ne doit porter que
        // matériau+bénédiction.
        var abbrs = new[] { carriedItem.MaterialRule?.Abbreviation, carriedItem.BlessingRule?.Abbreviation }
            .Where(a => !string.IsNullOrEmpty(a)).ToList();
        NameDisplay = abbrs.Count > 0 ? $"{carriedItem.Item.Name} ({string.Join(", ", abbrs)})" : carriedItem.Item.Name;
    }
}
