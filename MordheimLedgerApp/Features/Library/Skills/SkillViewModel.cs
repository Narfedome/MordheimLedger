using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Skills.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Skills;

public partial class SkillViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly ISkillPickerNavigationService _pickerNavigation;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private List<Skill> _allItems = new();
    private List<WarbandArchetype> _warbandArchetypes = new();

    [ObservableProperty]
    private ObservableCollection<SkillRow> skills = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private SkillRow? selectedRow;

    /// <summary>Le vrai critère de filtre - null = Tout. Distinct de SelectedCategoryLabel (juste
    /// l'affichage) : comparer des libellés résolus par LocalizationService cassait le filtre au
    /// changement de langue (le libellé "Tout" figé dans l'ancienne langue ne correspondait plus à
    /// rien après un rechargement en LoadData, filtrant silencieusement la liste à vide).</summary>
    [ObservableProperty]
    private SkillCategory? selectedCategory;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Set by SkillSelectorPage right after construction - même bascule multi-sélection
    /// qu'EquipmentItemViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par ApplyFilter.</summary>
    public ObservableCollection<SkillRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    /// <summary>Null (Library CRUD tab) = no filter. Non-null (picker mode, set by SkillPickerService
    /// before construction) = only skills whose RestrictedToWarbandArchetypeIds is empty (common) or
    /// contains this id are shown - see WarriorEditDialogViewModel.AddSkill/EndOfGameDialogViewModel.</summary>
    public int? AllowedWarbandArchetypeId { get; set; }

    public SkillViewModel(ILibraryService libraryService, ISkillPickerNavigationService pickerNavigation,
        IWarbandArchetypePickerService warbandPicker)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;
        _warbandPicker = warbandPicker;
        selectedCategoryLabel = Loc["LibFilterAll"];

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((SkillViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetSkillsAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        RefreshSelectedCategoryLabel();
        ApplyFilter();
    }

    /// <summary>Recompute le libellé affiché depuis SelectedCategory (le vrai critère) - à refaire à
    /// chaque LoadData puisque le texte dépend de la langue courante.</summary>
    private void RefreshSelectedCategoryLabel() =>
        SelectedCategoryLabel = SelectedCategory is { } category ? CategoryLabel(category) : Loc["LibFilterAll"];

    private void ApplyFilter()
    {
        IEnumerable<Skill> filtered = _allItems;
        if (SelectedCategory is { } category)
            filtered = filtered.Where(i => i.Category == category);
        if (AllowedWarbandArchetypeId is { } warbandId)
            filtered = filtered.Where(i => i.RestrictedToWarbandArchetypeIds.Count == 0 || i.RestrictedToWarbandArchetypeIds.Contains(warbandId));

        Skills = new ObservableCollection<SkillRow>(filtered.Select(i => new SkillRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    private string CategoryLabel(SkillCategory category) => Loc[$"SkillCategory{category}"];

    partial void OnSelectedRowChanged(SkillRow? oldValue, SkillRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(SkillRow row)
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
        var categories = Enum.GetValues<SkillCategory>();
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
        var newItem = new Skill();
        var dialogViewModel = new SkillEditDialogViewModel(newItem, Loc["SkillCreateTitle"], _warbandPicker, _warbandArchetypes);
        if (await ShowDialogAsync(new SkillEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSkillAsync(newItem, LocalizationService.Instance.Language);
        _allItems.Add(newItem);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Skill
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath,
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds)
        };

        var dialogViewModel = new SkillEditDialogViewModel(copy, Loc["SkillEditTitle"], _warbandPicker, _warbandArchetypes);
        if (await ShowDialogAsync(new SkillEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSkillAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteSkillAsync(row.Item.Id);
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
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Skill>());
}
