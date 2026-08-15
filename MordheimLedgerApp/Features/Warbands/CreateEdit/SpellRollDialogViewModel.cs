using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Sort de départ d'un lanceur de sorts fraîchement recruté (hors mode Bande existante) - livre
/// des règles : "A Wizard starts with one spell, determined randomly - roll 1D6 on the appropriate
/// list". Regroupe le contexte (école(s) de magie de la bande, puce tapotable comme partout ailleurs
/// dans l'appli - ShowMagicSchoolDetail) et la saisie du jet (comme AdvanceRollEntry côté Fin de partie :
/// taper le résultat d'un dé physique, ou le bouton dé pour le remplir au hasard) dans un seul dialog
/// plutôt qu'une ligne inline dans RecruitSlotTabsView. Close(spell) une fois le jet validé ; l'appelant
/// (WarbandEditDialogViewModel.ShowSpellRollDialog) ajoute le sort et enchaîne sur son récap complet
/// (IDetailDialogService.ShowSpellDetailDialogAsync), pour que le joueur voie immédiatement ce qu'il a
/// obtenu plutôt qu'une simple puce muette.</summary>
public partial class SpellRollDialogViewModel : DialogViewModel<Spell?>
{
    private readonly ILibraryService _libraryService;

    protected override Spell? CancelResult => null;

    public List<MagicSchool> MagicSchools { get; }

    [ObservableProperty]
    private string spellRoll = string.Empty;

    [ObservableProperty]
    private string? rollError;

    public SpellRollDialogViewModel(List<MagicSchool> magicSchools, ILibraryService libraryService)
    {
        MagicSchools = magicSchools;
        _libraryService = libraryService;
    }

    [RelayCommand]
    private void AutoRoll() => SpellRoll = SpellRules.RollDice().ToString();

    [RelayCommand]
    private async Task ShowMagicSchoolDetail(MagicSchool school)
    {
        var language = LocalizationService.Instance.Language;
        var spells = (await _libraryService.GetSpellsAsync(language)).Where(s => s.MagicSchoolId == school.Id).ToList();
        await ShowChipDetailAsync(school.Name, school.Description, spells);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (!int.TryParse(SpellRoll, out var roll) || roll < 1 || roll > 6)
        {
            RollError = Loc["WarbandsSpellRollInvalidMessage"];
            return;
        }

        var magicSchoolIds = MagicSchools.Select(s => s.Id).ToHashSet();
        var available = (await _libraryService.GetSpellsAsync(LocalizationService.Instance.Language))
            .Where(s => magicSchoolIds.Contains(s.MagicSchoolId)).ToList();
        var spell = available.FirstOrDefault(s => s.RollValue == roll);
        if (spell is null)
        {
            RollError = Loc["WarbandsSpellRollEmptyMessage"];
            return;
        }

        RollError = null;
        Close(spell);
    }
}
