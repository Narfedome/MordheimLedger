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
/// la bande (voir EndOfGameDialogViewModel.Recruitment.cs's BuildStashPool, qui la traite déjà comme telle
/// pour le Recrutement) donc doit être vendable au même titre que le reste de la réserve, cohérent avec le
/// livre des règles ("trade in weapons and equipment... swapped around the warband").
///
/// **Vente partielle** (retour utilisateur 2026-09-21, revu par rapport à une première version "ligne
/// entière uniquement" - "une sélection avec un affichage en tuile comme dans le codex, avec - et + comme
/// sur les équipements, mais à la différence qu'on ne peut pas ajouter des équipements inexistants, juste
/// réduire ou non") : SelectedQuantity (stepper sur SellEquipmentSelectorPage) va de 0 à OwnedQuantity,
/// jamais au-delà - contrairement au stepper d'achat standard (EquipmentItemView.xaml), qui n'a pas de
/// plafond réel côté catalogue. Vendre moins que OwnedQuantity laisse le reste en place (réserve ou
/// porté) ; voir WarbandDetailViewModel.EndOfGame.ApplyEquipmentTradingAsync pour la réduction de quantité
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
    /// pipeline - voir WarbandDetailViewModel.EndOfGame.SaveAsync). Compte comme réserve (IsFromStash) pour
    /// BuildStashPool/l'affichage, mais sa suppression à Terminer doit re-résoudre le vrai id fraîchement
    /// créé plutôt que réutiliser un id connu à la construction de ce candidat - voir
    /// ApplyEquipmentTradingAsync.</summary>
    public bool IsFromExploration { get; }

    public EquipmentItem Item { get; }
    public SpecialRule? MaterialRule { get; }

    /// <summary>Combien de cette ligne sont disponibles à la vente dans CE picker (stock réel moins ce qui
    /// est déjà dans PendingSales pour cette même source - voir EndOfGameDialogViewModel.EquipmentTrading.
    /// cs's BuildSellableCandidates, reconstruite à chaque ouverture du picker) - plafond du stepper
    /// SelectedQuantity, jamais dépassable.</summary>
    public int OwnedQuantity { get; }

    public string NameDisplay { get; }

    /// <summary>Alias - ChipView lie son Label directement sur Name, même idiome que WarbandEquipment/
    /// WarriorEquipment.</summary>
    public string Name => NameDisplay;

    /// <summary>Nom affiché dans l'Historique de la partie une fois la vente appliquée à Terminer -
    /// suffixé "× N" quand la vente est PARTIELLE (SelectedQuantity &gt; 1), pour ne pas laisser croire que
    /// toute la pile part alors qu'il en reste en réserve/porté.</summary>
    public string SoldLabel => SelectedQuantity > 1 ? $"{NameDisplay} × {SelectedQuantity}" : NameDisplay;

    /// <summary>"Réserve" pour un objet non porté (stash ou trouvaille d'Exploration, les deux affichent le
    /// même libellé), sinon le nom du guerrier qui le porte - utilisé comme nom de section dans
    /// SellEquipmentSelectorViewModel.Groups (retour utilisateur 2026-09-21 - "un affichage à la codex,
    /// avec la séparation par héros, avec la réserve tout en haut"), plus utile à répéter sur chaque tuile
    /// une fois que le groupe le porte déjà (voir UnitSellPriceDisplay, qui a pris sa place sur la
    /// tuile).</summary>
    public string SourceLabel => CarrierName ?? LocalizationService.Instance["EndOfGameEquipmentTradingStashSource"];

    /// <summary>Prix de vente À L'UNITÉ (pas SellPrice, qui porte sur SelectedQuantity) - affiché sur
    /// chaque tuile de SellEquipmentSelectorPage pour aider à décider quoi vendre, à la place du
    /// SourceLabel désormais redondant avec l'en-tête de section (Groups).</summary>
    public string UnitSellPriceDisplay => $"{EquipmentPricing.CalculateSellPrice(Item.Cost, MaterialRule?.CostMultiplier)} {LocalizationService.Instance["LibGoldCrownsAbbr"]}";

    /// <summary>Réserve (stash pré-partie OU trouvaille d'Exploration cette partie, les deux rejoignent le
    /// même pool - voir BuildStashPool) plutôt que porté par un guerrier.</summary>
    public bool IsFromStash => CarriedItem is null;

    /// <summary>Identité stable de la ligne source (IsFromStash + SourceId forme une clé unique, un
    /// WarbandEquipment.Id et un WarriorEquipment.Id vivent dans deux tables différentes donc peuvent
    /// coïncider numériquement sans IsFromStash pour les distinguer) - synthétisé négatif (jamais un vrai
    /// id positif) pour une trouvaille d'Exploration, qui n'en a pas encore un réel.</summary>
    public int SourceId => StashItem?.Id ?? CarriedItem?.Id ?? SyntheticExplorationSourceId(Item.Id, MaterialRule?.Id);

    public static int SyntheticExplorationSourceId(int itemId, int? materialRuleId) => -(itemId * 1_000_003 + (materialRuleId ?? 0) + 1);

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
    /// la quantité réellement mise en vente (SelectedQuantity, jamais OwnedQuantity) - voir
    /// Core.Rules.EquipmentPricing.CalculateSellPrice.</summary>
    public int SellPrice => EquipmentPricing.CalculateSellPrice(Item.Cost, MaterialRule?.CostMultiplier) * SelectedQuantity;

    public SellableEquipmentCandidate(WarbandEquipment stashItem, int ownedQuantity)
    {
        StashItem = stashItem;
        Item = stashItem.Item;
        MaterialRule = stashItem.MaterialRule;
        OwnedQuantity = ownedQuantity;
        NameDisplay = stashItem.NameDisplay;
    }

    public SellableEquipmentCandidate(WarriorEquipment carriedItem, string carrierName, int ownedQuantity)
    {
        CarriedItem = carriedItem;
        CarrierName = carrierName;
        Item = carriedItem.Item;
        MaterialRule = carriedItem.MaterialRule;
        OwnedQuantity = ownedQuantity;
        // Jamais carriedItem.NameDisplay tel quel : celui-ci suffixe déjà " x{Quantity}" pour le stock
        // TOTAL porté (voir WarriorEquipment.NameDisplay) - une vente partielle a sa propre quantité
        // (SelectedQuantity, voir SoldLabel/CountDisplay), le nom de base ne doit porter que
        // matériau+bénédiction.
        var abbrs = new[] { carriedItem.MaterialRule?.Abbreviation, carriedItem.BlessingRule?.Abbreviation }
            .Where(a => !string.IsNullOrEmpty(a)).ToList();
        NameDisplay = abbrs.Count > 0 ? $"{carriedItem.Item.Name} ({string.Join(", ", abbrs)})" : carriedItem.Item.Name;
    }

    public SellableEquipmentCandidate(EquipmentItem explorationItem, SpecialRule? materialRule, int ownedQuantity)
    {
        IsFromExploration = true;
        Item = explorationItem;
        MaterialRule = materialRule;
        OwnedQuantity = ownedQuantity;
        NameDisplay = materialRule?.Abbreviation is { Length: > 0 } abbr ? $"{explorationItem.Name} ({abbr})" : explorationItem.Name;
    }
}
