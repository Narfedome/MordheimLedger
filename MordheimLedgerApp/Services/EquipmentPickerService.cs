using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.EquipmentItems;

namespace MordheimLedgerApp.Services;

public interface IEquipmentPickerService
{
    /// <summary>warbandArchetypeId: only items whose RestrictedToWarbandArchetypeIds is empty (common)
    /// or contains this id are selectable - see WarriorEditDialogViewModel.AddEquipment.</summary>
    Task<IReadOnlyList<EquipmentItem>> PickEquipmentAsync(int warbandArchetypeId);
}

public class EquipmentPickerService : IEquipmentPickerService
{
    private readonly IServiceProvider _provider;

    public EquipmentPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<EquipmentItem>> PickEquipmentAsync(int warbandArchetypeId)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<EquipmentItem>>();

        var navigationService = _provider.GetRequiredService<IEquipmentPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<EquipmentItemSelectorPage>()) pour pouvoir poser
        // le filtre AllowedWarbandArchetypeId sur le ViewModel avant que la page ne charge ses données.
        var viewModel = _provider.GetRequiredService<EquipmentItemViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
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

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
