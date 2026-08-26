using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>One row of the "Jets avant bataille" section - Vieille blessure (Injury roll "32") requires
/// a 1D6 roll before EVERY future battle, fail on a 1 - the only Serious Injury result that recurs
/// indefinitely rather than resolving once at the End of Game wizard (see SERIOUS_INJURIES_STATUS.md).
/// Same "type what you physically rolled" idiom as WarriorOutcomeRow.ManualRoll - the app never rolls
/// dice on the player's behalf.
///
/// One entry PER Vieille blessure Injury instance the warrior carries, not one per warrior - user
/// feedback (2026-08-26): "si on a plusieurs oldwound, on doit tirer le nombre de old wound... 2 old
/// wound = 2 jets... ça augmente les chances de ne pas jouer". A warrior can accumulate more than one
/// (e.g. two separate Serious Injury results both landing on 32 across different games), and each one
/// is tested independently - any single failure is enough to sit out. See
/// WarbandDetailViewModel.StartGame for how Subtitle disambiguates multiple rows for the same
/// warrior ("(1/2)", "(2/2)").</summary>
public partial class OldWoundRollEntry : ObservableObject
{
    public Warrior Warrior { get; }
    public string Subtitle { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRolled))]
    [NotifyPropertyChangedFor(nameof(CanFight))]
    [NotifyPropertyChangedFor(nameof(ShowPass))]
    [NotifyPropertyChangedFor(nameof(ShowFail))]
    private string manualRoll = string.Empty;

    public bool HasRolled => int.TryParse(ManualRoll, out _);

    /// <summary>Null until a valid roll is entered - fails only on a 1, same 1D6 convention as every
    /// other manual roll in the app.</summary>
    public bool? CanFight => int.TryParse(ManualRoll, out var roll) ? roll != 1 : null;

    public bool ShowPass => CanFight == true;
    public bool ShowFail => CanFight == false;

    public OldWoundRollEntry(Warrior warrior, string subtitle)
    {
        Warrior = warrior;
        Subtitle = subtitle;
    }
}
