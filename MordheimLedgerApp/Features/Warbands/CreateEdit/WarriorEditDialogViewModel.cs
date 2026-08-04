using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

public partial class WarriorEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, WarriorStatus> _statusByLabel = new();
    private readonly IWarbandService _warbandService;
    private readonly IInjuryPickerService _injuryPicker;

    protected override bool CancelResult => false;

    public ObservableCollection<string> StatusOptions { get; } = new();

    [ObservableProperty]
    private Warrior item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedStatusLabel = string.Empty;

    /// <summary>Alimenté à la fois par le jet de blessure de Fin de partie et par les ajouts manuels
    /// (picker via +, retrait via la croix sur chaque puce). Persisté immédiatement (pas soumis à
    /// Enregistrer/Annuler), même logique que Équipement/Compétences sur la carte guerrier.</summary>
    public ObservableCollection<WarriorInjury> Injuries { get; }

    public WarriorEditDialogViewModel(Warrior item, string title, IWarbandService warbandService, IInjuryPickerService injuryPicker)
    {
        this.item = item;
        this.title = title;
        _warbandService = warbandService;
        _injuryPicker = injuryPicker;

        foreach (var status in new[] { WarriorStatus.Active, WarriorStatus.Dead })
        {
            var label = Loc[$"WarriorStatus{status}"];
            _statusByLabel[label] = status;
            StatusOptions.Add(label);
        }

        selectedStatusLabel = Loc[$"WarriorStatus{item.Status}"];

        Injuries = new ObservableCollection<WarriorInjury>(item.Injuries);
    }

    partial void OnSelectedStatusLabelChanged(string value)
    {
        if (_statusByLabel.TryGetValue(value, out var status))
            Item.Status = status;
    }

    [RelayCommand]
    private async Task AddInjury()
    {
        var injuries = await _injuryPicker.PickInjuriesAsync();
        foreach (var injury in injuries)
        {
            var tracked = await _warbandService.AddWarriorInjuryAsync(Item.Id, injury);
            Injuries.Add(tracked);
        }
    }

    [RelayCommand]
    private async Task RemoveInjury(WarriorInjury tracked)
    {
        await _warbandService.RemoveWarriorInjuryAsync(tracked.Id);
        Injuries.Remove(tracked);
    }

    [RelayCommand]
    private void Save() => Close(true);
}
