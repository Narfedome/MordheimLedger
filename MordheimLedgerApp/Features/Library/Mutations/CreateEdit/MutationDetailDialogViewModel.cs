using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

/// <summary>Read-only recap of MutationEditDialog.</summary>
public partial class MutationDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Mutation Item { get; }

    /// <summary>Already resolved by the caller (MutationViewModel.ShowDetails) from the ids on Item -
    /// same idiom as MutationViewModel.GroupNameFor, no service call needed here.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }

    /// <summary>Un seul texte plutôt qu'un titre fixe + un indice affichés en même temps liste vide -
    /// même principe que MutationEditDialogViewModel.RestrictedWarbandsHeaderText.</summary>
    public string RestrictedWarbandsHeaderText =>
        RestrictedWarbands.Count > 0 ? Loc["LibRestrictedToWarbandsPh"] : Loc["LibRestrictedToAllHint"];

    public MutationDetailDialogViewModel(Mutation item, List<WarbandArchetype> restrictedWarbands)
    {
        Item = item;
        Title = item.Name;
        RestrictedWarbands = restrictedWarbands;
    }

    [RelayCommand]
    private Task ShowWarbandDetail(WarbandArchetype warband) => ShowChipDetailAsync(warband.Name, warband.Description);
}
