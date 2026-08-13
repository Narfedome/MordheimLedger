using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.MagicSchools;
using MordheimLedgerApp.Components.Dialogs;

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

        // Poussée nue (pas de NavigationPage) - voir PickerSelectorLayout pour le pourquoi.
        var page = _provider.GetRequiredService<MagicSchoolSelectorPage>();

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<MagicSchool>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "MagicSchoolPicker.Push");

        return await tcs.Task;
    }
}
