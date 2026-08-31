using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IEquipmentPickerService
{
    /// <summary>warbandArchetypeId: only items whose RestrictedToWarbandArchetypeIds is empty (common)
    /// or contains this id are selectable - see WarriorEditDialogViewModel.AddEquipment. equipmentListId:
    /// non-null (recruit picker, the warrior's assigned EquipmentList) switches filtering to the union of
    /// that list and the band's Rare/Trading-Post items; null (EquipmentList editor's own "add item"
    /// picker) keeps the broad common+band browse. warriorArchetypeId: narrows any
    /// RestrictedToWarriorArchetypeIds-tagged item further, same idea as the Skill picker. availableGold:
    /// non-null shows a live "spent/remaining" line in the picker as items are selected (null hides it -
    /// the EquipmentList editor's own "add item" picker has no gold budget to track). unitCount: cost
    /// multiplier applied to the running total shown in that line - a henchman row's effectif for a
    /// grouped purchase (WarbandEditDialogViewModel.AddEquipment), 1 for an individual purchase; ignores
    /// any material-rule cost multiplier (Gromril...), only decided after the picker closes.
    /// alreadyHasFreeDagger: true if the target already carries a free dagger (EquipmentItem.
    /// IsFreeDagger) - the Dagger tile shows its normal price instead of "Gratuit"/"Free" when set.
    /// lockedCategory: non-null pre-filters the picker to this category and locks the category-change
    /// button (see EquipmentItemViewModel.LockedCategory) - used by WarriorEditDialogViewModel.
    /// SelectAnimal to reuse this same picker pre-filtered to EquipmentCategory.Animal instead of a
    /// separate Animal-only picker. singleSelect: true restricts the picker to at most one tile at a
    /// time (see EquipmentItemViewModel.SingleSelectMode) - used by the End of Game wizard's "Objets
    /// rares" step (EndOfGameDialogViewModel.RareItems.cs), where a Hero is nominating exactly ONE item
    /// to attempt a 2D6 search roll against ("You may also only make one roll for each Hero"), not doing
    /// a normal multi-item purchase. rareSearchMode: true alongside singleSelect for that same step -
    /// hides common ranged weapons/armour, which have no search path there (see EquipmentItemViewModel.
    /// RareSearchMode). allowedEquipmentListItemIds: non-null bypasses equipmentListId's single-list
    /// resolution and sets EquipmentItemViewModel.AllowedEquipmentListItemIds directly - used by the same
    /// "Objets rares" step to scope the browse to the UNION of every equipment list the warband's
    /// recruitable types use (a Hero may search for something meant for a different Hero, to stash it for
    /// the band - user request 2026-08-28), rather than one specific warrior's own list.</summary>
    Task<IReadOnlyList<EquipmentItem>> PickEquipmentAsync(int warbandArchetypeId, int? equipmentListId = null, int? warriorArchetypeId = null,
        int? availableGold = null, int unitCount = 1, bool alreadyHasFreeDagger = false, EquipmentCategory? lockedCategory = null, bool singleSelect = false,
        bool rareSearchMode = false, HashSet<int>? allowedEquipmentListItemIds = null);
}

public class EquipmentPickerService : IEquipmentPickerService
{
    private readonly IServiceProvider _provider;
    private readonly ILibraryService _libraryService;

    public EquipmentPickerService(IServiceProvider provider, ILibraryService libraryService)
    {
        _provider = provider;
        _libraryService = libraryService;
    }

    public async Task<IReadOnlyList<EquipmentItem>> PickEquipmentAsync(int warbandArchetypeId, int? equipmentListId = null, int? warriorArchetypeId = null,
        int? availableGold = null, int unitCount = 1, bool alreadyHasFreeDagger = false, EquipmentCategory? lockedCategory = null, bool singleSelect = false,
        bool rareSearchMode = false, HashSet<int>? allowedEquipmentListItemIds = null)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<EquipmentItem>>();

        var navigationService = _provider.GetRequiredService<IEquipmentPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<EquipmentItemSelectorPage>()) pour pouvoir poser
        // le filtre AllowedWarbandArchetypeId sur le ViewModel avant que la page ne charge ses données.
        var viewModel = _provider.GetRequiredService<EquipmentItemViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        viewModel.AllowedWarriorArchetypeId = warriorArchetypeId;
        viewModel.AllowedEquipmentListItemIds = allowedEquipmentListItemIds
            ?? (equipmentListId is { } id ? await _libraryService.GetEquipmentListItemIdsAsync(id) : null);
        viewModel.AvailableGold = availableGold;
        viewModel.UnitCount = unitCount;
        viewModel.AlreadyHasFreeDagger = alreadyHasFreeDagger;
        viewModel.LockedCategory = lockedCategory;
        viewModel.SingleSelectMode = singleSelect;
        viewModel.RareSearchMode = rareSearchMode;
        // Poussée nue (pas de NavigationPage) - voir PickerSelectorLayout pour le pourquoi (un
        // NavigationPage déjà au sommet de la pile modale absorbait le push modal suivant, ex. une
        // dialog imbriquée depuis ce sélecteur, au lieu de l'empiler correctement).
        var page = new EquipmentItemSelectorPage(viewModel);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<EquipmentItem>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "EquipmentPicker.Push");

        return await tcs.Task;
    }
}
