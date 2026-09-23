using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>D'où vient une ligne de réserve - jamais utilisé pour le calcul de pool (deux lignes du
/// même (Item, Matériau, FoundValueOverride) se fusionnent en une seule quelle que soit leur origine,
/// voir ReserveCollection.Add), seulement pour les récaps/phrases d'Historique qui doivent distinguer
/// "acheté" de "récupéré au renvoi" etc.</summary>
public enum ReserveLineOrigin { PreExisting, Dismissal, Purchase, Exploration }

/// <summary>Une ligne de la réserve d'équipement de la bande pendant le wizard Fin de Partie (2026-09-23,
/// unification de la réserve - voir ReserveCollection's own doc) - état de session pur, jamais mappé
/// vers/depuis la DB directement (même famille qu'EquipmentPick/ReallocatableItem, déjà dans ce dossier
/// plutôt que Core.Models). SourceId porte l'id RÉEL du WarbandEquipment d'origine pour une ligne
/// PreExisting (déjà en base à l'ouverture du wizard) - null pour une ligne apparue PENDANT cette même
/// Fin de Partie (Renvoyer/Achat), qui n'existera en base qu'une fois le pipeline de Terminer passé sur
/// l'étape correspondante (ApplyDismissalsAsync/ApplyEquipmentTradingAsync) - voir
/// EndOfGamePageViewModel.Apply.cs pour comment ce cas est reconcilié à Terminer.</summary>
public sealed class ReserveLine
{
    public EquipmentItem Item { get; }
    public SpecialRule? MaterialRule { get; }
    public int Quantity { get; set; }
    public int? FoundValueOverride { get; }
    public ReserveLineOrigin Origin { get; }
    public int? SourceId { get; }

    public ReserveLine(EquipmentItem item, SpecialRule? materialRule, int quantity, ReserveLineOrigin origin, int? sourceId = null, int? foundValueOverride = null)
    {
        Item = item;
        MaterialRule = materialRule;
        Quantity = quantity;
        Origin = origin;
        SourceId = sourceId;
        FoundValueOverride = foundValueOverride;
    }

    /// <summary>Même idiome que WarbandEquipment.NameDisplay - suffixe le matériau entre parenthèses
    /// quand il y en a un.</summary>
    public string NameDisplay => MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $"{Item.Name} ({abbr})" : Item.Name;

    /// <summary>Alias - ChipView lie son Label directement sur Name, même idiome que WarbandEquipment/
    /// WarriorEquipment.</summary>
    public string Name => NameDisplay;

    /// <summary>Même formule que WarbandEquipment.SellValue.</summary>
    public int SellValue => (FoundValueOverride ?? Item.Cost * (MaterialRule?.CostMultiplier ?? 1)) * Quantity;
}
