using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListViewModel : BaseViewModel
{
    private readonly IWarbandService _warbandService;
    private readonly ILibraryService _libraryService;

    [ObservableProperty]
    private ObservableCollection<Warband> warbands = new();

    public WarbandListViewModel(IWarbandService warbandService, ILibraryService libraryService)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
    }

    [RelayCommand]
    private async Task LoadWarbandsAsync()
    {
        await Loading.RunAsync(async () =>
        {
            var loaded = await _warbandService.GetWarbandsAsync();
            Warbands = new ObservableCollection<Warband>(loaded);
        });
    }

    [RelayCommand]
    private async Task CreateWarbandAsync()
    {
        var archetypes = await _libraryService.GetWarbandArchetypesAsync();
        if (archetypes.Count == 0)
        {
            await ShowInfoAsync("Empty library", "No warband type is available yet.");
            return;
        }

        var options = archetypes.Select(a => $"{a.Name} ({a.StartingTreasury}gc)").ToArray();
        var index = await ShowActionSheetIndexAsync("Choose a warband type", options);
        if (index < 0) return;

        var name = await ShowPromptAsync("New warband", "Warband name", "Name");
        if (string.IsNullOrWhiteSpace(name)) return;

        await Loading.RunAsync(async () =>
        {
            var warband = await _warbandService.CreateWarbandAsync(name, archetypes[index]);
            Warbands.Add(warband);
        });
    }

    [RelayCommand]
    private async Task OpenWarbandAsync(Warband warband)
    {
        await Shell.Current.GoToAsync($"{nameof(WarbandDetailPage)}?warbandId={warband.Id}");
    }
}
