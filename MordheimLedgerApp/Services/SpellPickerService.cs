using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Spells;

namespace MordheimLedgerApp.Services;

public interface ISpellPickerService
{
    Task<IReadOnlyList<Spell>> PickSpellsAsync();
}

public class SpellPickerService : ISpellPickerService
{
    private readonly IServiceProvider _provider;

    public SpellPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Spell>> PickSpellsAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Spell>>();

        var navigationService = _provider.GetRequiredService<ISpellPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<SpellSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Spell>());
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
