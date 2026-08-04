using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Injuries.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Injuries;

public partial class InjuryViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IInjuryPickerNavigationService _pickerNavigation;

    [ObservableProperty]
    private ObservableCollection<InjuryRow> injuries = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private InjuryRow? selectedRow;

    /// <summary>Set by InjurySelectorPage right after construction - même bascule multi-sélection
    /// qu'EquipmentItemViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<InjuryRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public InjuryViewModel(ILibraryService libraryService, IInjuryPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetInjuriesAsync();
        Injuries = new ObservableCollection<InjuryRow>(allItems.Select(i => new InjuryRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(InjuryRow? oldValue, InjuryRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(InjuryRow row)
    {
        if (!IsSelectorMode)
        {
            SelectedRow = row;
            return;
        }

        row.IsSelected = !row.IsSelected;
        if (row.IsSelected) SelectedRows.Add(row);
        else SelectedRows.Remove(row);
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new Injury();
        var dialogViewModel = new InjuryEditDialogViewModel(newItem, Loc["InjuryCreateTitle"]);
        if (await ShowDialogAsync(new InjuryEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveInjuryAsync(newItem);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Injury
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new InjuryEditDialogViewModel(copy, Loc["InjuryEditTitle"]);
        if (await ShowDialogAsync(new InjuryEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveInjuryAsync(copy);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteInjuryAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Injury>());
}
