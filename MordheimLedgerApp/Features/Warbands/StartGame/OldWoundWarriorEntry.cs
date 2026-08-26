using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>One card of the "Jets avant bataille" section - one per warrior who carries at least one
/// Vieille blessure, with one OldWoundRollEntry PER instance carried (a warrior can accumulate more than
/// one across separate games - see OldWoundRollEntry's doc). Grouped into a single card per warrior
/// rather than one card per roll (user feedback 2026-08-26: "au lieu d'avoir 2 card, c'est d'avoir une
/// card avec 2 roll à l'intérieur").</summary>
public class OldWoundWarriorEntry
{
    public Warrior Warrior { get; }
    public List<OldWoundRollEntry> Rolls { get; }

    /// <summary>Whether ANY roll in this card failed - a single 1 among several is enough to sit out
    /// this game (see WarbandDetailViewModel.StartGame), same "one failure is enough" rule the user
    /// confirmed ("si l'un des jets de la blessure est à 1, le personnage ne jouera pas la partie").</summary>
    public bool HasFailure => Rolls.Any(r => r.CanFight == false);

    public OldWoundWarriorEntry(Warrior warrior, List<OldWoundRollEntry> rolls)
    {
        Warrior = warrior;
        Rolls = rolls;
    }
}
