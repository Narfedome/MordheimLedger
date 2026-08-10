using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Animals;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IAnimalPickerService
{
    Task<IReadOnlyList<Animal>> PickAnimalsAsync();
}

public class AnimalPickerService : IAnimalPickerService
{
    private readonly IServiceProvider _provider;

    public AnimalPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Animal>> PickAnimalsAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Animal>>();

        var navigationService = _provider.GetRequiredService<IAnimalPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<AnimalSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Animal>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "AnimalPicker.Push");

        return await tcs.Task;
    }
}
