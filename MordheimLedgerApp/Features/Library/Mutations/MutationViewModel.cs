using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Mutations.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Mutations;

public partial class MutationViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IMutationPickerNavigationService _pickerNavigation;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private List<WarbandArchetype> _warbandArchetypes = new();
    private List<Mutation> _allItems = new();
    private bool _suppressFilterReload;

    /// <summary>Sections of the grid, one per group ("Commun" or a warband name) - always grouped
    /// internally (cf. SpellViewModel.SpellGroups), the header is just hidden outside the "All" filter
    /// where it'd be redundant with the filter button already shown above (see ShowGroupHeaders).</summary>
    [ObservableProperty]
    private ObservableCollection<MutationGroup> mutationGroups = new();

    private string AllGroupsLabel => Loc["LibFilterAll"];
    private string CommonLabel => Loc["LibFilterCommon"];

    [ObservableProperty]
    private ObservableCollection<string> groupFilterOptions = new();

    [ObservableProperty]
    private string selectedGroupFilter = string.Empty;

    /// <summary>Group headers are redundant once a single group is picked (its name is already on the
    /// filter button) - only shown for the "All" filter, cf. SpellViewModel.ShowGroupHeaders.</summary>
    public bool ShowGroupHeaders => SelectedGroupFilter == AllGroupsLabel;

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private MutationRow? selectedRow;

    /// <summary>Set by MutationSelectorPage right after construction - même bascule multi-sélection
    /// qu'InjuryViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<MutationRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    /// <summary>Null (Library CRUD tab) = no filter. Non-null (picker mode, set by
    /// MutationPickerService before construction) = only mutations whose RestrictedToWarbandArchetypeIds
    /// is empty (common) or contains this id are shown - see WarriorEditDialogViewModel.AddMutation.</summary>
    public int? AllowedWarbandArchetypeId { get; set; }

    public MutationViewModel(ILibraryService libraryService, IMutationPickerNavigationService pickerNavigation,
        IWarbandArchetypePickerService warbandPicker)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;
        _warbandPicker = warbandPicker;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((MutationViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    partial void OnSelectedGroupFilterChanged(string value)
    {
        OnPropertyChanged(nameof(ShowGroupHeaders));
        if (_suppressFilterReload) return;
        ApplyFilterAndGroup();
    }

    /// <summary>Mutation has no rulebook category - "Commun" (unrestricted) vs. the warband name(s) it's
    /// restricted to doubles as the group, using data already tracked for the picker rather than adding
    /// an unused field (see MutationGroup).</summary>
    private string GroupNameFor(Mutation item) =>
        item.RestrictedToWarbandArchetypeIds.Count == 0
            ? CommonLabel
            : string.Join(", ", item.RestrictedToWarbandArchetypeIds
                .Select(id => _warbandArchetypes.FirstOrDefault(w => w.Id == id)?.Name)
                .Where(n => n != null));

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetMutationsAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);

        var allowedFiltered = AllowedWarbandArchetypeId is { } warbandId
            ? _allItems.Where(m => m.RestrictedToWarbandArchetypeIds.Count == 0 || m.RestrictedToWarbandArchetypeIds.Contains(warbandId)).ToList()
            : _allItems;

        var previousFilter = string.IsNullOrEmpty(SelectedGroupFilter) ? AllGroupsLabel : SelectedGroupFilter;
        _suppressFilterReload = true;
        GroupFilterOptions = new ObservableCollection<string>(
            new[] { AllGroupsLabel }.Concat(allowedFiltered.Select(GroupNameFor).Distinct()));
        SelectedGroupFilter = GroupFilterOptions.Contains(previousFilter) ? previousFilter : AllGroupsLabel;
        _suppressFilterReload = false;

        ApplyFilterAndGroup();
    }

    private void ApplyFilterAndGroup()
    {
        var allowedFiltered = AllowedWarbandArchetypeId is { } warbandId
            ? _allItems.Where(m => m.RestrictedToWarbandArchetypeIds.Count == 0 || m.RestrictedToWarbandArchetypeIds.Contains(warbandId)).ToList()
            : _allItems;

        var filtered = SelectedGroupFilter == AllGroupsLabel
            ? allowedFiltered
            : allowedFiltered.Where(i => GroupNameFor(i) == SelectedGroupFilter).ToList();

        var groups = new ObservableCollection<MutationGroup>();
        foreach (var item in filtered)
        {
            var groupName = GroupNameFor(item);
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group is null)
            {
                group = new MutationGroup(groupName);
                groups.Add(group);
            }
            group.Add(new MutationRow(item));
        }
        if (groups.Count > 0)
            groups[0].IsFirst = true;
        MutationGroups = groups;

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

    partial void OnSelectedRowChanged(MutationRow? oldValue, MutationRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(MutationRow row)
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
        var newItem = new Mutation();
        var dialogViewModel = new MutationEditDialogViewModel(newItem, Loc["MutationCreateTitle"], _warbandPicker, _warbandArchetypes);
        if (await ShowDialogAsync(new MutationEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMutationAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Mutation
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Cost = s.Cost,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath,
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds)
        };

        var dialogViewModel = new MutationEditDialogViewModel(copy, Loc["MutationEditTitle"], _warbandPicker, _warbandArchetypes);
        if (await ShowDialogAsync(new MutationEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMutationAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteMutationAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Mutation>());

    /// <summary>Read-only recap popup (tile info button) - same restriction-id resolution as GroupNameFor.</summary>
    [RelayCommand]
    private async Task ShowDetails(MutationRow row)
    {
        var restrictedWarbands = _warbandArchetypes.Where(w => row.Item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();
        var dialogViewModel = new MutationDetailDialogViewModel(row.Item, restrictedWarbands);
        await ShowDialogAsync(new MutationDetailDialog(dialogViewModel));
    }
}
