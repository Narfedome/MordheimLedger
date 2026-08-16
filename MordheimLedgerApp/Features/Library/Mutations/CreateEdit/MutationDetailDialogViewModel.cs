using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

/// <summary>Read-only recap of MutationEditDialog.</summary>
public partial class MutationDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public Mutation Item { get; }

    /// <summary>Already resolved by the caller (MutationViewModel.ShowDetails) from the ids on Item -
    /// same idiom as MutationViewModel.GroupNameFor, no service call needed here. Shown as-is - no
    /// complement/collapse here, see EquipmentItemDetailDialogViewModel.RestrictedWarbands.</summary>
    public List<WarbandArchetype> RestrictedWarbands { get; }

    /// <summary>Un seul texte plutôt qu'un titre fixe + un indice séparé affichés en même temps liste
    /// vide : "Réservé à ces bandes" tant qu'il y a des restrictions, remplacé par "Commun à toutes les
    /// bandes" dès que la liste est vidée - jamais les deux empilés.</summary>
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
