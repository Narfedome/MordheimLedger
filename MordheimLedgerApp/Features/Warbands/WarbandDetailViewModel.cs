using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Features.Warbands;

[QueryProperty(nameof(WarbandId), "warbandId")]
public partial class WarbandDetailViewModel : BaseViewModel
{
    private readonly IWarbandService _warbandService;
    private readonly ILibraryService _libraryService;

    private List<WarriorArchetype> _recruitableArchetypes = new();

    [ObservableProperty]
    private int warbandId;

    [ObservableProperty]
    private Warband? warband;

    [ObservableProperty]
    private ObservableCollection<Warrior> warriors = new();

    public WarbandDetailViewModel(IWarbandService warbandService, ILibraryService libraryService)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
    }

    partial void OnWarbandIdChanged(int value) => _ = LoadAsync(value);

    private async Task LoadAsync(int id)
    {
        await Loading.RunAsync(async () =>
        {
            Warband = await _warbandService.GetWarbandAsync(id);
            if (Warband is null) return;

            _recruitableArchetypes = await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId);

            var loaded = await _warbandService.GetWarriorsAsync(id);
            Warriors = new ObservableCollection<Warrior>(loaded);
        });
    }

    [RelayCommand]
    private static async Task BackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task RecruitWarriorAsync()
    {
        if (Warband is null) return;
        if (_recruitableArchetypes.Count == 0)
        {
            await ShowInfoAsync("Empty library", "This warband type has no recruitable warrior yet.");
            return;
        }

        var options = _recruitableArchetypes.Select(a => $"{a.Name} ({a.Cost}gc)").ToArray();
        var index = await ShowActionSheetIndexAsync("Recruit a warrior", options);
        if (index < 0) return;

        var name = await ShowPromptAsync("Recruit", "Warrior's name", "Name");
        if (string.IsNullOrWhiteSpace(name)) return;

        await Loading.RunAsync(async () =>
        {
            var warrior = await _warbandService.RecruitWarriorAsync(Warband.Id, _recruitableArchetypes[index], name);
            Warriors.Add(warrior);
        });
    }
}
