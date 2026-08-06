using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.SpecialRules;

public partial class SpecialRuleViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly ISpecialRulePickerNavigationService _pickerNavigation;

    [ObservableProperty]
    private ObservableCollection<SpecialRuleRow> specialRules = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private SpecialRuleRow? selectedRow;

    /// <summary>Set by SpecialRuleSelectorPage right after construction - même bascule multi-sélection
    /// qu'InjuryViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<SpecialRuleRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public SpecialRuleViewModel(ILibraryService libraryService, ISpecialRulePickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((SpecialRuleViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language);
        SpecialRules = new ObservableCollection<SpecialRuleRow>(allItems.Select(i => new SpecialRuleRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(SpecialRuleRow? oldValue, SpecialRuleRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(SpecialRuleRow row)
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
        var newItem = new SpecialRule();
        var dialogViewModel = new SpecialRuleEditDialogViewModel(newItem, Loc["SpecialRuleCreateTitle"]);
        if (await ShowDialogAsync(new SpecialRuleEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSpecialRuleAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new SpecialRule
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new SpecialRuleEditDialogViewModel(copy, Loc["SpecialRuleEditTitle"]);
        if (await ShowDialogAsync(new SpecialRuleEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSpecialRuleAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteSpecialRuleAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<SpecialRule>());

    /// <summary>Read-only recap popup (tile info button) - simplest of the 8, just Name/Description.</summary>
    [RelayCommand]
    private async Task ShowDetails(SpecialRuleRow row) =>
        await ShowDialogAsync(new SpecialRuleDetailDialog(new SpecialRuleDetailDialogViewModel(row.Item)));
}
