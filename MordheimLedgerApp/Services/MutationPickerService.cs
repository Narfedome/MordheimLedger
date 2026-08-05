using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Mutations;

namespace MordheimLedgerApp.Services;

public interface IMutationPickerService
{
    Task<IReadOnlyList<Mutation>> PickMutationsAsync();
}

public class MutationPickerService : IMutationPickerService
{
    private readonly IServiceProvider _provider;

    public MutationPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Mutation>> PickMutationsAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Mutation>>();

        var navigationService = _provider.GetRequiredService<IMutationPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<MutationSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Mutation>());
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
