using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.SpecialRules;

namespace MordheimLedgerApp.Services;

public interface ISpecialRulePickerService
{
    /// <summary>Null (Animal/EquipmentItem callers) shows the full unfiltered catalog, unchanged from
    /// before Scope existed. Warband/Warrior restrict the selector to rules actually meant to be
    /// added at that level - see SpecialRuleScope.</summary>
    Task<IReadOnlyList<SpecialRule>> PickSpecialRulesAsync(SpecialRuleScope? scope = null);
}

public class SpecialRulePickerService : ISpecialRulePickerService
{
    private readonly IServiceProvider _provider;

    public SpecialRulePickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<SpecialRule>> PickSpecialRulesAsync(SpecialRuleScope? scope = null)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<SpecialRule>>();

        var navigationService = _provider.GetRequiredService<ISpecialRulePickerNavigationService>();
        navigationService.RequestedScope = scope;
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
