using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Skills.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Skills;

public partial class SkillViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly ISkillPickerNavigationService _pickerNavigation;
    private List<Skill> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<SkillRow> skills = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private SkillRow? selectedRow;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    public SkillViewModel(ILibraryService libraryService, ISkillPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;
        selectedCategoryLabel = Loc["LibFilterAll"];
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetSkillsAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var allLabel = Loc["LibFilterAll"];
        var filtered = SelectedCategoryLabel == allLabel
            ? _allItems
            : _allItems.Where(i => CategoryLabel(i.Category) == SelectedCategoryLabel).ToList();

        Skills = new ObservableCollection<SkillRow>(filtered.Select(i => new SkillRow(i)));
        SelectedRow = null;
    }

    private string CategoryLabel(SkillCategory category) => Loc[$"SkillCategory{category}"];

    partial void OnSelectedRowChanged(SkillRow? oldValue, SkillRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(SkillRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task SelectCategory()
    {
        var allLabel = Loc["LibFilterAll"];
        var options = new[] { allLabel }.Concat(Enum.GetValues<SkillCategory>().Select(CategoryLabel)).ToArray();

        var result = await ShowActionSheetAsync(Loc["LibFilterCategory"], options);
        if (result is null) return;

        SelectedCategoryLabel = result;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new Skill();
        var dialogViewModel = new SkillEditDialogViewModel(newItem, Loc["SkillCreateTitle"]);
        if (await ShowDialogAsync(new SkillEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSkillAsync(newItem);
        _allItems.Add(newItem);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Skill
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Description = s.Description,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new SkillEditDialogViewModel(copy, Loc["SkillEditTitle"]);
        if (await ShowDialogAsync(new SkillEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSkillAsync(copy);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteSkillAsync(row.Item.Id);
        _allItems.Remove(row.Item);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        if (SelectedRow is { } row)
            await _pickerNavigation.ClosePickerAsync(row.Item);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(null);
}
