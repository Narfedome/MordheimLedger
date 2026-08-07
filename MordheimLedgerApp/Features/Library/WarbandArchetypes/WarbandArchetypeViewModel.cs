using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentLists;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IMagicSchoolPickerService _magicSchoolPicker;
    private readonly IWarbandArchetypePickerNavigationService _pickerNavigation;

    private List<WarbandArchetype> _allItems = new();
    private bool _suppressFilterReload;

    /// <summary>Sections of the grid, one per Grade - always grouped internally (cf.
    /// SpellViewModel.SpellGroups), the header is just hidden outside the "All warbands" filter where
    /// it'd be redundant with the filter button already shown above (see ShowGroupHeaders).</summary>
    [ObservableProperty]
    private ObservableCollection<WarbandArchetypeGroup> warbandArchetypeGroups = new();

    private string AllGradesLabel => Loc["WarbandGradeFilterAll"];

    [ObservableProperty]
    private ObservableCollection<string> gradeFilterOptions = new();

    [ObservableProperty]
    private string selectedGradeFilter = string.Empty;

    /// <summary>Group headers are redundant once a single Grade is picked (its name is already on the
    /// filter button) - only shown for the "All warbands" filter, cf. SpellViewModel.ShowGroupHeaders.</summary>
    public bool ShowGroupHeaders => SelectedGradeFilter == AllGradesLabel;

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private WarbandArchetypeRow? selectedRow;

    /// <summary>Set by WarbandArchetypeSelectorPage right after construction - même bascule
    /// multi-sélection que SpecialRuleViewModel.IsSelectorMode (utilisé pour "réservé à ces bandes" sur
    /// Équipement/Compétences/Animaux).</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<WarbandArchetypeRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public WarbandArchetypeViewModel(ILibraryService libraryService, ISpecialRulePickerService specialRulePicker,
        IMagicSchoolPickerService magicSchoolPicker, IWarbandArchetypePickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;
        _pickerNavigation = pickerNavigation;

        // Les pages Bibliothèque sont des onglets TabBar gardés en mémoire par Shell - OnNavigatedTo ne
        // se déclenche pas de façon fiable en changeant d'onglet, donc Name/Description (résolus dans
        // la langue courante) resteraient périmés après un changement de langue depuis Réglages sans ce
        // rechargement explicite. Même pattern que SettingsViewModel.RebuildThemeOptions.
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((WarbandArchetypeViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    partial void OnSelectedGradeFilterChanged(string value)
    {
        OnPropertyChanged(nameof(ShowGroupHeaders));
        if (_suppressFilterReload) return;
        ApplyFilterAndGroup();
    }

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);

        var gradesPresent = _allItems.Select(i => i.Grade).Distinct().OrderBy(g => g);
        var previousFilter = string.IsNullOrEmpty(SelectedGradeFilter) ? AllGradesLabel : SelectedGradeFilter;
        _suppressFilterReload = true;
        GradeFilterOptions = new ObservableCollection<string>(
            new[] { AllGradesLabel }.Concat(gradesPresent.Select(g => Loc[$"WarbandGrade{g}"])));
        SelectedGradeFilter = GradeFilterOptions.Contains(previousFilter) ? previousFilter : AllGradesLabel;
        _suppressFilterReload = false;

        ApplyFilterAndGroup();
    }

    private void ApplyFilterAndGroup()
    {
        var filtered = SelectedGradeFilter == AllGradesLabel
            ? _allItems
            : _allItems.Where(i => Loc[$"WarbandGrade{i.Grade}"] == SelectedGradeFilter).ToList();

        var groups = new ObservableCollection<WarbandArchetypeGroup>();
        foreach (var item in filtered)
        {
            var groupName = Loc[$"WarbandGrade{item.Grade}"];
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group is null)
            {
                group = new WarbandArchetypeGroup(groupName);
                groups.Add(group);
            }
            group.Add(new WarbandArchetypeRow(item));
        }
        if (groups.Count > 0)
            groups[0].IsFirst = true;
        WarbandArchetypeGroups = groups;

        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    [RelayCommand]
    private async Task SelectGradeFilter()
    {
        var result = await ShowActionSheetAsync(Loc["WarbandGradeFilterTitle"], GradeFilterOptions.ToArray());
        if (result != null) SelectedGradeFilter = result;
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
        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(newItem, Loc["WarbandArchetypeCreateTitle"], _specialRulePicker, _magicSchoolPicker);
        if (await ShowDialogAsync(new WarbandArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarbandArchetypeAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
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
            Grade = s.Grade,
            StartingTreasury = s.StartingTreasury,
            MaxWarriors = s.MaxWarriors,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            ImagePath = s.ImagePath,
            SpecialRules = new List<SpecialRule>(s.SpecialRules),
            MagicSchools = new List<MagicSchool>(s.MagicSchools)
        };

        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(copy, Loc["WarbandArchetypeEditTitle"], _specialRulePicker, _magicSchoolPicker);
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
        await LoadData();
    }

    /// <summary>Read-only recap popup (tile info button) - opens instantly, no service call: Général
    /// (Grade/Trésorerie/Description/SpecialRules/MagicSchools) reads entirely off the already-loaded
    /// Item. Guerriers/Équipement fetch lazily on first visit to their own onglet (see
    /// WarbandArchetypeDetailDialogViewModel) - _libraryService is just handed through for that.</summary>
    [RelayCommand]
    private Task ShowDetails(WarbandArchetypeRow row) =>
        ShowDialogAsync(new WarbandArchetypeDetailDialog(
            new WarbandArchetypeDetailDialogViewModel(row.Item, _libraryService)));

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

    [RelayCommand]
    private async Task ManageEquipmentLists()
    {
        if (SelectedRow is not { } row) return;

        await Shell.Current.GoToAsync(nameof(EquipmentListListPage),
            new Dictionary<string, object>
            {
                { "WarbandArchetypeId", row.Item.Id },
                { "WarbandArchetypeName", row.Item.Name }
            });
    }
}
