using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.SpecialRules;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface ISpecialRulePickerService
{
    /// <summary>Null (Animal/EquipmentItem callers) shows the full unfiltered catalog. Warband/Warrior
    /// restrict the selector to rules actually attached at that level - see SpecialRuleFilterKind.</summary>
    Task<IReadOnlyList<SpecialRule>> PickSpecialRulesAsync(SpecialRuleFilterKind? filterKind = null);
}

public class SpecialRulePickerService : ISpecialRulePickerService
{
    private readonly IServiceProvider _provider;

    public SpecialRulePickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<SpecialRule>> PickSpecialRulesAsync(SpecialRuleFilterKind? filterKind = null)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<SpecialRule>>();

        var navigationService = _provider.GetRequiredService<ISpecialRulePickerNavigationService>();
        navigationService.RequestedFilterKind = filterKind;
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

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "SpecialRulePicker.Push");

        return await tcs.Task;
    }
}
