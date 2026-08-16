using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Injuries.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Injuries;

public partial class InjuryViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IInjuryPickerNavigationService _pickerNavigation;
    private List<Injury> _allItems = new();

    /// <summary>Sections of the grid, one per InjuryCategory (Hero/Henchman) - always grouped
    /// internally, the header is just hidden outside the "All" filter, cf. SkillViewModel.</summary>
    [ObservableProperty]
    private ObservableCollection<InjuryGroup> injuryGroups = new();

    /// <summary>Group headers are redundant once a single category is picked (its name is already on
    /// the filter button) - only shown for the "All" filter, cf. SkillViewModel.ShowGroupHeaders.</summary>
    public bool ShowGroupHeaders => SelectedCategory is null;

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private InjuryRow? selectedRow;

    /// <summary>Le vrai critère de filtre - null = Tout. Voir SkillViewModel.SelectedCategory pour la
    /// raison de la séparation avec SelectedCategoryLabel.</summary>
    [ObservableProperty]
    private InjuryCategory? selectedCategory;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Set by InjurySelectorPage right after construction - même bascule multi-sélection
    /// qu'EquipmentItemViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par ApplyFilter.</summary>
    public ObservableCollection<InjuryRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public InjuryViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs, IInjuryPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _pickerNavigation = pickerNavigation;
        selectedCategoryLabel = Loc["LibFilterAll"];

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((InjuryViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetInjuriesAsync(LocalizationService.Instance.Language);
        RefreshSelectedCategoryLabel();
        ApplyFilter();
    }

    /// <summary>Recompute le libellé affiché depuis SelectedCategory (le vrai critère) - à refaire à
    /// chaque LoadData puisque le texte dépend de la langue courante.</summary>
    private void RefreshSelectedCategoryLabel() =>
        SelectedCategoryLabel = SelectedCategory is { } category ? CategoryLabel(category) : Loc["LibFilterAll"];

    partial void OnSelectedCategoryChanged(InjuryCategory? value) => OnPropertyChanged(nameof(ShowGroupHeaders));

    private void ApplyFilter()
    {
        IEnumerable<Injury> filtered = _allItems;
        if (SelectedCategory is { } category)
            filtered = filtered.Where(i => i.Category == category);

        var groups = new ObservableCollection<InjuryGroup>();
        foreach (var item in filtered)
        {
            var groupName = CategoryLabel(item.Category);
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group is null)
            {
                group = new InjuryGroup(groupName);
                groups.Add(group);
            }
            group.Add(new InjuryRow(item));
        }
        InjuryGroups = groups;

        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    private string CategoryLabel(InjuryCategory category) => Loc[$"InjuryCategory{category}"];

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
    private async Task SelectCategory()
    {
        var categories = Enum.GetValues<InjuryCategory>();
        var options = new[] { Loc["LibFilterAll"] }.Concat(categories.Select(CategoryLabel)).ToArray();

        var index = await ShowActionSheetIndexAsync(Loc["LibFilterCategory"], options);
        if (index < 0) return;

        SelectedCategory = index == 0 ? null : categories[index - 1];
        RefreshSelectedCategoryLabel();
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new Injury();
        var dialogViewModel = new InjuryEditDialogViewModel(newItem, Loc["InjuryCreateTitle"]);
        if (await ShowDialogAsync(new InjuryEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveInjuryAsync(newItem, LocalizationService.Instance.Language);
        _allItems.Add(newItem);
        ApplyFilter();
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
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath,
            Category = s.Category,
            RollRange = s.RollRange
        };

        var dialogViewModel = new InjuryEditDialogViewModel(copy, Loc["InjuryEditTitle"]);
        if (await ShowDialogAsync(new InjuryEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveInjuryAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteInjuryAsync(row.Item.Id);
        _allItems.Remove(row.Item);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Injury>());

    /// <summary>Read-only recap popup (tile info button). AllowConcurrentExecutions : voir
    /// WarbandArchetypeViewModel.ShowDetails.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(InjuryRow row) => _detailDialogs.ShowInjuryDetailDialogAsync(row.Item);
}
