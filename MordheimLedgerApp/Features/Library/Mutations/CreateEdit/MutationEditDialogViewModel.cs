using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Mutations.CreateEdit;

public partial class MutationEditDialogViewModel : DialogViewModel<bool>
{
    private readonly IWarbandArchetypePickerService _warbandPicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Mutation item;

    [ObservableProperty]
    private string title;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même principe qu'EquipmentItemEditDialogViewModel.RestrictedWarbands. Vide = commun à toutes les
    /// bandes qui peuvent acheter des mutations (voir Mutation.RestrictedToWarbandArchetypeIds).</summary>
    public ObservableCollection<WarbandArchetype> RestrictedWarbands { get; }

    /// <summary>Un seul texte plutôt qu'un titre fixe + un indice séparé affichés en même temps liste
    /// vide : "Réservé à ces bandes" tant qu'il y a des restrictions, remplacé par "Commun à toutes les
    /// bandes" dès que la liste est vidée - jamais les deux empilés.</summary>
    public string RestrictedWarbandsHeaderText =>
        RestrictedWarbands.Count > 0 ? Loc["LibRestrictedToWarbandsPh"] : Loc["LibRestrictedToAllHint"];

    public MutationEditDialogViewModel(Mutation item, string title, IWarbandArchetypePickerService warbandPicker,
        IReadOnlyList<WarbandArchetype> allWarbandArchetypes)
    {
        this.item = item;
        this.title = title;
        _warbandPicker = warbandPicker;

        RestrictedWarbands = new ObservableCollection<WarbandArchetype>(
            allWarbandArchetypes.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)));
    }

    [RelayCommand]
    private async Task AddRestriction()
    {
        var picked = await _warbandPicker.PickWarbandArchetypesAsync();
        foreach (var warband in picked)
        {
            if (RestrictedWarbands.Any(w => w.Id == warband.Id)) continue;
            RestrictedWarbands.Add(warband);
        }
        OnPropertyChanged(nameof(RestrictedWarbandsHeaderText));
    }

    [RelayCommand]
    private void RemoveRestriction(WarbandArchetype warband)
    {
        RestrictedWarbands.Remove(warband);
        OnPropertyChanged(nameof(RestrictedWarbandsHeaderText));
    }

    private bool ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(Item.Name))
        {
            NameError = Loc["LibFieldRequired"];
            return false;
        }
        NameError = null;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!ValidateRequiredFields()) return;

        Item.RestrictedToWarbandArchetypeIds = RestrictedWarbands.Select(w => w.Id).ToList();
        Close(true);
    }
}
