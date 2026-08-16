using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Animals.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Animals;

public partial class AnimalViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IAnimalPickerNavigationService _pickerNavigation;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private List<WarbandArchetype> _warbandArchetypes = new();
    private List<Animal> _allItems = new();
    private bool _suppressFilterReload;

    /// <summary>Sections of the grid, one per group ("Commun" or a warband name) - always grouped
    /// internally (cf. SpellViewModel.SpellGroups), the header is just hidden outside the "All" filter
    /// where it'd be redundant with the filter button already shown above (see ShowGroupHeaders).</summary>
    [ObservableProperty]
    private ObservableCollection<AnimalGroup> animalGroups = new();

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
    private AnimalRow? selectedRow;

    /// <summary>Null (onglet Library CRUD) = pas de filtre, tout le catalogue est parcouru. Non-null
    /// (mode picker, posé par AnimalPickerService avant construction de la page) = ne garde que les
    /// animaux communs (RestrictedToWarbandArchetypeIds vide) ou explicitement restreints à cette bande -
    /// même principe qu'EquipmentItemViewModel.AllowedWarbandArchetypeId, mais Animal n'a qu'une
    /// restriction bande (pas de RestrictedToWarriorArchetypeIds, contrairement à EquipmentItem/Skill).</summary>
    public int? AllowedWarbandArchetypeId { get; set; }

    /// <summary>Set by AnimalSelectorPage right after construction - même bascule multi-sélection
    /// qu'InjuryViewModel.IsSelectorMode (le picker ne renvoie en pratique qu'un animal : le premier
    /// résultat coché remplace l'animal actuel du guerrier, voir WarriorEditDialogViewModel.PickAnimal).</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<AnimalRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public AnimalViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs, IAnimalPickerNavigationService pickerNavigation,
        IWarbandArchetypePickerService warbandPicker, ISpecialRulePickerService specialRulePicker)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _pickerNavigation = pickerNavigation;
        _warbandPicker = warbandPicker;
        _specialRulePicker = specialRulePicker;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((AnimalViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    partial void OnSelectedGroupFilterChanged(string value)
    {
        OnPropertyChanged(nameof(ShowGroupHeaders));
        if (_suppressFilterReload) return;
        ApplyFilterAndGroup();
    }

    /// <summary>Animal has no rulebook category - "Commun" (unrestricted) vs. the warband name(s) it's
    /// restricted to doubles as the group, using data already tracked for the picker rather than adding
    /// an unused field (see AnimalGroup).</summary>
    private string GroupNameFor(Animal item) =>
        item.RestrictedToWarbandArchetypeIds.Count == 0
            ? CommonLabel
            : string.Join(", ", item.RestrictedToWarbandArchetypeIds
                .Select(id => _warbandArchetypes.FirstOrDefault(w => w.Id == id)?.Name)
                .Where(n => n != null));

    private async Task LoadData()
    {
        var loaded = await _libraryService.GetAnimalsAsync(LocalizationService.Instance.Language);
        _allItems = AllowedWarbandArchetypeId is { } warbandId
            ? loaded.Where(i => i.RestrictedToWarbandArchetypeIds.Count == 0 || i.RestrictedToWarbandArchetypeIds.Contains(warbandId)).ToList()
            : loaded;
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);

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

        var groups = new ObservableCollection<AnimalGroup>();
        foreach (var item in filtered)
        {
            var groupName = GroupNameFor(item);
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group is null)
            {
                group = new AnimalGroup(groupName);
                groups.Add(group);
            }
            group.Add(new AnimalRow(item));
        }
        AnimalGroups = groups;

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

    partial void OnSelectedRowChanged(AnimalRow? oldValue, AnimalRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(AnimalRow row)
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
        var newItem = new Animal();
        var dialogViewModel = new AnimalEditDialogViewModel(newItem, Loc["AnimalCreateTitle"], _warbandPicker, _warbandArchetypes, _specialRulePicker);
        if (await ShowDialogAsync(new AnimalEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveAnimalAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Animal
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Cost = s.Cost,
            Rarity = s.Rarity,
            Movement = s.Movement,
            WeaponSkill = s.WeaponSkill,
            BallisticSkill = s.BallisticSkill,
            Strength = s.Strength,
            Toughness = s.Toughness,
            Wounds = s.Wounds,
            Initiative = s.Initiative,
            Attacks = s.Attacks,
            Leadership = s.Leadership,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath,
            RestrictedToWarbandArchetypeIds = s.RestrictedToWarbandArchetypeIds,
            SpecialRules = s.SpecialRules
        };

        var dialogViewModel = new AnimalEditDialogViewModel(copy, Loc["AnimalEditTitle"], _warbandPicker, _warbandArchetypes, _specialRulePicker);
        if (await ShowDialogAsync(new AnimalEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveAnimalAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteAnimalAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Animal>());

    /// <summary>Read-only recap popup (tile info button). AllowConcurrentExecutions : voir
    /// WarbandArchetypeViewModel.ShowDetails.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(AnimalRow row) => _detailDialogs.ShowAnimalDetailDialogAsync(row.Item);
}
