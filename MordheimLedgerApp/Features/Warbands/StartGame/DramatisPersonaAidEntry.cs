using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>One card of the "Aide conditionnelle" section - one per recruited Dramatis Persona whose
/// catalogue entry has RequiresRatingDisadvantage (e.g. Bertha) - "A request... must be made for each
/// battle", checked HERE (at game launch) rather than the End of Game wizard, because the NEXT
/// opponent's Rating is only knowable once a game is about to start (see DramatisPersona.
/// RequiresRatingDisadvantage's own doc for why the End of Game wizard can't do this). Purely
/// informational - same "nothing is actually blocked" philosophy as the rest of StartGameDialog: if she
/// doesn't come, the player simply doesn't field her this battle, no roster/status change either way.</summary>
public partial class DramatisPersonaAidEntry : ObservableObject
{
    public string WarriorName { get; }
    public string PersonaName { get; }
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
    private string roll = string.Empty;

    public int? RollValue => int.TryParse(Roll, out var r) ? r : null;

    public bool HasResult => RequiredRoll is not null && RollValue is not null;

    public bool IsSuccess => HasResult && RollValue >= RequiredRoll;

    public bool IsFailure => HasResult && !IsSuccess;

    public DramatisPersonaAidEntry(string warriorName, string personaName, int ownRating)
    {
        WarriorName = warriorName;
        PersonaName = personaName;
        _ownRating = ownRating;
    }
}
