using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.RacialProfiles;
using MordheimLedgerApp.Features.Library.Races;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IMagicSchoolPickerService _magicSchoolPicker;
    private readonly IEquipmentPickerService _equipmentPicker;
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

    public SelectionMode SelectionMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<WarbandArchetypeRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0 || SelectedRow != null;

    public WarbandArchetypeViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs, ISpecialRulePickerService specialRulePicker,
        IMagicSchoolPickerService magicSchoolPicker, IEquipmentPickerService equipmentPicker,
        IWarbandArchetypePickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;
        _equipmentPicker = equipmentPicker;
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
        if (!IsSelectorMode || SelectionMode == SelectionMode.Single)
        {
            SelectedRow = row;
            OnPropertyChanged(nameof(HasSelectedRows));
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
        if (SelectionMode == SelectionMode.Single && SelectedRow != null)
        {
            await  _pickerNavigation.ClosePickerAsync(SelectedRow.Item);
        }
        else
        {
            var items = SelectedRows.Select(r => r.Item).ToList();
            await _pickerNavigation.ClosePickerAsync(items);
        }

    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<WarbandArchetype>());

    [RelayCommand]
    private async Task Create()
    {
        // Valeurs de départ raisonnables plutôt que 0/null - purement indicatives, l'utilisateur les
        // ajuste ou les efface (MaxWarriors reste nullable, 10 n'est qu'un point de départ arbitraire).
        var newItem = new WarbandArchetype { StartingTreasury = 500, MaxWarriors = 10 };
        var allRaces = await _libraryService.GetRacesAsync(LocalizationService.Instance.Language);
        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(newItem, Loc["WarbandArchetypeCreateTitle"],
            _specialRulePicker, _magicSchoolPicker, _libraryService, _detailDialogs, _equipmentPicker, allRaces);
        if (await ShowDialogAsync(new WarbandArchetypeEditDialog(dialogViewModel)) != true) return;

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
            MagicSchools = new List<MagicSchool>(s.MagicSchools),
            RaceId = s.RaceId
        };

        var allRaces = await _libraryService.GetRacesAsync(LocalizationService.Instance.Language);
        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(copy, Loc["WarbandArchetypeEditTitle"],
            _specialRulePicker, _magicSchoolPicker, _libraryService, _detailDialogs, _equipmentPicker, allRaces);
        if (await ShowDialogAsync(new WarbandArchetypeEditDialog(dialogViewModel)) != true) return;

        await LoadData();
    }

    /// <summary>Ouvre le catalogue Race en édition (Créer/Renommer/Supprimer) - même idiome que
    /// SpellViewModel.ManageMagicSchools, reflété ici sur l'onglet Bandes puisque Race classifie
    /// WarbandArchetype comme MagicSchool classifie Spell.</summary>
    [RelayCommand]
    private static async Task ManageRaces() => await Shell.Current.GoToAsync(nameof(RaceListPage));

    /// <summary>Ouvre le catalogue RacialProfile (maximums de caractéristiques par type de créature) -
    /// même idiome que ManageRaces juste au-dessus, reflété ici pour la même raison (WarriorArchetype,
    /// pas WarbandArchetype directement, mais aucun onglet Codex dédié pour l'instant).</summary>
    [RelayCommand]
    private static async Task ManageRacialProfiles() => await Shell.Current.GoToAsync(nameof(RacialProfileListPage));

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
    /// WarbandArchetypeDetailDialogViewModel) - _libraryService is just handed through for that.
    /// AllowConcurrentExecutions : ShowDetailsCommand est une seule instance partagée par toutes les
    /// tuiles (seul CommandParameter change) - sans ça, AsyncRelayCommand désactive tout le monde
    /// (CanExecute lié à IsRunning) pendant qu'un dialog est ouvert pour UNE tuile, pas juste elle.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(WarbandArchetypeRow row) => _detailDialogs.ShowWarbandArchetypeDetailDialogAsync(row.Item);
}
