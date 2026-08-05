using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IWarbandArchetypePickerNavigationService _pickerNavigation;

    [ObservableProperty]
    private ObservableCollection<WarbandArchetypeRow> warbandArchetypeItems = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private WarbandArchetypeRow? selectedRow;

    /// <summary>Set by WarbandArchetypeSelectorPage right after construction - même bascule
    /// multi-sélection que SpecialRuleViewModel.IsSelectorMode (utilisé pour "réservé à ces bandes" sur
    /// Équipement/Compétences/Montures).</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<WarbandArchetypeRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public WarbandArchetypeViewModel(ILibraryService libraryService, ISpecialRulePickerService specialRulePicker,
        IWarbandArchetypePickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _specialRulePicker = specialRulePicker;
        _pickerNavigation = pickerNavigation;

        // Les pages Bibliothèque sont des onglets TabBar gardés en mémoire par Shell - OnNavigatedTo ne
        // se déclenche pas de façon fiable en changeant d'onglet, donc Name/Description (résolus dans
        // la langue courante) resteraient périmés après un changement de langue depuis Réglages sans ce
        // rechargement explicite. Même pattern que SettingsViewModel.RebuildThemeOptions.
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((WarbandArchetypeViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        WarbandArchetypeItems = new ObservableCollection<WarbandArchetypeRow>(items.Select(i => new WarbandArchetypeRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(WarbandArchetypeRow? oldValue, WarbandArchetypeRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(WarbandArchetypeRow row)
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
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<WarbandArchetype>());

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new WarbandArchetype();
        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(newItem, Loc["WarbandArchetypeCreateTitle"], _specialRulePicker);
        if (await ShowDialogAsync(new WarbandArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarbandArchetypeAsync(newItem, LocalizationService.Instance.Language);
        WarbandArchetypeItems.Add(new WarbandArchetypeRow(newItem));
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;
        var s = row.Item;

        var copy = new WarbandArchetype
        {
            Id = s.Id,
            Name = s.Name,
            Source = s.Source,
            StartingTreasury = s.StartingTreasury,
            MaxWarriors = s.MaxWarriors,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            ImagePath = s.ImagePath,
            SpecialRules = new List<SpecialRule>(s.SpecialRules)
        };

        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(copy, Loc["WarbandArchetypeEditTitle"], _specialRulePicker);
        if (await ShowDialogAsync(new WarbandArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarbandArchetypeAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteWarbandArchetypeAsync(row.Item.Id);
        WarbandArchetypeItems.Remove(row);
        SelectedRow = null;
    }

    [RelayCommand]
    private async Task ManageWarriors()
    {
        if (SelectedRow is not { } row) return;

        await Shell.Current.GoToAsync(nameof(WarriorArchetypeListPage),
            new Dictionary<string, object>
            {
                { "WarbandArchetypeId", row.Item.Id },
                { "WarbandArchetypeName", row.Item.Name }
            });
    }
}
