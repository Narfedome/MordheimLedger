using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mounts.CreateEdit;

/// <summary>Read-only recap of MountEditDialog.</summary>
public partial class MountDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Mount Item { get; }
    public string RarityDisplay { get; }

    /// <summary>Already resolved by the caller (MountViewModel.ShowDetails) from the ids on Item - same
    /// idiom as MountViewModel.GroupNameFor. SpecialRules needs no such resolution - Mount.SpecialRules
    /// is already a List&lt;SpecialRule&gt;.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }

    public bool HasRestrictedWarbands => RestrictedWarbands.Count > 0;

    public MountDetailDialogViewModel(Mount item, List<WarbandArchetype> restrictedWarbands)
    {
        Item = item;
        Title = item.Name;
        RarityDisplay = item.Rarity?.ToString() ?? Loc["LibFilterCommon"];
        RestrictedWarbands = restrictedWarbands;
    }

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);
}
