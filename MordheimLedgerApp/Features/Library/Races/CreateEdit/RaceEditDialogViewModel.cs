using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Races.CreateEdit;

/// <summary>Un seul champ obligatoire (Nom), pas d'onglet - contrairement à MagicSchoolEditDialogViewModel
/// (Général/Sorts), une Race ne porte aucune sous-collection à éditer en mémoire.</summary>
public partial class RaceEditDialogViewModel : DialogViewModel<bool>
{
    private readonly ILibraryService _libraryService;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Race item;

    [ObservableProperty]
    private string title;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// MagicSchoolEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    public RaceEditDialogViewModel(Race item, string title, ILibraryService libraryService)
    {
        this.item = item;
        this.title = title;
        _libraryService = libraryService;
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
    private async Task Save()
    {
        if (!ValidateRequiredFields()) return;

        var language = LocalizationService.Instance.Language;
        await Loading.RunAsync(async () => await _libraryService.SaveRaceAsync(Item, language));

        Close(true);
    }
}
