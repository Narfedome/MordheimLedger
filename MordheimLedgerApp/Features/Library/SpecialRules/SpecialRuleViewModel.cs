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
    private HashSet<int> _warbandRuleIds = new();
    private HashSet<int> _warriorRuleIds = new();
    private HashSet<int> _animalRuleIds = new();
    private HashSet<int> _itemRuleIds = new();
    private bool _suppressFilterReload;

    /// <summary>Sections of the grid, one per group (Bandes/Guerriers/Montures/Objets, joined if a rule
    /// is attached to several types) - always grouped internally (cf. MutationViewModel.MutationGroups),
    /// the header is just hidden outside the "All" filter (see ShowGroupHeaders).</summary>
    [ObservableProperty]
    private ObservableCollection<SpecialRuleGroup> specialRuleGroups = new();

    private string AllGroupsLabel => Loc["LibFilterAll"];
    private string WarbandGroupLabel => Loc["LibFilterSpecialRuleWarbands"];
    private string WarriorGroupLabel => Loc["LibFilterSpecialRuleWarriors"];
    private string AnimalGroupLabel => Loc["LibFilterSpecialRuleAnimals"];
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

    /// <summary>A rule can in principle be attached to several types at once (e.g. a rule used both by
    /// a Warband and a Warrior) - joined like MutationViewModel.GroupNameFor joins multiple warband
    /// restrictions. None (a freshly-created rule not attached anywhere yet) falls back to "Non classée".
    /// A material rule (CostMultiplier != null, e.g. "Gromril Weapon") always counts as Objets even with
    /// zero EquipmentItemSpecialRuleEntity rows - it's never pre-attached to a specific item, it's chosen
    /// at purchase time as a weapon upgrade (see SpecialRule.CostMultiplier/WarriorEquipment.MaterialRule),
    /// so the join-table membership that drives every other group never applies to it.</summary>
    private string GroupNameFor(SpecialRule item)
    {
        var labels = new List<string>();
        if (_warbandRuleIds.Contains(item.Id)) labels.Add(WarbandGroupLabel);
        if (_warriorRuleIds.Contains(item.Id)) labels.Add(WarriorGroupLabel);
        if (_animalRuleIds.Contains(item.Id)) labels.Add(AnimalGroupLabel);
        if (_itemRuleIds.Contains(item.Id) || item.CostMultiplier != null) labels.Add(ItemGroupLabel);
        return labels.Count > 0 ? string.Join(", ", labels) : UncategorizedGroupLabel;
    }

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language);
        (_warbandRuleIds, _warriorRuleIds, _animalRuleIds, _itemRuleIds) = await _libraryService.GetSpecialRuleAttachmentsAsync();

        // Sélecteur ouvert pour un contexte précis (WarbandArchetypeEditDialog/WarriorArchetypeEditDialog) :
        // ne propose que les règles déjà attachées à ce type-là quelque part, PLUS celles jamais
        // attachées nulle part (comportement permissif par défaut - sans ça, une règle tout juste créée
        // via le "+" du sélecteur, donc encore sans aucune attache réelle, n'apparaîtrait pas dans son
        // propre sélecteur). Hors sélecteur, ou sélecteur sans contexte (Animal/EquipmentItem) :
        // catalogue complet, comportement inchangé.
        if (IsSelectorMode && _pickerNavigation.RequestedFilterKind is { } filterKind)
        {
            var relevantIds = filterKind == SpecialRuleFilterKind.Warband ? _warbandRuleIds : _warriorRuleIds;
            // Une règle de matériau (CostMultiplier != null, ex. "Gromril Weapon") compte comme classée
            // Objets même sans ligne EquipmentItemSpecialRuleEntity - voir GroupNameFor, même raison :
            // elle ne s'attache jamais à un objet précis, elle est choisie à l'achat
            // (WarriorEquipment.MaterialRule). Sans ça elle repasse "jamais attachée nulle part" et
            // resurgit dans tous les sélecteurs filtrés malgré le fix du regroupement Codex.
            var materialRuleIds = _allItems.Where(i => i.CostMultiplier != null).Select(i => i.Id);
            var everAttachedAnywhere = new HashSet<int>(_warbandRuleIds.Concat(_warriorRuleIds).Concat(_animalRuleIds).Concat(_itemRuleIds).Concat(materialRuleIds));
            _allItems = _allItems.Where(i => relevantIds.Contains(i.Id) || !everAttachedAnywhere.Contains(i.Id)).ToList();
        }

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

        // Sélecteur : le "+" doit se comporter comme si on avait tapé la nouvelle tuile - coché et
        // ajouté à SelectedRows, sans fermer le picker (l'utilisateur peut enchaîner d'autres
        // créations/sélections avant de Confirmer).
        if (IsSelectorMode)
        {
            var row = SpecialRuleGroups.SelectMany(g => g).FirstOrDefault(r => r.Item.Id == newItem.Id);
            if (row != null) Select(row);
        }
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
            Abbreviation = s.Abbreviation
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
