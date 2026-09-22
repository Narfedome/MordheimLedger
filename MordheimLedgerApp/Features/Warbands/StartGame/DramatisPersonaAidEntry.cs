using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>One card of the "Aide conditionnelle" section - one per recruited Dramatis Persona whose
/// catalogue entry has RequiresRatingDisadvantage (e.g. Bertha) - "A request... must be made for each
/// battle", checked HERE (at game launch) rather than the End of Game wizard, because the NEXT
/// opponent's Rating is only knowable once a game is about to start (see DramatisPersona.
/// RequiresRatingDisadvantage's own doc for why the End of Game wizard can't do this).
///
/// NOT purely informational (revised 2026-09-01, user request): once the outcome is known (WontCome, or
/// a failed Roll), confirming "Commencer la partie" removes this Warrior from the roster right away -
/// same "leaves the warband entirely, only a fresh search brings her back" behaviour as the Vagabond
/// departure already applied at End of Game (see WarbandDetailViewModel.EndOfGame.
/// ApplyWandererDeparturesAsync), just triggered earlier because a failed aid check already answers the
/// question ("she isn't with the band for this battle at all") without waiting for the battle to be
/// played out. A successful check leaves her alone here - she still leaves normally at the END of this
/// same game via the existing Vagabond-departure step, since RequiresRatingDisadvantage always implies
/// IsWanderer in practice (Bertha is both).</summary>
public partial class DramatisPersonaAidEntry : ObservableObject
{
    public Warrior Warrior { get; }
    public string WarriorName { get; }
    public string PersonaName { get; }

    /// <summary>False in the common case (Warrior.Name is recopied from persona.Name as-is at recruitment,
    /// see WarbandService.RecruitDramatisPersonaAsync) - showing WarriorName under PersonaName then would
    /// just repeat the same text twice (bug reported 2026-09-01: "Bertha Bestraufrung..." shown as both
    /// the card title and the line right under it). Only true if the player renamed the recruited warrior
    /// afterwards (EditWarrior), in which case both names are worth showing.</summary>
    public bool HasCustomWarriorName => WarriorName != PersonaName;

    private readonly int _ownRating;

    /// <summary>Free-typed - the opponent's warband Rating isn't tracked anywhere in this app (no
    /// networked multiplayer, see the memory note on that idea), so the player types in whatever they
    /// agreed on/measured before the game.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RatingDifference))]
    [NotifyPropertyChangedFor(nameof(HasRatingDifference))]
    [NotifyPropertyChangedFor(nameof(RequiredRoll))]
    [NotifyPropertyChangedFor(nameof(RequiredRollDisplay))]
    [NotifyPropertyChangedFor(nameof(WontCome))]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(IsFailure))]
    [NotifyPropertyChangedFor(nameof(WontFight))]
    private string enemyRating = string.Empty;

    public int? RatingDifference => int.TryParse(EnemyRating, out var enemy) ? enemy - _ownRating : null;

    public bool HasRatingDifference => RatingDifference is not null;

    /// <summary>Null while RatingDifference isn't known yet, OR once known if the gap is under 50 ("will
    /// not come" per RatingGapAidTable) - WontCome (below) distinguishes the two for display.</summary>
    public int? RequiredRoll => RatingDifference is { } d ? RatingGapAidTable.GetRequiredRoll(d) : null;

    /// <summary>Computed here rather than a nested {loc:Loc} inside a StringFormat attribute (not valid
    /// XAML) - LocalizationService.Instance used directly (static singleton), same idiom as
    /// DramatisPersonaRow.FeeDisplay, since this row-helper class isn't constructed with an injected
    /// loc service like RareItemSearchEntry is.</summary>
    public string RequiredRollDisplay => RequiredRoll is { } roll
        ? string.Format(LocalizationService.Instance["StartGameAidRequiredRollFormat"], roll)
        : string.Empty;

    public bool WontCome => HasRatingDifference && RequiredRoll is null;

    /// <summary>Free-typed D6, same "type what you physically rolled" idiom as OldWoundRollEntry.ManualRoll.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(IsFailure))]
    [NotifyPropertyChangedFor(nameof(WontFight))]
    private string roll = string.Empty;

    public int? RollValue => int.TryParse(Roll, out var r) ? r : null;

    public bool HasResult => RequiredRoll is not null && RollValue is not null;

    public bool IsSuccess => HasResult && RollValue >= RequiredRoll;

    public bool IsFailure => HasResult && !IsSuccess;

    /// <summary>True once the outcome definitively says "not with the band this battle" - either the gap
    /// was too small to need her (WontCome) or the willingness roll failed (IsFailure). False while
    /// nothing has been entered yet (HasRatingDifference/HasResult both false) - never removes anyone on
    /// incomplete data.</summary>
    public bool WontFight => WontCome || IsFailure;

    public DramatisPersonaAidEntry(Warrior warrior, string personaName, int ownRating)
    {
        Warrior = warrior;
        WarriorName = warrior.Name;
        PersonaName = personaName;
        _ownRating = ownRating;
    }
}
