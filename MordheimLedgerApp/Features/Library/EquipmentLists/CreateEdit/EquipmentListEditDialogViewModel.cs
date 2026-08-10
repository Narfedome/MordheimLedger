using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;

public partial class EquipmentListEditDialogViewModel : DialogViewModel<bool>
{
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ILibraryService _libraryService;

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
        IReadOnlyList<EquipmentItem> initialItems, ILibraryService libraryService)
    {
        this.item = item;
        this.title = title;
        _libraryService = libraryService;
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
    private async Task ShowItemDetail(EquipmentItem equipmentItem)
    {
        var language = LocalizationService.Instance.Language;
        var categoryLabel = Loc[$"EquipmentCategory{equipmentItem.Category}"];

        var restrictedWarbands = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : (await _libraryService.GetWarbandArchetypesAsync(language))
                .Where(w => equipmentItem.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var restrictedWarriors = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0 || equipmentItem.RestrictedToWarriorArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(equipmentItem.RestrictedToWarbandArchetypeIds, language))
                .Where(w => equipmentItem.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        await ShowDialogAsync(new EquipmentItemDetailDialog(
            new EquipmentItemDetailDialogViewModel(equipmentItem, categoryLabel, restrictedWarbands, restrictedWarriors)));
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
