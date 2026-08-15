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
/// taper le résultat d'un dé physique, ou le bouton dé pour le remplir au hasard) dans un seul dialog.
/// Deux étapes : Roll résout le jet en sort (ResolvedSpell, affiché en puce tapotable - récap complet à
/// la demande, pas automatiquement) ; Accept ferme le dialog avec ce résultat. RemoveCommand de la puce
/// (Reroll) permet de recommencer sans fermer le dialog si le joueur se ravise.</summary>
public partial class SpellRollDialogViewModel : DialogViewModel<Spell?>
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;

    protected override Spell? CancelResult => null;

    public List<MagicSchool> MagicSchools { get; }

    [ObservableProperty]
    private string spellRoll = string.Empty;

    [ObservableProperty]
    private string? rollError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResolvedSpell))]
    private Spell? resolvedSpell;

    public bool HasResolvedSpell => ResolvedSpell is not null;

    public SpellRollDialogViewModel(List<MagicSchool> magicSchools, ILibraryService libraryService, IDetailDialogService detailDialogs)
    {
        MagicSchools = magicSchools;
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
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

    /// <summary>Récap complet à la demande (tap sur la puce), pas automatique à la résolution du jet -
    /// le joueur voit déjà le nom via la puce elle-même.</summary>
    [RelayCommand]
    private Task ShowResolvedSpellDetail() =>
        ResolvedSpell is null ? Task.CompletedTask : _detailDialogs.ShowSpellDetailDialogAsync(ResolvedSpell);

    /// <summary>Branché sur ChipItemView.RemoveCommand (le "x" de la puce) - revient à l'étape de saisie
    /// sans fermer le dialog, pour relancer.</summary>
    [RelayCommand]
    private void Reroll()
    {
        ResolvedSpell = null;
        SpellRoll = string.Empty;
        RollError = null;
    }

    [RelayCommand]
    private async Task Roll()
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
        ResolvedSpell = spell;
    }

    [RelayCommand]
    private void Accept() => Close(ResolvedSpell);
}
