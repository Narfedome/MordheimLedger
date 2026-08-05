using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Mounts.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Mounts;

public partial class MountViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IMountPickerNavigationService _pickerNavigation;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private List<WarbandArchetype> _warbandArchetypes = new();

    [ObservableProperty]
    private ObservableCollection<MountRow> mounts = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private MountRow? selectedRow;

    /// <summary>Set by MountSelectorPage right after construction - même bascule multi-sélection
    /// qu'InjuryViewModel.IsSelectorMode (le picker ne renvoie en pratique qu'une monture : le premier
    /// résultat coché remplace la monture actuelle du guerrier, voir WarriorEditDialogViewModel.PickMount).</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<MountRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    public MountViewModel(ILibraryService libraryService, IMountPickerNavigationService pickerNavigation,
        IWarbandArchetypePickerService warbandPicker, ISpecialRulePickerService specialRulePicker)
    {
        _libraryService = libraryService;
        _pickerNavigation = pickerNavigation;
        _warbandPicker = warbandPicker;
        _specialRulePicker = specialRulePicker;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((MountViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetMountsAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        Mounts = new ObservableCollection<MountRow>(allItems.Select(i => new MountRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(MountRow? oldValue, MountRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(MountRow row)
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
        var newItem = new Mount();
        var dialogViewModel = new MountEditDialogViewModel(newItem, Loc["MountCreateTitle"], _warbandPicker, _warbandArchetypes, _specialRulePicker);
        if (await ShowDialogAsync(new MountEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMountAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Mount
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

        var dialogViewModel = new MountEditDialogViewModel(copy, Loc["MountEditTitle"], _warbandPicker, _warbandArchetypes, _specialRulePicker);
        if (await ShowDialogAsync(new MountEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveMountAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteMountAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Mount>());
}
