using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.SpecialRules;

namespace MordheimLedgerApp.Services;

public interface ISpecialRulePickerService
{
    Task<IReadOnlyList<SpecialRule>> PickSpecialRulesAsync();
}

public class SpecialRulePickerService : ISpecialRulePickerService
{
    private readonly IServiceProvider _provider;

    public SpecialRulePickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<SpecialRule>> PickSpecialRulesAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<SpecialRule>>();

        var navigationService = _provider.GetRequiredService<ISpecialRulePickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var page = _provider.GetRequiredService<SpecialRuleSelectorPage>();
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<SpecialRule>());
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
