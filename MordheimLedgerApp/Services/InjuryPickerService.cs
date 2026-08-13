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

        // Poussée nue (pas de NavigationPage) - voir PickerSelectorLayout pour le pourquoi.
        var page = _provider.GetRequiredService<InjurySelectorPage>();

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Injury>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "InjuryPicker.Push");

        return await tcs.Task;
    }
}
