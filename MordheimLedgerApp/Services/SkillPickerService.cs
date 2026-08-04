using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Skills;

namespace MordheimLedgerApp.Services;

public interface ISkillPickerService
{
    Task<Skill?> PickSkillAsync();
}

public class SkillPickerService : ISkillPickerService
{
    private readonly IServiceProvider _provider;

    public SkillPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<Skill?> PickSkillAsync()
    {
        var tcs = new TaskCompletionSource<Skill?>();

        var navigationService = _provider.GetRequiredService<ISkillPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<SkillSelectorPage>();
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
