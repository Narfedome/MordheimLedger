using CommunityToolkit.Mvvm.ComponentModel;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One D6 of the Exploration roll (see EndOfGameDialogViewModel.ExplorationDice/
/// SyncExplorationDice) - as many entries as ExplorationDiceCount computes, each independently
/// filled by hand or by AutoRollExplorationDie. Value is null while empty/invalid (1-6 only), same
/// "string Entry, not int" idiom as every other roll field in this wizard.</summary>
public partial class ExplorationDieEntry : ObservableObject
{
    public int Index { get; }

    [ObservableProperty]
    private string manualRoll = string.Empty;

    [ObservableProperty]
    private string? rollError;

    public int? Value => int.TryParse(ManualRoll, out var v) && v is >= 1 and <= 6 ? v : null;

    partial void OnManualRollChanged(string value)
    {
        OnPropertyChanged(nameof(Value));
        if (Value is not null) RollError = null;
    }

    public ExplorationDieEntry(int index) => Index = index;
}
