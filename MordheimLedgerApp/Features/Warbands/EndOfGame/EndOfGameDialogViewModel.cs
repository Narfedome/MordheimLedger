using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

public partial class EndOfGameDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    public ObservableCollection<string> ResultOptions { get; } = new();
    public ObservableCollection<WarriorOutcomeRow> WarriorRows { get; }

    [ObservableProperty]
    private string selectedResult;

    [ObservableProperty]
    private int treasuryFound;

    public EndOfGameDialogViewModel(IEnumerable<Warrior> activeWarriors)
    {
        ResultOptions.Add(Loc["EndOfGameResultVictory"]);
        ResultOptions.Add(Loc["EndOfGameResultDefeat"]);
        ResultOptions.Add(Loc["EndOfGameResultDraw"]);
        selectedResult = ResultOptions[0];

        WarriorRows = new ObservableCollection<WarriorOutcomeRow>(activeWarriors.Select(w => new WarriorOutcomeRow(w)));
    }

    [RelayCommand]
    private void Save() => Close(true);
}

/// <summary>One row per active Warrior in the End of Game dialog — collects the outcome, the caller
/// (WarbandDetailViewModel) applies it via IWarbandService and builds the History sentence.</summary>
public partial class WarriorOutcomeRow : ObservableObject
{
    private readonly Dictionary<string, WarriorStatus> _statusByLabel = new();

    public Warrior Warrior { get; }
    public string Name => Warrior.Name;
    public ObservableCollection<string> StatusOptions { get; } = new();

    [ObservableProperty]
    private int experienceGained;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInjuryTools))]
    private bool isOutOfAction;

    [ObservableProperty]
    private string injuryResultText = string.Empty;

    [ObservableProperty]
    private string selectedStatusLabel = string.Empty;

    public bool ShowInjuryTools => IsOutOfAction;
    public WarriorStatus Status => _statusByLabel.GetValueOrDefault(SelectedStatusLabel, Warrior.Status);

    public WarriorOutcomeRow(Warrior warrior)
    {
        Warrior = warrior;

        var loc = LocalizationService.Instance;
        foreach (var status in new[] { WarriorStatus.Active, WarriorStatus.Dead, WarriorStatus.Retired })
        {
            var label = loc[$"WarriorStatus{status}"];
            _statusByLabel[label] = status;
            StatusOptions.Add(label);
        }

        selectedStatusLabel = _statusByLabel.First(kv => kv.Value == warrior.Status).Key;
    }

    [RelayCommand]
    private void RollInjury()
    {
        var (roll, text) = SeriousInjuryTable.Roll();
        InjuryResultText = $"({roll}) {text}";
    }
}
