using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.MagicSchools;

namespace MordheimLedgerApp.Services;

public interface IMagicSchoolPickerService
{
    Task<IReadOnlyList<MagicSchool>> PickMagicSchoolsAsync();
}

public class MagicSchoolPickerService : IMagicSchoolPickerService
{
    private readonly IServiceProvider _provider;

    public MagicSchoolPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<MagicSchool>> PickMagicSchoolsAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<MagicSchool>>();

        var navigationService = _provider.GetRequiredService<IMagicSchoolPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<MagicSchoolSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<MagicSchool>());
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
