using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes;

[QueryProperty(nameof(WarbandArchetypeId), "WarbandArchetypeId")]
[QueryProperty(nameof(WarbandArchetypeName), "WarbandArchetypeName")]
public partial class WarriorArchetypeViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly ISpecialRulePickerService _specialRulePicker;

    [ObservableProperty]
    private int warbandArchetypeId;

    [ObservableProperty]
    private string warbandArchetypeName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WarriorArchetypeRow> warriorArchetypeItems = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private WarriorArchetypeRow? selectedRow;

    public WarriorArchetypeViewModel(ILibraryService libraryService, ISpecialRulePickerService specialRulePicker)
    {
        _libraryService = libraryService;
        _specialRulePicker = specialRulePicker;

        // Voir WarbandArchetypeViewModel - même besoin de rechargement explicite sur changement de
        // langue (cette page est atteinte par push depuis la Bibliothèque, mais rien n'empêche l'appli
        // d'y être déjà quand la langue change).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((WarriorArchetypeViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetWarriorArchetypesAsync(WarbandArchetypeId, LocalizationService.Instance.Language);
        WarriorArchetypeItems = new ObservableCollection<WarriorArchetypeRow>(items.Select(i => new WarriorArchetypeRow(i)));
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(WarriorArchetypeRow? oldValue, WarriorArchetypeRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private static async Task Back() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void Select(WarriorArchetypeRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new WarriorArchetype { WarbandArchetypeId = WarbandArchetypeId };
        var dialogViewModel = new WarriorArchetypeEditDialogViewModel(newItem, Loc["WarriorArchetypeCreateTitle"], _specialRulePicker);
        if (await ShowDialogAsync(new WarriorArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarriorArchetypeAsync(newItem, LocalizationService.Instance.Language);
        WarriorArchetypeItems.Add(new WarriorArchetypeRow(newItem));
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new WarriorArchetype
        {
            Id = s.Id,
            WarbandArchetypeId = s.WarbandArchetypeId,
            Name = s.Name,
            IsHero = s.IsHero,
            Cost = s.Cost,
            Source = s.Source,
            MaxCount = s.MaxCount,
            Movement = s.Movement,
            WeaponSkill = s.WeaponSkill,
            BallisticSkill = s.BallisticSkill,
            Strength = s.Strength,
            Toughness = s.Toughness,
            Wounds = s.Wounds,
            Initiative = s.Initiative,
            Attacks = s.Attacks,
            Leadership = s.Leadership,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            ImagePath = s.ImagePath,
            SpecialRules = new List<SpecialRule>(s.SpecialRules),
            SpellListName = s.SpellListName,
            CanBuyMutations = s.CanBuyMutations
        };

        var dialogViewModel = new WarriorArchetypeEditDialogViewModel(copy, Loc["WarriorArchetypeEditTitle"], _specialRulePicker);
        if (await ShowDialogAsync(new WarriorArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarriorArchetypeAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteWarriorArchetypeAsync(row.Item.Id);
        WarriorArchetypeItems.Remove(row);
        SelectedRow = null;
    }
}
