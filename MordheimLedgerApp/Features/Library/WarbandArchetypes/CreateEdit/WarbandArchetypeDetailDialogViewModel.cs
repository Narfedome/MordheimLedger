using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

/// <summary>Read-only recap of WarbandArchetypeEditDialog. 3 onglets (Général/Guerriers/Équipement)
/// plutôt qu'un long scroll empilé - même mécanisme toggle que WarriorEditDialog (SelectedTab +
/// IsXTab dérivés), pas un vrai TabbedPage. Règles spéciales/Écoles de magie restent dans Général
/// (pas d'onglet dédié) sur demande explicite de l'utilisateur.
///
/// Guerriers/Équipement sont chargés en différé, au premier passage sur leur onglet (pas à
/// l'ouverture du dialog) - le dialog s'ouvre donc instantanément sur Général, qui n'a besoin
/// d'aucun appel service (tout vient de l'Item déjà chargé sur la tuile). Chaque onglet ne charge
/// qu'une fois (_warriorsLoaded/_equipmentListsLoaded), pas à chaque re-sélection.
///
/// Les deux onglets n'affichent que des chips Nom (voir ChipListView) - Warriors/EquipmentLists ne
/// chargent donc que Id+Name (GetWarriorArchetypeNamesAsync/GetEquipmentListNamesAsync), pas les
/// WarriorArchetype/EquipmentList complets (SpecialRules, ItemIds...) que personne ne regarde tant que
/// le chip n'est pas tapé. Le détail complet n'est fetché qu'à la demande, un seul élément à la fois,
/// dans ShowWarriorDetail/ShowEquipmentListDetail.</summary>
public partial class WarbandArchetypeDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public WarbandArchetype Item { get; }
    public string GradeLabel { get; }
    public string MaxWarriorsDisplay { get; }

    [ObservableProperty]
    private List<NamedRef> warriors = new();

    [ObservableProperty]
    private List<NamedRef> equipmentLists = new();

    private readonly ILibraryService _libraryService;
    private bool _warriorsLoaded;
    private bool _equipmentListsLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsWarriorsTab))]
    [NotifyPropertyChangedFor(nameof(IsEquipmentTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsWarriorsTab => SelectedTab == 1;
    public bool IsEquipmentTab => SelectedTab == 2;

    public WarbandArchetypeDetailDialogViewModel(WarbandArchetype item, ILibraryService libraryService)
    {
        Item = item;
        Title = item.Name;
        GradeLabel = Loc[$"WarbandGrade{item.Grade}"];
        MaxWarriorsDisplay = item.MaxWarriors?.ToString() ?? "—";
        _libraryService = libraryService;
    }

    [RelayCommand]
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private async Task ShowWarriorsTab()
    {
        SelectedTab = 1;
        await EnsureWarriorsLoadedAsync();
    }

    [RelayCommand]
    private async Task ShowEquipmentTab()
    {
        SelectedTab = 2;
        await EnsureEquipmentListsLoadedAsync();
    }

    private Task EnsureWarriorsLoadedAsync()
    {
        if (_warriorsLoaded) return Task.CompletedTask;
        return Loading.RunAsync(async () =>
        {
            // Task.Run : le mapping DB->modèle (TranslationResolver.ResolveAsync et consorts) fait un
            // vrai travail CPU synchrone une fois la lecture SQLite revenue - sans ça, ce travail
            // tournait sur le thread UI et l'empêchait de peindre/animer l'ActivityIndicator pendant ce
            // temps (sensation de freeze signalée par l'utilisateur, indicateur qui n'apparaît pas au
            // bon moment).
            var language = LocalizationService.Instance.Language;
            Warriors = await Task.Run(() => _libraryService.GetWarriorArchetypeNamesAsync(Item.Id, language));
            _warriorsLoaded = true;
        });
    }

    private Task EnsureEquipmentListsLoadedAsync()
    {
        if (_equipmentListsLoaded) return Task.CompletedTask;
        return Loading.RunAsync(async () =>
        {
            var language = LocalizationService.Instance.Language;
            EquipmentLists = await Task.Run(() => _libraryService.GetEquipmentListNamesAsync(Item.Id, language));
            _equipmentListsLoaded = true;
        });
    }

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private Task ShowMagicSchoolDetail(MagicSchool school) => ShowChipDetailAsync(school.Name, school.Description);

    [RelayCommand]
    private async Task ShowWarriorDetail(NamedRef warrior)
    {
        var language = LocalizationService.Instance.Language;
        WarriorArchetype? fullWarrior = null;
        await Loading.RunAsync(async () =>
        {
            // EquipmentListDisplay a besoin des listes de la bande pour résoudre le nom - garanti chargé
            // même si l'utilisateur n'est jamais passé par l'onglet Équipement.
            await EnsureEquipmentListsLoadedAsync();
            fullWarrior = await Task.Run(() => _libraryService.GetWarriorArchetypeAsync(warrior.Id, language));
        });
        if (fullWarrior is null) return;

        await ShowDialogAsync(new WarriorArchetypeDetailDialog(new WarriorArchetypeDetailDialogViewModel(fullWarrior, EquipmentLists)));
    }

    [RelayCommand]
    private async Task ShowEquipmentListDetail(NamedRef list)
    {
        var language = LocalizationService.Instance.Language;
        List<EquipmentItem> items = null!;
        await Loading.RunAsync(async () =>
        {
            var itemIds = await _libraryService.GetEquipmentListItemIdsAsync(list.Id);
            items = await Task.Run(() => _libraryService.GetEquipmentItemsAsync(itemIds, language));
        });

        var equipmentList = new EquipmentList { Id = list.Id, Name = list.Name, WarbandArchetypeId = Item.Id };
        await ShowDialogAsync(new EquipmentListDetailDialog(
            new EquipmentListDetailDialogViewModel(equipmentList, items, _libraryService)));
    }
}
