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
    private ObservableCollection<WarriorArchetype> warriorArchetypeItems = new();

    [ObservableProperty]
    private WarriorArchetype? selectedWarriorArchetype;

    public WarriorArchetypeViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetWarriorArchetypesAsync(WarbandArchetypeId);
        WarriorArchetypeItems = new ObservableCollection<WarriorArchetype>(items);
    }

    [RelayCommand]
    private static async Task Back() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new WarriorArchetype { WarbandArchetypeId = WarbandArchetypeId };
        var dialogViewModel = new WarriorArchetypeEditDialogViewModel(newItem, Loc["WarriorArchetypeCreateTitle"]);
        if (await ShowDialogAsync(new WarriorArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarriorArchetypeAsync(newItem);
        WarriorArchetypeItems.Add(newItem);
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedWarriorArchetype is null) return;

        var s = SelectedWarriorArchetype;
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
        if (SelectedWarriorArchetype is null) return;
        if (!await ConfirmDeleteAsync(SelectedWarriorArchetype.Name)) return;

        await _libraryService.DeleteWarriorArchetypeAsync(SelectedWarriorArchetype.Id);
        WarriorArchetypeItems.Remove(SelectedWarriorArchetype);
        SelectedWarriorArchetype = null;
    }
}
