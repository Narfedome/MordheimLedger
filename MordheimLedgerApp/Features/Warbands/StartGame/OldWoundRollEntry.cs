using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>One row of the "Jets avant bataille" section - Vieille blessure (Injury roll "32") requires
/// a 1D6 roll before EVERY future battle, fail on a 1 - the only Serious Injury result that recurs
/// indefinitely rather than resolving once at the End of Game wizard (see SERIOUS_INJURIES_STATUS.md).
/// Same "type what you physically rolled" idiom as WarriorOutcomeRow.ManualRoll - the app never rolls
/// dice on the player's behalf.</summary>
public partial class OldWoundRollEntry : ObservableObject
{
    public Warrior Warrior { get; }

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

    public OldWoundRollEntry(Warrior warrior) => Warrior = warrior;
}
