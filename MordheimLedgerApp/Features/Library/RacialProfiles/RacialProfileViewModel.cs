using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.RacialProfiles.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.RacialProfiles;

/// <summary>Catalogue RacialProfile (maximums raciaux par type de créature) - même motif que
/// RaceViewModel : pas de sous-onglet, pas de picker multi-sélection (l'assignation à un
/// WarriorArchetype se fait via un Picker directement sur WarriorArchetypeEditDialog).</summary>
public partial class RacialProfileViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;

    [ObservableProperty]
    private ObservableCollection<RacialProfileRow> racialProfiles = new();

    [ObservableProperty]
    private RacialProfileRow? selectedRow;

    public RacialProfileViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;

        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((RacialProfileViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetRacialProfilesAsync(LocalizationService.Instance.Language);
        RacialProfiles = new ObservableCollection<RacialProfileRow>(allItems.Select(i => new RacialProfileRow(i)));
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(RacialProfileRow? oldValue, RacialProfileRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(RacialProfileRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new RacialProfile();
        var dialogViewModel = new RacialProfileEditDialogViewModel(newItem, Loc["RacialProfileCreateTitle"], _libraryService);
        if (await ShowDialogAsync(new RacialProfileEditDialog(dialogViewModel)) != true) return;

        await LoadData();
    }

    /// <summary>Read-only recap (tile info button) - contrairement à RaceViewModel.ShowDetails
    /// (ChipDetailDialog, Nom+Description seuls), ce catalogue porte des données substantielles (les 9
    /// maximums) : dialog récap dédié qui reprend le layout de l'Edit en lecture seule, même motif que
    /// WarriorArchetypeDetailDialog.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ShowDetails(RacialProfileRow row) =>
        await ShowDialogAsync(new RacialProfileDetailDialog(new RacialProfileDetailDialogViewModel(row.Item)));

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var r = row.Item;
        var copy = new RacialProfile
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            NameKey = r.NameKey,
            DescriptionKey = r.DescriptionKey,
            Source = r.Source,
            Movement = r.Movement,
            MovementOverride = r.MovementOverride,
            WeaponSkill = r.WeaponSkill,
            BallisticSkill = r.BallisticSkill,
            Strength = r.Strength,
            Toughness = r.Toughness,
            Wounds = r.Wounds,
            Initiative = r.Initiative,
            Attacks = r.Attacks,
            Leadership = r.Leadership
        };

        var dialogViewModel = new RacialProfileEditDialogViewModel(copy, Loc["RacialProfileEditTitle"], _libraryService);
        if (await ShowDialogAsync(new RacialProfileEditDialog(dialogViewModel)) != true) return;

        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteRacialProfileAsync(row.Item.Id);
        await LoadData();
    }

    [RelayCommand]
    private static async Task Back() => await Shell.Current.GoToAsync("..");
}
