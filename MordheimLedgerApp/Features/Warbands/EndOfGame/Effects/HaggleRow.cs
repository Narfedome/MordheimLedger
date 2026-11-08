using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame.Effects;

/// <summary>Ce dont une ligne Marchandage a besoin du wizard : où la réduction est utilisée, les achats qu'elle
/// peut viser, et les actions qui la déplacent.</summary>
public interface IHaggleHost
{
    PurchaseSpot? SpotWhereUsed(HaggleUsage usage);
    Task UseAsync(HaggleUsage usage, IReadOnlyList<PurchaseSpot> candidates);
    void Release(HaggleUsage usage);
    void RollDiscount(HaggleUsage usage);
}

/// <summary>Une réduction affichée à un endroit du wizard (maquette « Achat avec Marchandage ») : sur une carte
/// d'objet rare (un seul achat visé) ou sous une liste d'achats (Achat, Recrutement). Libre : « Utiliser ici ».
/// Utilisée ici : jet de dés et prix réduit. Utilisée ailleurs : message ambre qui dit où, et ce que ça
/// rendrait de la déplacer - jamais bloquant. Refresh() est appelée par le wizard à chaque changement.</summary>
public partial class HaggleRow : ObservableObject
{
    private readonly IHaggleHost _host;
    private readonly Func<IReadOnlyList<PurchaseSpot>> _candidates;
    private readonly LocalizationService _loc;

    public HaggleUsage Usage { get; }
    public string Title { get; }
    public string? ConditionWarning { get; }

    public HaggleRow(HaggleUsage usage, IHaggleHost host, Func<IReadOnlyList<PurchaseSpot>> candidates, LocalizationService loc)
    {
        Usage = usage;
        _host = host;
        _candidates = candidates;
        _loc = loc;
        Title = string.Format(loc["EffectSourceTitle"], usage.Effect.SourceName, usage.Effect.Warrior.Name);
        ConditionWarning = EffectTexts.UnmetCondition(usage.Effect, loc);
    }

    private PurchaseSpot? UsedSpot => _host.SpotWhereUsed(Usage);

    public bool IsUsedHere => UsedSpot is { } spot && _candidates().Any(c => c.Id == spot.Id);
    public bool IsUsedElsewhere => UsedSpot is not null && !IsUsedHere;
    public bool CanUseHere => !IsUsedHere && _candidates().Count > 0;

    public string Subtitle
    {
        get
        {
            if (UsedSpot is not { } spot) return string.Format(_loc["HaggleFreeSubtitle"], Usage.Formula, Usage.MinimumPrice ?? 0);
            if (!IsUsedHere) return _loc["HaggleUsedElsewhereSubtitle"];
            return Usage.RollValue is null
                ? string.Format(_loc["HaggleRollPending"], spot.ItemName, spot.Price)
                : string.Format(_loc["HaggleAppliedFormat"], spot.ItemName, spot.Price, Usage.DiscountedPrice(spot.Price));
        }
    }

    public string? Warning
    {
        get
        {
            var parts = new List<string>();
            if (ConditionWarning is not null) parts.Add(ConditionWarning);
            if (IsUsedElsewhere && UsedSpot is { } spot)
                parts.Add(string.Format(_loc["HaggleUsedElsewhereWarning"], spot.StepName, spot.ItemName, spot.Price));
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }
    }

    public bool HasWarning => Warning is not null;

    public string RollPlaceholder => Usage.Formula;

    public void Refresh()
    {
        OnPropertyChanged(nameof(IsUsedHere));
        OnPropertyChanged(nameof(IsUsedElsewhere));
        OnPropertyChanged(nameof(CanUseHere));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(HasWarning));
    }

    [RelayCommand]
    private Task Use() => _host.UseAsync(Usage, _candidates());

    [RelayCommand]
    private void Release() => _host.Release(Usage);

    [RelayCommand]
    private void RollDice() => _host.RollDiscount(Usage);
}
