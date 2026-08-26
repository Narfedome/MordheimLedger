using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;

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
    public List<OldWoundRollEntry> OldWoundRolls { get; }
    public string? NextGameNote { get; }

    public bool HasUnavailableWarriors => UnavailableWarriors.Count > 0;
    public bool HasOldWoundRolls => OldWoundRolls.Count > 0;
    public bool HasNextGameNote => !string.IsNullOrWhiteSpace(NextGameNote);
    public bool HasNothingToShow => !HasUnavailableWarriors && !HasOldWoundRolls && !HasNextGameNote;

    public StartGameDialogViewModel(List<UnavailableWarriorRow> unavailableWarriors, List<OldWoundRollEntry> oldWoundRolls, string? nextGameNote)
    {
        UnavailableWarriors = unavailableWarriors;
        OldWoundRolls = oldWoundRolls;
        NextGameNote = nextGameNote;
    }

    [RelayCommand]
    private void Confirm() => Close(true);

    // Même convention que EndOfGameDialogViewModel.Injury.AutoRoll : remplit le champ avec un 1D6
    // tiré par l'appli, modifiable ensuite si le joueur préfère lancer son propre dé physique.
    [RelayCommand]
    private void AutoRoll(OldWoundRollEntry entry) => entry.ManualRoll = Random.Shared.Next(1, 7).ToString();
}
