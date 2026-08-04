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
    private readonly IWarbandService _warbandService;
    private readonly IInjuryPickerService _injuryPicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Warrior item;

    [ObservableProperty]
    private string title;

    /// <summary>Set when Delete succeeds - the caller (WarbandDetailViewModel.EditWarrior) checks this
    /// instead of trying to SaveWarriorAsync a warrior that no longer exists.</summary>
    public bool WasDeleted { get; private set; }

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

        Injuries = new ObservableCollection<WarriorInjury>(item.Injuries);
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
    private async Task Delete()
    {
        // Message dédié (pas ConfirmDeleteAsync générique) : précise que ce n'est pas une mort du
        // personnage (ça se joue via Fin de partie) et que le coût est remboursé au trésor.
        if (!await ConfirmAsync(Loc["DialogDelete"], string.Format(Loc["WarriorDeleteConfirm"], Item.Name)))
            return;

        await _warbandService.DeleteWarriorAsync(Item.Id);
        WasDeleted = true;
        Close(true);
    }

    [RelayCommand]
    private void Save() => Close(true);
}
