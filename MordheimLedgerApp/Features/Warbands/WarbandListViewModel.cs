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
    private ObservableCollection<WarbandRowItem> warbands = new();

    // Pas de CollectionView.SelectedItem/SelectionMode natif (cf. WarbandListPage.xaml, même raison
    // que CategoryListPage de DmTools) : sur Android, le fond de sélection natif reste teinté par
    // colorAccent quel que soit le style posé dessus. SelectedWarband est donc géré à la main via un
    // TapGestureRecognizer par ligne plutôt que SelectedItem.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifySelectedWarband))]
    private WarbandRowItem? selectedWarband;

    public bool CanModifySelectedWarband => SelectedWarband is not null;

    public WarbandListViewModel(IWarbandService warbandService, ILibraryService libraryService)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
    }

    partial void OnSelectedWarbandChanged(WarbandRowItem? oldValue, WarbandRowItem? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void SelectWarband(WarbandRowItem item) => SelectedWarband = item;

    [RelayCommand]
    private async Task LoadWarbandsAsync()
    {
        await Loading.RunAsync(async () =>
        {
            var loaded = await _warbandService.GetWarbandsAsync();
            Warbands = new ObservableCollection<WarbandRowItem>(loaded.Select(w => new WarbandRowItem(w)));
            SelectedWarband = null;
        });
    }

    [RelayCommand]
    private async Task CreateWarbandAsync()
    {
        var archetypes = await _libraryService.GetWarbandArchetypesAsync();
        if (archetypes.Count == 0)
        {
            await ShowInfoAsync(Loc["WarbandsEmptyLibraryTitle"], Loc["WarbandsEmptyLibraryMessage"]);
            return;
        }

        var options = archetypes.Select(a => $"{a.Name} ({a.StartingTreasury}gc)").ToArray();
        var index = await ShowActionSheetIndexAsync(Loc["WarbandsChooseType"], options);
        if (index < 0) return;

        var name = await ShowPromptAsync(Loc["WarbandsNewTitle"], Loc["PromptName"]);
        if (string.IsNullOrWhiteSpace(name)) return;

        await Loading.RunAsync(async () =>
        {
            var warband = await _warbandService.CreateWarbandAsync(name, archetypes[index]);
            Warbands.Add(new WarbandRowItem(warband));
        });
    }

    [RelayCommand]
    private async Task OpenSelectedWarbandAsync()
    {
        if (SelectedWarband is null) return;
        await Shell.Current.GoToAsync($"{nameof(WarbandDetailPage)}?warbandId={SelectedWarband.Warband.Id}");
    }

    [RelayCommand]
    private async Task DeleteSelectedWarbandAsync()
    {
        if (SelectedWarband is null) return;
        if (!await ConfirmDeleteAsync(SelectedWarband.Name)) return;

        await Loading.RunAsync(async () =>
        {
            await _warbandService.DeleteWarbandAsync(SelectedWarband.Warband.Id);
            Warbands.Remove(SelectedWarband);
            SelectedWarband = null;
        });
    }
}

// Classe (pas record) : IsSelected doit être un ObservableProperty pilotable à la main pour la
// bordure de mise en évidence (cf. WarbandListViewModel.OnSelectedWarbandChanged et
// WarbandListPage.xaml), la sélection native du CollectionView étant évitée.
public partial class WarbandRowItem : ObservableObject
{
    public Warband Warband { get; }

    public string Name => Warband.Name;
    public int Treasury => Warband.Treasury;

    [ObservableProperty]
    private bool isSelected;

    public WarbandRowItem(Warband warband) => Warband = warband;
}
