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
/// Résolution automatique (2026-08-24, revu sur demande explicite - "pas la peine d'appuyer sur le
/// bouton pour voir quel sort on récupère") : dès que SpellRoll est un 1D6 valide, OnSpellRollChanged
/// résout directement ResolvedSpell, affiché en puce tapotable (récap complet à la demande, pas
/// automatiquement) - plus de bouton "Valider" intermédiaire, même idiome que AdvanceRollEntry.
/// ManualRoll qui se résout tout seul. Accept ferme le dialog avec ce résultat. RemoveCommand de la
/// puce (Reroll) permet de recommencer sans fermer le dialog si le joueur se ravise.</summary>
public partial class SpellRollDialogViewModel : DialogViewModel<Spell?>
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;

    /// <summary>Sorts déjà connus de ce guerrier (Id) - un jet retombant dessus est refusé (voir
    /// ResolveSpellAsync), demande explicite du 2026-08-24 : un lanceur de sorts ne doit jamais pouvoir
    /// "gagner" deux fois le même sort. Vide pour une toute nouvelle recrue (rien connu encore).</summary>
    private readonly IReadOnlyCollection<int> _knownSpellIds;

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

    public SpellRollDialogViewModel(List<MagicSchool> magicSchools, ILibraryService libraryService, IDetailDialogService detailDialogs,
        IReadOnlyCollection<int> knownSpellIds)
    {
        MagicSchools = magicSchools;
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _knownSpellIds = knownSpellIds;
    }

    [RelayCommand]
    private void AutoRoll() => SpellRoll = SpellRules.RollDice().ToString();

    // Saisie physique (comme AutoRoll ci-dessus, qui déclenche ce même handler en modifiant SpellRoll) :
    // résout dès qu'un 1D6 valide est présent, aucun bouton "Valider" à cliquer en plus. Un jet
    // momentanément invalide en cours de frappe (vide, hors 1-6) est ignoré silencieusement plutôt que
    // de montrer une erreur - RollError reste réservé au cas "cette école n'a rien sur cette valeur".
    partial void OnSpellRollChanged(string value)
    {
        RollError = null;
        if (!int.TryParse(value, out var roll) || roll < 1 || roll > 6) return;
        _ = ResolveSpellAsync(roll);
    }

    private async Task ResolveSpellAsync(int roll)
    {
        var magicSchoolIds = MagicSchools.Select(s => s.Id).ToHashSet();
        var available = (await _libraryService.GetSpellsAsync(LocalizationService.Instance.Language))
            .Where(s => magicSchoolIds.Contains(s.MagicSchoolId)).ToList();
        var spell = available.FirstOrDefault(s => s.RollValue == roll);
        if (spell is null)
        {
            RollError = Loc["WarbandsSpellRollEmptyMessage"];
            return;
        }

        if (_knownSpellIds.Contains(spell.Id))
        {
            RollError = Loc["WarbandsSpellRollAlreadyKnownMessage"];
            return;
        }

        RollError = null;
        ResolvedSpell = spell;
    }

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
    private void Accept() => Close(ResolvedSpell);
}
