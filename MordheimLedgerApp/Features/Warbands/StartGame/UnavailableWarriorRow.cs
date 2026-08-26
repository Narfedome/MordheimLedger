namespace MordheimLedgerApp.Features.Warbands.StartGame;

/// <summary>One row of the "Guerriers indisponibles" reminder section - pairs a WarriorRow (Dead/
/// Retired/Sick) with a short display reason, resolved once by WarbandDetailViewModel.StartGame since
/// WarriorRow itself has no single "why unavailable" text (IsDead/IsRetired/IsSick are 3 separate
/// bools, each with its own resx label).</summary>
public class UnavailableWarriorRow
{
    public WarriorRow Row { get; }
    public string Reason { get; }

    public UnavailableWarriorRow(WarriorRow row, string reason)
    {
        Row = row;
        Reason = reason;
    }
}
