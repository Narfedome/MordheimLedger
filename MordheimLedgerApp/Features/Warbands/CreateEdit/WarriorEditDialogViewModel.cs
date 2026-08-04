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
    private readonly Warband _warband;
    private readonly IWarbandService _warbandService;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly IInjuryPickerService _injuryPicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Warrior item;

    [ObservableProperty]
    private string title;

    /// <summary>Set when Delete succeeds - the caller (WarbandDetailViewModel.EditWarrior) checks this
    /// instead of trying to SaveWarriorAsync a warrior that no longer exists.</summary>
    public bool WasDeleted { get; private set; }

    /// <summary>Onglets Équipement/Compétences/Blessures - même pattern toggle (pas de vrai TabbedPage)
    /// que Roster/Historique sur WarbandDetailPage, adapté à 3 sections avec un index plutôt que des
    /// bools séparés (précédent local : CurrentStep/IsStepN de EndOfGameDialogViewModel).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEquipmentTab))]
    [NotifyPropertyChangedFor(nameof(IsSkillsTab))]
    [NotifyPropertyChangedFor(nameof(IsInjuriesTab))]
    private int selectedTab;

    public bool IsEquipmentTab => SelectedTab == 0;
    public bool IsSkillsTab => SelectedTab == 1;
    public bool IsInjuriesTab => SelectedTab == 2;

    [RelayCommand]
    private void ShowEquipmentTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowSkillsTab() => SelectedTab = 1;

    [RelayCommand]
    private void ShowInjuriesTab() => SelectedTab = 2;

    /// <summary>Équipement/Compétences/Blessures sont toutes persistées immédiatement (pas soumises à
    /// Enregistrer/Annuler) - regroupées ici depuis la carte guerrier (WarbandDetailPage, qui reste
    /// lecture seule pour ces trois listes) plutôt que gérées à deux endroits différents.</summary>
    public ObservableCollection<WarriorEquipment> Equipment { get; }
    public ObservableCollection<WarriorSkill> Skills { get; }
    public ObservableCollection<WarriorInjury> Injuries { get; }

    public WarriorEditDialogViewModel(Warrior item, string title, Warband warband, IWarbandService warbandService,
        IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker, IInjuryPickerService injuryPicker)
    {
        this.item = item;
        this.title = title;
        _warband = warband;
        _warbandService = warbandService;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _injuryPicker = injuryPicker;

        Equipment = new ObservableCollection<WarriorEquipment>(item.Equipment);
        Skills = new ObservableCollection<WarriorSkill>(item.Skills);
        Injuries = new ObservableCollection<WarriorInjury>(item.Injuries);
    }

    [RelayCommand]
    private async Task AddEquipment()
    {
        var items = await _equipmentPicker.PickEquipmentAsync();
        foreach (var equipmentItem in items)
        {
            // Sélection multiple : on paye/ajoute un par un, et on s'arrête au premier objet trop cher
            // plutôt que de tout annuler - même logique que l'ancien AddEquipment de WarbandDetailViewModel.
            if (_warband.Treasury < equipmentItem.Cost)
            {
                await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                break;
            }

            _warband.Treasury -= equipmentItem.Cost;
            await _warbandService.SaveWarbandAsync(_warband);

            var carried = await _warbandService.AddWarriorEquipmentAsync(Item.Id, equipmentItem);
            Equipment.Add(carried);
        }
    }

    [RelayCommand]
    private async Task RemoveEquipment(WarriorEquipment carried)
    {
        await _warbandService.RemoveWarriorEquipmentAsync(carried.Id);
        Equipment.Remove(carried);
    }

    [RelayCommand]
    private async Task AddSkill()
    {
        var skills = await _skillPicker.PickSkillAsync();
        foreach (var skill in skills)
        {
            var learned = await _warbandService.AddWarriorSkillAsync(Item.Id, skill);
            Skills.Add(learned);
        }
    }

    [RelayCommand]
    private async Task RemoveSkill(WarriorSkill learned)
    {
        await _warbandService.RemoveWarriorSkillAsync(learned.Id);
        Skills.Remove(learned);
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
