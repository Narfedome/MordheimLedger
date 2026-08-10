using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IWarbandArchetypePickerService
{
    Task<IReadOnlyList<WarbandArchetype>> PickWarbandArchetypesAsync();
}

public class WarbandArchetypePickerService : IWarbandArchetypePickerService
{
    private readonly IServiceProvider _provider;

    public WarbandArchetypePickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<WarbandArchetype>> PickWarbandArchetypesAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<WarbandArchetype>>();

        var navigationService = _provider.GetRequiredService<IWarbandArchetypePickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<WarbandArchetypeSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<WarbandArchetype>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "WarbandArchetypePicker.Push");

        return await tcs.Task;
    }
}
