using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Injuries;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IInjuryPickerService
{
    Task<IReadOnlyList<Injury>> PickInjuriesAsync();
}

public class InjuryPickerService : IInjuryPickerService
{
    private readonly IServiceProvider _provider;

    public InjuryPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Injury>> PickInjuriesAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Injury>>();

        var navigationService = _provider.GetRequiredService<IInjuryPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<InjurySelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Injury>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "InjuryPicker.Push");

        return await tcs.Task;
    }
}
