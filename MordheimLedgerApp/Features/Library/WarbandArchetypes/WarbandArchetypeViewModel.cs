using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;

    [ObservableProperty]
    private ObservableCollection<WarbandArchetype> warbandArchetypeItems = new();

    [ObservableProperty]
    private WarbandArchetype? selectedWarbandArchetype;

    public WarbandArchetypeViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetWarbandArchetypesAsync();
        WarbandArchetypeItems = new ObservableCollection<WarbandArchetype>(items);
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new WarbandArchetype();
        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(newItem, Loc["WarbandArchetypeCreateTitle"]);
        if (await ShowDialogAsync(new WarbandArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarbandArchetypeAsync(newItem);
        WarbandArchetypeItems.Add(newItem);
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedWarbandArchetype is null) return;

        var copy = new WarbandArchetype
        {
            Id = SelectedWarbandArchetype.Id,
            Name = SelectedWarbandArchetype.Name,
            Source = SelectedWarbandArchetype.Source,
            StartingTreasury = SelectedWarbandArchetype.StartingTreasury,
            MaxWarriors = SelectedWarbandArchetype.MaxWarriors,
            Description = SelectedWarbandArchetype.Description,
            ImagePath = SelectedWarbandArchetype.ImagePath
        };

        var dialogViewModel = new WarbandArchetypeEditDialogViewModel(copy, Loc["WarbandArchetypeEditTitle"]);
        if (await ShowDialogAsync(new WarbandArchetypeEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveWarbandArchetypeAsync(copy);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedWarbandArchetype is null) return;
        if (!await ConfirmDeleteAsync(SelectedWarbandArchetype.Name)) return;

        await _libraryService.DeleteWarbandArchetypeAsync(SelectedWarbandArchetype.Id);
        WarbandArchetypeItems.Remove(SelectedWarbandArchetype);
        SelectedWarbandArchetype = null;
    }

    [RelayCommand]
    private async Task ManageWarriors()
    {
        if (SelectedWarbandArchetype is null) return;

        await Shell.Current.GoToAsync(nameof(WarriorArchetypeListPage),
            new Dictionary<string, object>
            {
                { "WarbandArchetypeId", SelectedWarbandArchetype.Id },
                { "WarbandArchetypeName", SelectedWarbandArchetype.Name }
            });
    }
}
