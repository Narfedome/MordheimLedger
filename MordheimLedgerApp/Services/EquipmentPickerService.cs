using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.EquipmentItems;

namespace MordheimLedgerApp.Services;

public interface IEquipmentPickerService
{
    Task<EquipmentItem?> PickEquipmentAsync();
}

public class EquipmentPickerService : IEquipmentPickerService
{
    private readonly IServiceProvider _provider;

    public EquipmentPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<EquipmentItem?> PickEquipmentAsync()
    {
        var tcs = new TaskCompletionSource<EquipmentItem?>();

        var navigationService = _provider.GetRequiredService<IEquipmentPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<EquipmentItemSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(null);
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
