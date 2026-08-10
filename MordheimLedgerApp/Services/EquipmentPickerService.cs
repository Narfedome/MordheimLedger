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
    /// RestrictedToWarriorArchetypeIds-tagged item further, same idea as the Skill picker.</summary>
    Task<IReadOnlyList<EquipmentItem>> PickEquipmentAsync(int warbandArchetypeId, int? equipmentListId = null, int? warriorArchetypeId = null);
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

    public async Task<IReadOnlyList<EquipmentItem>> PickEquipmentAsync(int warbandArchetypeId, int? equipmentListId = null, int? warriorArchetypeId = null)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<EquipmentItem>>();

        var navigationService = _provider.GetRequiredService<IEquipmentPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<EquipmentItemSelectorPage>()) pour pouvoir poser
        // le filtre AllowedWarbandArchetypeId sur le ViewModel avant que la page ne charge ses données.
        var viewModel = _provider.GetRequiredService<EquipmentItemViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        viewModel.AllowedWarriorArchetypeId = warriorArchetypeId;
        viewModel.AllowedEquipmentListItemIds = equipmentListId is { } id ? await _libraryService.GetEquipmentListItemIdsAsync(id) : null;
        var page = new EquipmentItemSelectorPage(viewModel);
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<EquipmentItem>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "EquipmentPicker.Push");

        return await tcs.Task;
    }
}
