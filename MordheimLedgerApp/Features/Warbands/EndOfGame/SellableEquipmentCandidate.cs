using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Une ligne vendable à l'étape Achat/Vente - un objet de la réserve (WarbandEquipment, déjà là
/// avant cette partie OU trouvé via Exploration PENDANT cette même partie - voir IsFromExploration), ou
/// un objet porté par un guerrier ACTIF (WarriorEquipment, déjà porté avant cette partie). Jamais un
/// objet ACHETÉ pendant cette même Fin de Partie (Achat de cette étape) : annuler un achat de cette
/// session retire simplement le pick (RemoveReserveEquipment/RemoveRecruitEquipment), ce n'est jamais une
/// "vente" à proprement parler (retour utilisateur 2026-09-05 - "si on fait un achat et que finalement on
/// change d'avis, on considère que l'objet n'est pas acheté" - pas de demi-tarif pour ça, juste rien
/// facturé du tout). Un objet TROUVÉ via Exploration cette même partie, en revanche, EST vendable : retour
/// utilisateur 2026-09-21 - "j'avais fait un jet d'exploration qui m'a permis de trouver une épée et une
/// dague ornée. dans la vente, les 2 objets ne sont pas apparents" - une trouvaille rejoint la réserve de
/// la bande (voir EndOfGamePageViewModel.Recruitment.cs's BuildStashPool, qui la traite déjà comme telle
/// pour le Recrutement) donc doit être vendable au même titre que le reste de la réserve, cohérent avec le
/// livre des règles ("trade in weapons and equipment... swapped around the warband").
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
    public WarbandEquipment? StashItem { get; }
    public WarriorEquipment? CarriedItem { get; }
    public string? CarrierName { get; }

    /// <summary>True pour un objet trouvé via Exploration cette même partie (PendingExplorationStashItems) -
    /// il n'existe pas encore de WarbandEquipment réel au moment de construire ce candidat,
    /// ApplyExplorationOutcomeAsync ne le crée qu'à Terminer (avant ApplyEquipmentTradingAsync dans le
    /// pipeline - voir EndOfGamePageViewModel.Apply.SaveAsync). Compte comme réserve (IsFromStash) pour
    /// BuildStashPool/l'affichage, mais sa suppression à Terminer doit re-résoudre le vrai id fraîchement
    /// créé plutôt que réutiliser un id connu à la construction de ce candidat - voir
    /// ApplyEquipmentTradingAsync.</summary>
    public bool IsFromExploration { get; }

    /// <summary>True pour de l'équipement restitué à la réserve par l'étape "Renvoyer", juste avant
    /// Achat/Vente (PendingDismissedEquipment) - même situation qu'IsFromExploration (pas encore de
    /// WarbandEquipment réel au moment de construire ce candidat, ApplyDismissalsAsync ne le crée qu'à
    /// Terminer, AVANT ApplyEquipmentTradingAsync dans le pipeline) : traité par le même mécanisme de
    /// "re-requête fraîche + retrait/recréation du reliquat" dans ApplyEquipmentTradingAsync. Séparé
    /// d'IsFromExploration (pas juste réutilisé) pour ne pas mélanger deux provenances distinctes dans
    /// l'Historique/le débogage, même si la mécanique de persistance est identique.</summary>
    public bool IsFromDismissal { get; }

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

    /// <summary>Réserve (stash pré-partie OU trouvaille d'Exploration cette partie, les deux rejoignent le
    /// même pool - voir BuildStashPool) plutôt que porté par un guerrier.</summary>
    public bool IsFromStash => CarriedItem is null;

    /// <summary>Identité stable de la ligne source (IsFromStash + SourceId forme une clé unique, un
    /// WarbandEquipment.Id et un WarriorEquipment.Id vivent dans deux tables différentes donc peuvent
    /// coïncider numériquement sans IsFromStash pour les distinguer) - synthétisé négatif (jamais un vrai
    /// id positif) pour une trouvaille d'Exploration ou un renvoi, qui n'en ont pas encore un réel. Les
    /// deux formules synthétiques restent distinctes (offset +1 vs +2) pour qu'une trouvaille et un renvoi
    /// portant sur le même (Item, MaterialRule) cette même partie ne se fassent jamais passer pour la même
    /// source.</summary>
    public int SourceId => StashItem?.Id ?? CarriedItem?.Id
        ?? (IsFromDismissal ? SyntheticDismissalSourceId(Item.Id, MaterialRule?.Id) : SyntheticExplorationSourceId(Item.Id, MaterialRule?.Id));

    public static int SyntheticExplorationSourceId(int itemId, int? materialRuleId) => -(itemId * 1_000_003 + (materialRuleId ?? 0) + 1);

    public static int SyntheticDismissalSourceId(int itemId, int? materialRuleId) => -(itemId * 1_000_003 + (materialRuleId ?? 0) + 2);

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

    public SellableEquipmentCandidate(WarbandEquipment stashItem, int ownedQuantity)
    {
        StashItem = stashItem;
        Item = stashItem.Item;
        MaterialRule = stashItem.MaterialRule;
        OwnedQuantity = ownedQuantity;
        SaleQuantityMultiplier = 1;
        NameDisplay = stashItem.NameDisplay;
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

    public SellableEquipmentCandidate(EquipmentItem explorationItem, SpecialRule? materialRule, int ownedQuantity)
        : this(explorationItem, materialRule, ownedQuantity, isFromDismissal: false)
    {
    }

    /// <summary>Équipement restitué à la réserve par l'étape "Renvoyer" - factory nommée plutôt qu'un 4e
    /// constructeur positionnel (même liste de paramètres que le constructeur Exploration ci-dessus,
    /// impossible à surcharger sans le distinguer autrement).</summary>
    public static SellableEquipmentCandidate ForDismissal(EquipmentItem item, SpecialRule? materialRule, int ownedQuantity) =>
        new(item, materialRule, ownedQuantity, isFromDismissal: true);

    private SellableEquipmentCandidate(EquipmentItem item, SpecialRule? materialRule, int ownedQuantity, bool isFromDismissal)
    {
        IsFromExploration = !isFromDismissal;
        IsFromDismissal = isFromDismissal;
        Item = item;
        MaterialRule = materialRule;
        OwnedQuantity = ownedQuantity;
        SaleQuantityMultiplier = 1;
        NameDisplay = materialRule?.Abbreviation is { Length: > 0 } abbr ? $"{item.Name} ({abbr})" : item.Name;
    }
}
