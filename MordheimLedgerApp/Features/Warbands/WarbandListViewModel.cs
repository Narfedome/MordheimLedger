using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListViewModel : BaseViewModel
{
    private readonly IWarbandService _warbandService;
    private readonly ILibraryService _libraryService;

    [ObservableProperty]
    private ObservableCollection<WarbandRow> rows = new();

    // Pas de CollectionView.SelectedItem/SelectionMode natif (même raison que CategoryListPage de
    // DmTools : sur Android, le fond de sélection natif reste teinté par colorAccent quel que soit le
    // style posé dessus) - IsSelected est porté par la ligne elle-même, cf. SelectionMarkerStyle.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifySelectedWarband))]
    private WarbandRow? selectedRow;

    public bool CanModifySelectedWarband => SelectedRow != null;

    public WarbandListViewModel(IWarbandService warbandService, ILibraryService libraryService)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
    }

    partial void OnSelectedRowChanged(WarbandRow? oldValue, WarbandRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private async Task LoadWarbandsAsync()
    {
        await Loading.RunAsync(async () =>
        {
            var warbands = await _warbandService.GetWarbandsAsync();
            Rows = new ObservableCollection<WarbandRow>(warbands.Select(w => new WarbandRow(w)));
            SelectedRow = null;
        });
    }

    [RelayCommand]
    private void Select(WarbandRow row) => SelectedRow = row;

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
            await _warbandService.CreateWarbandAsync(name, archetypes[index]);
            await LoadWarbandsAsync();
        });
    }

    // Sélection (corps de la ligne) et ouverture (zone dédiée "Jouer" en bout de ligne, cf.
    // WarbandListPage.xaml) restent deux gestes distincts, comme SceneTemplate/SelectCommand+
    // LaunchCommand dans CampaignPage de DmTools - pas de bouton "Ouvrir" dans la barre du bas.
    [RelayCommand]
    private async Task OpenWarbandAsync(WarbandRow row)
    {
        SelectedRow = row;
        await Shell.Current.GoToAsync($"{nameof(WarbandDetailPage)}?warbandId={row.Warband.Id}");
    }

    [RelayCommand]
    private async Task EditSelectedWarbandAsync()
    {
        if (SelectedRow is not { } row) return;

        var newName = await ShowPromptAsync(Loc["DialogRename"], Loc["PromptName"], initialValue: row.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == row.Name) return;

        row.Warband.Name = newName.Trim();
        await _warbandService.SaveWarbandAsync(row.Warband);
        await LoadWarbandsAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedWarbandAsync()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Name)) return;

        await Loading.RunAsync(async () =>
        {
            await _warbandService.DeleteWarbandAsync(row.Warband.Id);
            await LoadWarbandsAsync();
        });
    }
}
