using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Warbands;

public partial class WarbandListViewModel : BaseViewModel
{
    private readonly IWarbandArchetypePickerNavigationService _pickerNavigation;
    private readonly IWarbandService _warbandService;
    private readonly IWarbandArchetypePickerService _warbandArchetypePickerService;
    private readonly ILibraryService _libraryService;
    private readonly IEquipmentPickerService _equipmentPickerService;
    private readonly ISkillPickerService _skillPickerService;

    [ObservableProperty]
    private ObservableCollection<WarbandRow> rows = new();

    [ObservableProperty]
    private bool hasWarbands;

    // Reste false pendant le court instant entre l'affichage de la page et le vrai début du
    // chargement (Loading.IsLoading ne passe à true qu'à l'intérieur de LoadWarbandsAsync) : sans ce
    // garde-fou, HasWarbands vaudrait encore false par défaut durant cette fenêtre et l'état vide
    // (bouton "Créer") s'afficherait brièvement avant le spinner - cf. CampaignViewModel.IsInitialized
    // dans DmTools.
    [ObservableProperty]
    private bool isInitialized;

    // Pas de CollectionView.SelectedItem/SelectionMode natif (même raison que CategoryListPage de
    // DmTools : sur Android, le fond de sélection natif reste teinté par colorAccent quel que soit le
    // style posé dessus) - IsSelected est porté par la ligne elle-même, cf. SelectionMarkerStyle.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanModifySelectedWarband))]
    private WarbandRow? selectedRow;

    public bool CanModifySelectedWarband => SelectedRow != null;

    public WarbandListViewModel(IWarbandArchetypePickerNavigationService pickerNavigation,
        IWarbandService warbandService, IWarbandArchetypePickerService warbandArchetypePickerService,
        ILibraryService libraryService, IEquipmentPickerService equipmentPickerService,
        ISkillPickerService skillPickerService)
    {
        _pickerNavigation = pickerNavigation;
        _warbandService = warbandService;
        _warbandArchetypePickerService = warbandArchetypePickerService;
        _libraryService = libraryService;
        _equipmentPickerService = equipmentPickerService;
        _skillPickerService = skillPickerService;
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
            await InitializeAsync();
        });
    }

    public async Task InitializeAsync()
    {
        var warbands = await _warbandService.GetWarbandsAsync();
        var archetypeNames = await Task.WhenAll(warbands.Select(b => _warbandService.GetWarbandArchetypeNameAsync(b.WarbandArchetypeId, LocalizationService.Instance.Language)));
        var ratings = await Task.WhenAll(warbands.Select(b => _warbandService.GetWarbandRatingAsync(b.Id)));
        Rows = new ObservableCollection<WarbandRow>(warbands.Select((w, i) => new WarbandRow(w) { ArchetypeName = archetypeNames[i], Rating = ratings[i] }));
        SelectedRow = null;
        HasWarbands = warbands.Count > 0;
        IsInitialized = true;
    }


    [RelayCommand]
    private void Select(WarbandRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task CreateWarbandAsync()
    {
        // Valeurs de départ raisonnables plutôt que 0/null - purement indicatives, l'utilisateur les
        // ajuste ou les efface (MaxWarriors reste nullable, 10 n'est qu'un point de départ arbitraire).
        var newItem = new Core.Models.Warband();
        var dialogViewModel = new WarbandEditDialogViewModel(newItem, Loc["WarbandCreateTitle"],
             _warbandArchetypePickerService, _warbandService, _libraryService, _equipmentPickerService, _skillPickerService);
        if (await ShowDialogAsync(new WarbandEditDialog(dialogViewModel)) != true) return;

        await LoadWarbandsAsync();
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
