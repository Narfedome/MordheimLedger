using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

public partial class EquipmentItemViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IEquipmentPickerNavigationService _pickerNavigation;
    private List<EquipmentItem> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<EquipmentItemRow> equipmentItems = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private EquipmentItemRow? selectedRow;

    /// <summary>Le vrai critère de filtre - null = Tout. Distinct de SelectedCategoryLabel (juste
    /// l'affichage) : comparer des libellés résolus par LocalizationService cassait le filtre au
    /// changement de langue (le libellé "Tout" figé dans l'ancienne langue ne correspondait plus à
    /// rien après un rechargement en LoadData, filtrant silencieusement la liste à vide).</summary>
    [ObservableProperty]
    private EquipmentCategory? selectedCategory;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Set by EquipmentItemSelectorPage right after construction: en mode picker, un tap
    /// bascule la sélection d'une ligne (liseré bleu, plusieurs lignes à la fois) au lieu du
    /// remplacement à une seule ligne utilisé par l'onglet Library (IsCrud) pour Éditer/Supprimer.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par ApplyFilter.</summary>
    public ObservableCollection<EquipmentItemRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public EquipmentItemViewModel(ILibraryService libraryService, IEquipmentPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;
        selectedCategoryLabel = Loc["LibFilterAll"];

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((EquipmentItemViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);
        RefreshSelectedCategoryLabel();
        ApplyFilter();
    }

    /// <summary>Recompute le libellé affiché depuis SelectedCategory (le vrai critère) - à refaire à
    /// chaque LoadData puisque le texte dépend de la langue courante.</summary>
    private void RefreshSelectedCategoryLabel() =>
        SelectedCategoryLabel = SelectedCategory is { } category ? CategoryLabel(category) : Loc["LibFilterAll"];

    private void ApplyFilter()
    {
        var filtered = SelectedCategory is { } category
            ? _allItems.Where(i => i.Category == category).ToList()
            : _allItems;

        EquipmentItems = new ObservableCollection<EquipmentItemRow>(filtered.Select(i => new EquipmentItemRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    private string CategoryLabel(EquipmentCategory category) => Loc[$"EquipmentCategory{category}"];

    partial void OnSelectedRowChanged(EquipmentItemRow? oldValue, EquipmentItemRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(EquipmentItemRow row)
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
        var categories = Enum.GetValues<EquipmentCategory>();
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
        var newItem = new EquipmentItem();
        var dialogViewModel = new EquipmentItemEditDialogViewModel(newItem, Loc["EquipmentItemCreateTitle"]);
        if (await ShowDialogAsync(new EquipmentItemEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentItemAsync(newItem, LocalizationService.Instance.Language);
        _allItems.Add(newItem);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new EquipmentItem
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Cost = s.Cost,
            Rarity = s.Rarity,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new EquipmentItemEditDialogViewModel(copy, Loc["EquipmentItemEditTitle"]);
        if (await ShowDialogAsync(new EquipmentItemEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentItemAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteEquipmentItemAsync(row.Item.Id);
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
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<EquipmentItem>());
}
