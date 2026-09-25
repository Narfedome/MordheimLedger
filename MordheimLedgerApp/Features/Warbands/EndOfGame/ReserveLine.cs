using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>D'où vient une ligne de réserve - fait partie de sa clé (ReserveLineKey) : deux lignes nées
/// pendant le wizard ne se fusionnent que si elles ont la même origine (voir ReserveCollection.Add). Sert
/// aussi à exclure de la Vente ce qui a été acheté pendant cette même Fin de Partie (Purchase/
/// RarePurchase - annuler un achat n'est jamais une vente à moitié prix).</summary>
public enum ReserveLineOrigin { PreExisting, Dismissal, Purchase, Exploration, Scenario, RarePurchase, Reallocation }

/// <summary>Identité stable d'une ligne de réserve, indépendante de l'instance : la réserve est
/// reconstruite à chaque lecture (EndOfGamePageViewModel.BuildReserve), donc une décision qui vise une
/// ligne précise (vente, déplacement vers un Héros) retient sa clé, jamais sa référence. SourceId non
/// null = ligne WarbandEquipment réelle d'avant la partie.</summary>
public sealed record ReserveLineKey(int? SourceId, int ItemId, int? MaterialRuleId, int? FoundValueOverride, ReserveLineOrigin Origin);

/// <summary>Un apport à la réserve pendant la Fin de Partie (Exploration, scénario, objet rare, renvoi,
/// achat, réallocation) - même forme pour toutes les sources, voir EndOfGamePageViewModel.Gains.cs.</summary>
public sealed record ReserveInflow(EquipmentItem Item, SpecialRule? MaterialRule, int Quantity, int? FoundValueOverride = null);

/// <summary>Une ligne de la réserve d'équipement de la bande pendant le wizard Fin de Partie - état de
/// session pur, jamais mappé vers/depuis la DB directement (même famille qu'EquipmentPick/
/// ReallocatableItem). SourceId porte l'id RÉEL du WarbandEquipment d'origine pour une ligne PreExisting,
/// null pour une ligne apparue pendant cette Fin de Partie - créée en base à Terminer seulement
/// (EndOfGamePageViewModel.ApplyReserveAsync).</summary>
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

    public ReserveLineKey Key => new(SourceId, Item.Id, MaterialRule?.Id, FoundValueOverride, Origin);

    /// <summary>Même idiome que WarbandEquipment.NameDisplay - suffixe le matériau entre parenthèses
    /// quand il y en a un.</summary>
    public string NameDisplay => MaterialRule?.Abbreviation is { Length: > 0 } abbr ? $"{Item.Name} ({abbr})" : Item.Name;

    /// <summary>Alias - ChipView lie son Label directement sur Name, même idiome que WarbandEquipment/
    /// WarriorEquipment.</summary>
    public string Name => NameDisplay;

    /// <summary>Même formule que WarbandEquipment.SellValue.</summary>
    public int SellValue => (FoundValueOverride ?? Item.Cost * (MaterialRule?.CostMultiplier ?? 1)) * Quantity;
}
