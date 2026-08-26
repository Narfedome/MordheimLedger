using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

/// <summary>Read-only recap of InjuryEditDialog.</summary>
public partial class InjuryDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Injury Item { get; }
    public string CategoryLabel { get; }

    private readonly IDetailDialogService _detailDialogs;

    public InjuryDetailDialogViewModel(Injury item, IDetailDialogService detailDialogs)
    {
        Item = item;
        Title = item.Name;
        CategoryLabel = Loc[$"InjuryCategory{item.Category}"];
        _detailDialogs = detailDialogs;
    }

    /// <summary>A permanent SpecialRule this Injury grants (e.g. Folie -> Stupidité/Frénésie, Bras
    /// amputé -> Armes à une main uniquement) is surfaced ONLY here, as a nested chip - not merged into
    /// the warrior card's top-level "Règles spéciales" list (see WarbandDetailViewModel.ToRow, reverted
    /// 2026-08-26 on explicit user request to avoid a duplicate chip: one via the Injury chip itself,
    /// one via a redundant top-level rule chip).</summary>
    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) =>
        rule.CostMultiplier.HasValue || rule.Rarity.HasValue
            ? _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule)
            : ShowChipDetailAsync(rule.Name, rule.Description);
}
