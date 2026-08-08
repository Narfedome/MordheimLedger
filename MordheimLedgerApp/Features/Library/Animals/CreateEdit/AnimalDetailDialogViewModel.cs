using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Animals.CreateEdit;

/// <summary>Read-only recap of AnimalEditDialog.</summary>
public partial class AnimalDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Animal Item { get; }
    public string RarityDisplay { get; }

    /// <summary>"25 - 37" when CostRandomMax is set (worst-case total, see Animal.CostRandomMax),
    /// otherwise just "25".</summary>
    public string CostDisplay => Item.CostRandomMax is { } max ? $"{Item.Cost} - {Item.Cost + max}" : Item.Cost.ToString();

    /// <summary>Already resolved by the caller (AnimalViewModel.ShowDetails) from the ids on Item - same
    /// idiom as AnimalViewModel.GroupNameFor. SpecialRules needs no such resolution - Animal.SpecialRules
    /// is already a List&lt;SpecialRule&gt;.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }

    public AnimalDetailDialogViewModel(Animal item, List<WarbandArchetype> restrictedWarbands)
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
