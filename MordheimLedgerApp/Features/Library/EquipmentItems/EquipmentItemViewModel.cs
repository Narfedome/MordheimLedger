using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

public partial class EquipmentItemViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private List<EquipmentItem> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<EquipmentItem> equipmentItems = new();

    [ObservableProperty]
    private EquipmentItem? selectedEquipmentItem;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    public EquipmentItemViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
        selectedCategoryLabel = Loc["LibFilterAll"];
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetEquipmentItemsAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var allLabel = Loc["LibFilterAll"];
        var filtered = SelectedCategoryLabel == allLabel
            ? _allItems
            : _allItems.Where(i => CategoryLabel(i.Category) == SelectedCategoryLabel).ToList();

        EquipmentItems = new ObservableCollection<EquipmentItem>(filtered);
    }

    private string CategoryLabel(EquipmentCategory category) => Loc[$"EquipmentCategory{category}"];

    [RelayCommand]
    private async Task SelectCategory()
    {
        var allLabel = Loc["LibFilterAll"];
        var options = new[] { allLabel }.Concat(Enum.GetValues<EquipmentCategory>().Select(CategoryLabel)).ToArray();

        var result = await ShowActionSheetAsync(Loc["LibFilterCategory"], options);
        if (result is null) return;

        SelectedCategoryLabel = result;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new EquipmentItem();
        var dialogViewModel = new EquipmentItemEditDialogViewModel(newItem, Loc["EquipmentItemCreateTitle"]);
        if (await ShowDialogAsync(new EquipmentItemEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentItemAsync(newItem);
        _allItems.Add(newItem);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedEquipmentItem is null) return;

        var s = SelectedEquipmentItem;
        var copy = new EquipmentItem
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Cost = s.Cost,
            Rarity = s.Rarity,
            Description = s.Description,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new EquipmentItemEditDialogViewModel(copy, Loc["EquipmentItemEditTitle"]);
        if (await ShowDialogAsync(new EquipmentItemEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentItemAsync(copy);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedEquipmentItem is null) return;
        if (!await ConfirmDeleteAsync(SelectedEquipmentItem.Name)) return;

        await _libraryService.DeleteEquipmentItemAsync(SelectedEquipmentItem.Id);
        EquipmentItems.Remove(SelectedEquipmentItem);
        _allItems.Remove(SelectedEquipmentItem);
        SelectedEquipmentItem = null;
    }
}
