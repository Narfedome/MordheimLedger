using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Mounts;

namespace MordheimLedgerApp.Services;

public interface IMountPickerService
{
    Task<IReadOnlyList<Mount>> PickMountsAsync();
}

public class MountPickerService : IMountPickerService
{
    private readonly IServiceProvider _provider;

    public MountPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Mount>> PickMountsAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Mount>>();

        var navigationService = _provider.GetRequiredService<IMountPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<MountSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Mount>());
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
