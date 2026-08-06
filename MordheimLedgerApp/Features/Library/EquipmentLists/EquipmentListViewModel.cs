using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentLists;

[QueryProperty(nameof(WarbandArchetypeId), "WarbandArchetypeId")]
[QueryProperty(nameof(WarbandArchetypeName), "WarbandArchetypeName")]
public partial class EquipmentListViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IEquipmentPickerService _equipmentPicker;

    [ObservableProperty]
    private int warbandArchetypeId;

    [ObservableProperty]
    private string warbandArchetypeName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<EquipmentListRow> equipmentListItems = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private EquipmentListRow? selectedRow;

    public EquipmentListViewModel(ILibraryService libraryService, IEquipmentPickerService equipmentPicker)
    {
        _libraryService = libraryService;
        _equipmentPicker = equipmentPicker;

        // Voir WarriorArchetypeViewModel - même besoin de rechargement explicite sur changement de
        // langue (cette page est atteinte par push depuis la Bibliothèque, mais rien n'empêche l'appli
        // d'y être déjà quand la langue change).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((EquipmentListViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetEquipmentListsAsync(WarbandArchetypeId, LocalizationService.Instance.Language);
        EquipmentListItems = new ObservableCollection<EquipmentListRow>(items.Select(i => new EquipmentListRow(i)));
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(EquipmentListRow? oldValue, EquipmentListRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private static async Task Back() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void Select(EquipmentListRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new EquipmentList { WarbandArchetypeId = WarbandArchetypeId };
        var dialogViewModel = new EquipmentListEditDialogViewModel(newItem, Loc["EquipmentListCreateTitle"], _equipmentPicker, Array.Empty<EquipmentItem>());
        if (await ShowDialogAsync(new EquipmentListEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentListAsync(newItem, LocalizationService.Instance.Language);
        EquipmentListItems.Add(new EquipmentListRow(newItem));
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new EquipmentList
        {
            Id = s.Id,
            WarbandArchetypeId = s.WarbandArchetypeId,
            Name = s.Name,
            NameKey = s.NameKey,
            Source = s.Source,
            ItemIds = new List<int>(s.ItemIds)
        };

        var allItems = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);
        var initialItems = allItems.Where(i => copy.ItemIds.Contains(i.Id)).ToList();
        var dialogViewModel = new EquipmentListEditDialogViewModel(copy, Loc["EquipmentListEditTitle"], _equipmentPicker, initialItems);
        if (await ShowDialogAsync(new EquipmentListEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentListAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteEquipmentListAsync(row.Item.Id);
        EquipmentListItems.Remove(row);
        SelectedRow = null;
    }
}
