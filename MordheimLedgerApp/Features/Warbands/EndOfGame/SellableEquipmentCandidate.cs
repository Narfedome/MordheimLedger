using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Une ligne vendable à l'étape Achat/Vente - soit un objet de la réserve (WarbandEquipment,
/// déjà là avant cette partie), soit un objet porté par un guerrier ACTIF (WarriorEquipment, déjà porté
/// avant cette partie) - jamais un objet acheté/trouvé PENDANT cette même Fin de Partie (Achat de cette
/// étape, Exploration...) : annuler un achat de cette session retire simplement le pick
/// (RemoveReserveEquipment/RemoveRecruitEquipment), ce n'est jamais une "vente" à proprement parler
/// (retour utilisateur 2026-09-05 - "si on fait un achat et que finalement on change d'avis, on considère
/// que l'objet n'est pas acheté" - pas de demi-tarif pour ça, juste rien facturé du tout). Livre des
/// règles : "Warriors can automatically sell equipment for half its listed price... rare equipment and
/// weapons which have a variable price, the warband receives half of the basic cost only" - ET "trade in
/// weapons and equipment... swapped around the warband" confirme que l'équipement DÉJÀ PORTÉ (pas
/// seulement la réserve) est bien vendable, pas seulement ce qui traîne dans la réserve. Vend la LIGNE
/// ENTIÈRE (toute la Quantity) - pas de vente partielle (simplification délibérée, même limite que
/// SeizedEquipmentItems/PairEngagement, pas de mécanisme de pile partielle ailleurs dans l'app).</summary>
public partial class SellableEquipmentCandidate : ObservableObject
{
    public WarbandEquipment? StashItem { get; }
    public WarriorEquipment? CarriedItem { get; }
    public string? CarrierName { get; }

    public EquipmentItem Item => StashItem?.Item ?? CarriedItem!.Item;
    public SpecialRule? MaterialRule => StashItem?.MaterialRule ?? CarriedItem!.MaterialRule;
    public int Quantity => StashItem?.Quantity ?? CarriedItem!.Quantity;
    public string NameDisplay => StashItem?.NameDisplay ?? CarriedItem!.NameDisplay;

    /// <summary>Alias - ChipView lie son Label directement sur Name, même idiome que WarbandEquipment/
    /// WarriorEquipment.</summary>
    public string Name => NameDisplay;

    /// <summary>"Réserve" pour un objet non porté, sinon le nom du guerrier qui le porte - affiché sous
    /// le nom de l'objet dans le picker pour distinguer les lignes homonymes (ex. deux "Épée", l'une en
    /// réserve, l'autre portée par "Ulrik").</summary>
    public string SourceLabel => CarrierName ?? LocalizationService.Instance["EndOfGameEquipmentTradingStashSource"];

    public bool IsFromStash => StashItem is not null;

    /// <summary>Identité stable de la ligne source (Id du WarbandEquipment ou du WarriorEquipment sous-
    /// jacent) - sert à exclure du picker Vente ce qui est déjà dans PendingSales (IsFromStash + SourceId
    /// forme une clé unique, un WarbandEquipment.Id et un WarriorEquipment.Id vivent dans deux tables
    /// différentes donc peuvent coïncider numériquement sans IsFromStash pour les distinguer).</summary>
    public int SourceId => StashItem?.Id ?? CarriedItem!.Id;

    /// <summary>Livre des règles - "half its listed price"/"half of the basic cost only" pour un prix
    /// variable : toujours le coût de BASE du catalogue (jamais FoundValueOverride/le jet trouvé), pour
    /// TOUTE la ligne (Quantity comprise) - voir Core.Rules.EquipmentPricing.CalculateSellPrice.</summary>
    public int SellPrice => EquipmentPricing.CalculateSellPrice(Item.Cost, MaterialRule?.CostMultiplier) * Quantity;

    /// <summary>Coché/décoché dans SellEquipmentDialog - la ligne ne rejoint EndOfGameDialogViewModel.
    /// PendingSales qu'une fois le dialog validé (Confirm), voir AddSaleEquipment.</summary>
    [ObservableProperty]
    private bool isSelected;

    public SellableEquipmentCandidate(WarbandEquipment stashItem)
    {
        StashItem = stashItem;
    }

    public SellableEquipmentCandidate(WarriorEquipment carriedItem, string carrierName)
    {
        CarriedItem = carriedItem;
        CarrierName = carrierName;
    }
}
