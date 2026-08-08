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
    private List<SpecialRule> _allItems = new();
    private HashSet<int> _fighterRuleIds = new();
    private HashSet<int> _itemRuleIds = new();
    private bool _suppressFilterReload;

    /// <summary>Sections of the grid, one per group ("Guerriers &amp; Bandes"/"Objets", or both joined
    /// if a rule is attached to each) - always grouped internally (cf. MutationViewModel.MutationGroups),
    /// the header is just hidden outside the "All" filter (see ShowGroupHeaders).</summary>
    [ObservableProperty]
    private ObservableCollection<SpecialRuleGroup> specialRuleGroups = new();

    private string AllGroupsLabel => Loc["LibFilterAll"];
    private string FighterGroupLabel => Loc["LibFilterSpecialRuleFighters"];
    private string ItemGroupLabel => Loc["LibFilterSpecialRuleItems"];
    private string UncategorizedGroupLabel => Loc["LibFilterUncategorized"];

    [ObservableProperty]
    private ObservableCollection<string> groupFilterOptions = new();

    [ObservableProperty]
    private string selectedGroupFilter = string.Empty;

    /// <summary>Group headers are redundant once a single group is picked (its name is already on the
    /// filter button) - only shown for the "All" filter, cf. MutationViewModel.ShowGroupHeaders.</summary>
    public bool ShowGroupHeaders => SelectedGroupFilter == AllGroupsLabel;

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

    partial void OnSelectedGroupFilterChanged(string value)
    {
        OnPropertyChanged(nameof(ShowGroupHeaders));
        if (_suppressFilterReload) return;
        ApplyFilterAndGroup();
    }

    /// <summary>A rule can in principle be attached to both a fighter (Warband/Warrior/Animal) and an
    /// item - joined like MutationViewModel.GroupNameFor joins multiple warband restrictions. Neither
    /// (a freshly-created rule not attached anywhere yet) falls back to "Non classée".</summary>
    private string GroupNameFor(SpecialRule item)
    {
        var labels = new List<string>();
        if (_fighterRuleIds.Contains(item.Id)) labels.Add(FighterGroupLabel);
        if (_itemRuleIds.Contains(item.Id)) labels.Add(ItemGroupLabel);
        return labels.Count > 0 ? string.Join(", ", labels) : UncategorizedGroupLabel;
    }

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language);
        (_fighterRuleIds, _itemRuleIds) = await _libraryService.GetSpecialRuleAttachmentsAsync();

        // Sélecteur ouvert avec un Scope demandé (WarbandArchetypeEditDialog/WarriorArchetypeEditDialog) :
        // ne propose que les règles pensées pour ce niveau-là, plus celles marquées Both. Hors sélecteur,
        // ou sélecteur sans Scope (Animal/EquipmentItem) : catalogue complet, comportement inchangé.
        if (IsSelectorMode && _pickerNavigation.RequestedScope is { } requestedScope)
            _allItems = _allItems.Where(i => i.Scope == requestedScope || i.Scope == SpecialRuleScope.Both).ToList();

        var previousFilter = string.IsNullOrEmpty(SelectedGroupFilter) ? AllGroupsLabel : SelectedGroupFilter;
        _suppressFilterReload = true;
        GroupFilterOptions = new ObservableCollection<string>(
            new[] { AllGroupsLabel }.Concat(_allItems.Select(GroupNameFor).Distinct()));
        SelectedGroupFilter = GroupFilterOptions.Contains(previousFilter) ? previousFilter : AllGroupsLabel;
        _suppressFilterReload = false;

        ApplyFilterAndGroup();
    }

    private void ApplyFilterAndGroup()
    {
        var filtered = SelectedGroupFilter == AllGroupsLabel
            ? _allItems
            : _allItems.Where(i => GroupNameFor(i) == SelectedGroupFilter).ToList();

        var groups = new ObservableCollection<SpecialRuleGroup>();
        foreach (var item in filtered)
        {
            var groupName = GroupNameFor(item);
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group is null)
            {
                group = new SpecialRuleGroup(groupName);
                groups.Add(group);
            }
            group.Add(new SpecialRuleRow(item));
        }
        SpecialRuleGroups = groups;

        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    [RelayCommand]
    private async Task SelectGroupFilter()
    {
        var result = await ShowActionSheetAsync(Loc["LibFilterCategory"], GroupFilterOptions.ToArray());
        if (result != null) SelectedGroupFilter = result;
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
            ImagePath = s.ImagePath,
            CostMultiplier = s.CostMultiplier,
            Scope = s.Scope
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

    /// <summary>Read-only recap popup (tile info button) - simplest of the 8, just Name/Description.
    /// AllowConcurrentExecutions : voir WarbandArchetypeViewModel.ShowDetails.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ShowDetails(SpecialRuleRow row) =>
        await ShowDialogAsync(new SpecialRuleDetailDialog(new SpecialRuleDetailDialogViewModel(row.Item)));
}
