using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame.Effects;

/// <summary>Un achat de l'après-bataille sur lequel une réduction peut porter (maquette « Achat avec
/// Marchandage ») : identifiant stable dans le wizard, étape, objet et prix actuel.</summary>
public sealed record PurchaseSpot(string Id, string StepName, string ItemName, int Price);

/// <summary>Une réduction « une fois par après-bataille » d'un Héros (Marchandage) : le jet de dés lui
/// appartient, pas à l'achat - la déplacer vers un autre achat garde le même jet. Où elle est utilisée est tenu
/// par OncePerPostBattleLedger (Core).</summary>
public partial class HaggleUsage : ObservableObject
{
    public ActiveEffect Effect { get; }
    public string Formula => Effect.Effect.Value ?? string.Empty;
    public int? MinimumPrice => Effect.Effect.MinimumPrice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RollValue))]
    private string roll = string.Empty;

    /// <summary>Le jet saisi, s'il tient dans la plage de la formule (EffectValues.DiceRange).</summary>
    public int? RollValue => int.TryParse(Roll, out var value)
        && (EffectValues.DiceRange(Formula) is not { } range || (value >= range.Min && value <= range.Max))
            ? value
            : null;

    public HaggleUsage(ActiveEffect effect) => Effect = effect;

    /// <summary>Prix après réduction pour ce prix d'origine (Core : jamais sous le plancher).</summary>
    public int DiscountedPrice(int price) =>
        RollValue is { } value ? EffectValues.ApplyDiscount(price, value, MinimumPrice) : price;
}
