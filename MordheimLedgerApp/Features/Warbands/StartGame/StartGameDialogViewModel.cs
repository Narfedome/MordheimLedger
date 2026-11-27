using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>Informational pre-battle screen ("wizard informatif", user's own framing 2026-08-26) shown
/// by the "Lancer la partie" action that replaces "Fin de partie" on WarbandDetailPage until this
/// warband's next End of Game. Deliberately NOT a multi-step wizard like EndOfGamePage - a single
/// screen, no roster/inventory locking (explicit user decision: this only tracks which button shows,
/// nothing is actually blocked while a game is "in progress").</summary>
public partial class StartGameDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    public List<UnavailableWarriorRow> UnavailableWarriors { get; }
    public List<OldWoundWarriorEntry> OldWoundEntries { get; }
    public List<DramatisPersonaAidEntry> AidEntries { get; }
    public string? NextGameNote { get; }

    public bool HasUnavailableWarriors => UnavailableWarriors.Count > 0;
    public bool HasOldWoundRolls => OldWoundEntries.Count > 0;
    public bool HasAidEntries => AidEntries.Count > 0;
    public bool HasNextGameNote => !string.IsNullOrWhiteSpace(NextGameNote);
    public bool HasNothingToShow => !HasUnavailableWarriors && !HasOldWoundRolls && !HasAidEntries && !HasNextGameNote && !HasBeforeBattleItems;

    /// <summary>Objets à usage unique et dépendants (section « Avant la bataille »).</summary>
    public List<BeforeBattleItemEntry> BeforeBattleItems { get; }
    public bool HasBeforeBattleItems => BeforeBattleItems.Count > 0;

    // --- Adversaires (1.6) : valeur de chaque bande adverse, facultative, jusqu'à 3 ---

    private const int MaxOpponents = 3;
    private readonly int _ownRating;

    public ObservableCollection<OpponentRatingEntry> Opponents { get; } = new();

    [ObservableProperty]
    private bool canAddOpponent = true;

    /// <summary>Verdict d'outsider affiché sous les champs (règle du livre + multijoueur du Town Cryer 5).</summary>
    [ObservableProperty]
    private string underdogVerdict = string.Empty;

    public string OwnRatingText { get; }

    /// <summary>Valeurs saisies, dans l'ordre ; enregistrées sur la bande au lancement (Warband.OpponentRatings).</summary>
    public List<int> OpponentRatings => Opponents.Select(o => o.Value).OfType<int>().ToList();

    [RelayCommand]
    private void AddOpponent()
    {
        if (Opponents.Count >= MaxOpponents) return;
        Opponents.Add(new OpponentRatingEntry(string.Empty, OnOpponentsChanged));
        OnOpponentsChanged();
    }

    [RelayCommand]
    private void RemoveOpponent(OpponentRatingEntry entry)
    {
        if (Opponents.Count <= 1) return;
        Opponents.Remove(entry);
        OnOpponentsChanged();
    }

    private void OnOpponentsChanged()
    {
        var loc = LocalizationService.Instance;
        for (var i = 0; i < Opponents.Count; i++)
        {
            Opponents[i].Label = Opponents.Count > 1 ? string.Format(loc["StartGameOpponentNumberFormat"], i + 1) : loc["StartGameOpponentLabel"];
            Opponents[i].CanRemove = Opponents.Count > 1;
        }
        CanAddOpponent = Opponents.Count < MaxOpponents;

        var ratings = OpponentRatings;
        if (ratings.Count == 0)
            UnderdogVerdict = string.Empty;
        else if (UnderdogExperience.GapFor(_ownRating, ratings) is not { } gap)
            UnderdogVerdict = loc[ratings.Count > 1 ? "StartGameNotLowestRating" : "StartGameNotUnderdog"];
        else
        {
            var bonus = UnderdogExperience.BonusForGap(gap);
            var against = ratings.Count > 1 ? string.Format(loc["StartGameNextHighestFormat"], ratings.Min()) : string.Empty;
            UnderdogVerdict = bonus > 0
                ? string.Format(loc["StartGameUnderdogFormat"], gap, against, bonus)
                : string.Format(loc["StartGameGapTooSmallFormat"], gap, against);
        }

        // Aide des Dramatis Personae (ex. Bertha) : même saisie, comparée à la bande adverse la plus forte - le champ
        // de la carte reste modifiable.
        if (ratings.Count > 0)
            foreach (var aid in AidEntries)
                aid.EnemyRating = ratings.Max().ToString();
    }

    /// <summary>One line per RatingGapAidTable.Tier (plus a leading "won't come" row for anything below
    /// the first tier) - built from the table's actual data (user request 2026-09-01: "à voir si on a les
    /// valeurs modifiables plutôt qu'un resx") rather than one hand-written resx sentence, so this
    /// explanation can never silently drift out of sync with the roll logic itself if the table's
    /// breakpoints ever change - both read RatingGapAidTable.Tiers. Shown once for the whole "Aide
    /// conditionnelle" section (every AidEntries card follows the same shared table), not per card.</summary>
    public List<string> AidRuleTierRows { get; }

    public StartGameDialogViewModel(List<UnavailableWarriorRow> unavailableWarriors, List<OldWoundWarriorEntry> oldWoundEntries,
        List<DramatisPersonaAidEntry> aidEntries, string? nextGameNote, int ownRating, List<BeforeBattleItemEntry>? beforeBattleItems = null)
    {
        BeforeBattleItems = beforeBattleItems ?? new List<BeforeBattleItemEntry>();
        _ownRating = ownRating;
        OwnRatingText = string.Format(LocalizationService.Instance["StartGameOwnRatingFormat"], ownRating);
        UnavailableWarriors = unavailableWarriors;
        OldWoundEntries = oldWoundEntries;
        AidEntries = aidEntries;
        NextGameNote = nextGameNote;
        AidRuleTierRows = BuildAidRuleTierRows();
        Opponents.Add(new OpponentRatingEntry(string.Empty, OnOpponentsChanged));
        OnOpponentsChanged();
    }

    private static List<string> BuildAidRuleTierRows()
    {
        var loc = LocalizationService.Instance;
        var rows = new List<string> { string.Format(loc["StartGameAidTierWontComeFormat"], RatingGapAidTable.Tiers[0].MinDifference) };
        for (var i = 0; i < RatingGapAidTable.Tiers.Count; i++)
        {
            var tier = RatingGapAidTable.Tiers[i];
            rows.Add(i + 1 < RatingGapAidTable.Tiers.Count
                ? string.Format(loc["StartGameAidTierRangeFormat"], tier.MinDifference, RatingGapAidTable.Tiers[i + 1].MinDifference - 1, tier.RequiredRoll)
                : string.Format(loc["StartGameAidTierOpenFormat"], tier.MinDifference, tier.RequiredRoll));
        }
        return rows;
    }

    [RelayCommand]
    private void Confirm() => Close(true);

    // Même convention que EndOfGamePageViewModel.Injury.AutoRoll : remplit le champ avec un 1D6
    // tiré par l'appli, modifiable ensuite si le joueur préfère lancer son propre dé physique.
    [RelayCommand]
    private void AutoRoll(OldWoundRollEntry entry) => entry.ManualRoll = Random.Shared.Next(1, 7).ToString();

    [RelayCommand]
    private void AutoRollAid(DramatisPersonaAidEntry entry) => entry.Roll = Random.Shared.Next(1, 7).ToString();

    [RelayCommand]
    private void AutoRollModifier(DiceModifierEntry entry) => entry.Roll = Random.Shared.Next(1, (entry.Modifier.DiceSides ?? 6) + 1).ToString();
}
