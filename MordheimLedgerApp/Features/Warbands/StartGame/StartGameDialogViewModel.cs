using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>Informational pre-battle screen ("wizard informatif", user's own framing 2026-08-26) shown
/// by the "Lancer la partie" action that replaces "Fin de partie" on WarbandDetailPage until this
/// warband's next End of Game. Deliberately NOT a multi-step wizard like EndOfGameDialog - a single
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
    public bool HasNothingToShow => !HasUnavailableWarriors && !HasOldWoundRolls && !HasAidEntries && !HasNextGameNote;

    /// <summary>One line per RatingGapAidTable.Tier (plus a leading "won't come" row for anything below
    /// the first tier) - built from the table's actual data (user request 2026-09-01: "à voir si on a les
    /// valeurs modifiables plutôt qu'un resx") rather than one hand-written resx sentence, so this
    /// explanation can never silently drift out of sync with the roll logic itself if the table's
    /// breakpoints ever change - both read RatingGapAidTable.Tiers. Shown once for the whole "Aide
    /// conditionnelle" section (every AidEntries card follows the same shared table), not per card.</summary>
    public List<string> AidRuleTierRows { get; }

    public StartGameDialogViewModel(List<UnavailableWarriorRow> unavailableWarriors, List<OldWoundWarriorEntry> oldWoundEntries,
        List<DramatisPersonaAidEntry> aidEntries, string? nextGameNote)
    {
        UnavailableWarriors = unavailableWarriors;
        OldWoundEntries = oldWoundEntries;
        AidEntries = aidEntries;
        NextGameNote = nextGameNote;
        AidRuleTierRows = BuildAidRuleTierRows();
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

    // Même convention que EndOfGameDialogViewModel.Injury.AutoRoll : remplit le champ avec un 1D6
    // tiré par l'appli, modifiable ensuite si le joueur préfère lancer son propre dé physique.
    [RelayCommand]
    private void AutoRoll(OldWoundRollEntry entry) => entry.ManualRoll = Random.Shared.Next(1, 7).ToString();

    [RelayCommand]
    private void AutoRollAid(DramatisPersonaAidEntry entry) => entry.Roll = Random.Shared.Next(1, 7).ToString();
}
