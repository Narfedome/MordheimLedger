using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Races.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Races;

/// <summary>Catalogue Race (Humain, Skaven, Orque...) - reprend le motif de MagicSchoolViewModel, en
/// plus simple : pas de picker multi-sélection (une bande n'a qu'UNE race, assignée via un Picker
/// directement sur WarbandArchetypeEditDialog, pas un sous-catalogue à sélectionner ici), pas de sous-
/// onglet (contrairement aux Sorts d'une école, une race ne porte aucune sous-collection).</summary>
public partial class RaceViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;

    [ObservableProperty]
    private ObservableCollection<RaceRow> races = new();

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle/MagicSchoolViewModel.
    [ObservableProperty]
    private RaceRow? selectedRow;

    public RaceViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((RaceViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var allItems = await _libraryService.GetRacesAsync(LocalizationService.Instance.Language);
        Races = new ObservableCollection<RaceRow>(allItems.Select(i => new RaceRow(i)));
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(RaceRow? oldValue, RaceRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(RaceRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new Race();
        var dialogViewModel = new RaceEditDialogViewModel(newItem, Loc["RaceCreateTitle"], _libraryService);
        if (await ShowDialogAsync(new RaceEditDialog(dialogViewModel)) != true) return;

        await LoadData();
    }

    /// <summary>Read-only recap (tile info button) - réutilise le ChipDetailDialog générique
    /// (Nom+Description) plutôt qu'un RaceDetailDialog dédié, même mécanisme que MagicSchoolViewModel.
    /// ShowDetails (une race n'a aucun attribut propre à afficher au-delà de son texte).
    /// AllowConcurrentExecutions : une seule commande partagée par toutes les lignes, sinon elles se
    /// désactivent toutes ensemble tant qu'un dialog est ouvert.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ShowDetails(RaceRow row) =>
        await ShowDialogAsync(new ChipDetailDialog(new ChipDetailDialogViewModel(row.Item.Name, row.Item.Description)));

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var r = row.Item;
        var copy = new Race
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            NameKey = r.NameKey,
            DescriptionKey = r.DescriptionKey,
            Source = r.Source
        };

        var dialogViewModel = new RaceEditDialogViewModel(copy, Loc["RaceEditTitle"], _libraryService);
        if (await ShowDialogAsync(new RaceEditDialog(dialogViewModel)) != true) return;

        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteRaceAsync(row.Item.Id);
        await LoadData();
    }

    /// <summary>Only bound when this ViewModel backs the standalone RaceListPage (reached from the
    /// Bandes tab's "Gérer les Races" button) - même idiome que MagicSchoolViewModel.Back.</summary>
    [RelayCommand]
    private static async Task Back() => await Shell.Current.GoToAsync("..");
}
