using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes;

[QueryProperty(nameof(WarbandArchetypeId), "WarbandArchetypeId")]
[QueryProperty(nameof(WarbandArchetypeName), "WarbandArchetypeName")]
public partial class WarriorArchetypeViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;

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

    public WarriorArchetypeViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetWarriorArchetypesAsync(WarbandArchetypeId);
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
        var dialogViewModel = new WarriorArchetypeEditDialogViewModel(newItem, Loc["WarriorArchetypeCreateTitle"]);
        if (await ShowDialogAsync(new WarriorArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarriorArchetypeAsync(newItem);
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
            ImagePath = s.ImagePath
        };

        var dialogViewModel = new WarriorArchetypeEditDialogViewModel(copy, Loc["WarriorArchetypeEditTitle"]);
        if (await ShowDialogAsync(new WarriorArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarriorArchetypeAsync(copy);
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
