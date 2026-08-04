using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

public partial class WarriorEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, WarriorStatus> _statusByLabel = new();

    protected override bool CancelResult => false;

    public ObservableCollection<string> StatusOptions { get; } = new();

    [ObservableProperty]
    private Warrior item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedStatusLabel = string.Empty;

    public WarriorEditDialogViewModel(Warrior item, string title)
    {
        this.item = item;
        this.title = title;

        foreach (var status in new[] { WarriorStatus.Active, WarriorStatus.Dead })
        {
            var label = Loc[$"WarriorStatus{status}"];
            _statusByLabel[label] = status;
            StatusOptions.Add(label);
        }

        selectedStatusLabel = Loc[$"WarriorStatus{item.Status}"];
    }

    partial void OnSelectedStatusLabelChanged(string value)
    {
        if (_statusByLabel.TryGetValue(value, out var status))
            Item.Status = status;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
