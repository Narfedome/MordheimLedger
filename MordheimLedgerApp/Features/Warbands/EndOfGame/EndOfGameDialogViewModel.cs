using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
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

    // Lance les dés à la place du joueur (D66) et affiche tout de suite le résultat complet dans une
    // popup - le champ ManualRoll reste modifiable ensuite si le joueur préfère un jet physique.
    [RelayCommand]
    private async Task AutoRoll(WarriorOutcomeRow row)
    {
        var (roll, text) = SeriousInjuryTable.Roll();
        row.ManualRoll = roll.ToString();
        row.InjuryResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
    }

    // Pour un jet fait à la table (dés physiques) : le joueur saisit son score dans ManualRoll, ce
    // bouton affiche juste le résultat correspondant sans en tirer un nouveau.
    [RelayCommand]
    private async Task ShowInjuryResult(WarriorOutcomeRow row)
    {
        if (!int.TryParse(row.ManualRoll, out var roll) || !SeriousInjuryTable.TryGet(roll, out var text))
        {
            await ShowInfoAsync(Loc["EndOfGameRoll"], Loc["EndOfGameInvalidRoll"]);
            return;
        }

        row.InjuryResultText = text;
        await ShowInfoAsync(string.Format(Loc["EndOfGameInjuryResultTitle"], roll), text);
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

    /// <summary>Le score D66 - saisi à la main (jet physique) ou rempli par AutoRoll.</summary>
    [ObservableProperty]
    private string manualRoll = string.Empty;

    /// <summary>Texte complet du résultat une fois consulté (via AutoRoll ou ShowInjuryResult) -
    /// c'est ce texte qui alimente la note du guerrier et la phrase d'Historique à la sauvegarde.</summary>
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
}
