using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.RacialProfiles.CreateEdit;

/// <summary>Un seul onglet - Nom/Description + la grille StatRowView en édition pour les 9 maximums.
/// Mouvement passe par un champ texte proxy (MovementInput, "4" ou "2D6") résolu au Save, même
/// mécanisme que WarriorArchetypeEditDialogViewModel.MovementInput.</summary>
public partial class RacialProfileEditDialogViewModel : DialogViewModel<bool>
{
    private readonly ILibraryService _libraryService;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private RacialProfile item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string movementInput;

    [ObservableProperty]
    private string? nameError;

    public RacialProfileEditDialogViewModel(RacialProfile item, string title, ILibraryService libraryService)
    {
        this.item = item;
        this.title = title;
        _libraryService = libraryService;
        movementInput = item.MovementOverride ?? item.Movement.ToString();
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

        if (int.TryParse(MovementInput, out var movement))
        {
            Item.Movement = movement;
            Item.MovementOverride = null;
        }
        else
        {
            Item.MovementOverride = MovementInput;
        }

        var language = LocalizationService.Instance.Language;
        await Loading.RunAsync(async () => await _libraryService.SaveRacialProfileAsync(Item, language));

        Close(true);
    }
}
