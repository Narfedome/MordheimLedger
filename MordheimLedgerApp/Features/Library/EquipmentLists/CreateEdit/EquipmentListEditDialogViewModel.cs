using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;

public partial class EquipmentListEditDialogViewModel : DialogViewModel<bool>
{
    private readonly IEquipmentPickerService _equipmentPicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private EquipmentList item;

    [ObservableProperty]
    private string title;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsItemsTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsItemsTab => SelectedTab == 1;

    /// <summary>Édité en mémoire ici, recopié sur Item.ItemIds à la sauvegarde - même principe que
    /// SkillEditDialogViewModel.RestrictedWarriors. Le picker se limite déjà au pool commun + aux
    /// objets propres à la bande de cette liste (EquipmentPickerService.PickEquipmentAsync filtre par
    /// warbandArchetypeId).</summary>
    public ObservableCollection<EquipmentItem> Items { get; }

    public EquipmentListEditDialogViewModel(EquipmentList item, string title, IEquipmentPickerService equipmentPicker,
        IReadOnlyList<EquipmentItem> initialItems)
    {
        this.item = item;
        this.title = title;
        _equipmentPicker = equipmentPicker;
        Items = new ObservableCollection<EquipmentItem>(initialItems);
    }

    [RelayCommand]
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowItemsTab() => SelectedTab = 1;

    [RelayCommand]
    private async Task AddItem()
    {
        var picked = await _equipmentPicker.PickEquipmentAsync(Item.WarbandArchetypeId);
        foreach (var equipmentItem in picked)
        {
            if (Items.Any(i => i.Id == equipmentItem.Id)) continue;
            Items.Add(equipmentItem);
        }
    }

    [RelayCommand]
    private void RemoveItem(EquipmentItem equipmentItem) => Items.Remove(equipmentItem);

    private bool ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(Item.Name))
        {
            NameError = Loc["LibFieldRequired"];
            return false;
        }
        NameError = null;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!ValidateRequiredFields())
        {
            SelectedTab = 0;
            return;
        }

        Item.ItemIds = Items.Select(i => i.Id).ToList();
        Close(true);
    }
}
