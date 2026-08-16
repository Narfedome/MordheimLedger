using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.SpecialRules;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface ISpecialRulePickerService
{
    /// <summary>Null (EquipmentItem callers, including Animal-category items) shows the full unfiltered
    /// catalog. Warband/Warrior
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

        // Poussée nue (pas enveloppée dans un NavigationPage) - voir SpecialRuleSelectorPage.xaml, qui a
        // désormais son propre en-tête (DetailPageHeaderView) au lieu de compter sur le Title/la barre
        // native du NavigationPage. Un NavigationPage déjà au sommet de la pile modale semble absorber le
        // push modal suivant au lieu de l'empiler correctement (observé : ModalStack ne bougeait pas au
        // push d'une dialog imbriquée depuis ce sélecteur, la nouvelle page héritant du chrome du
        // NavigationPage à la place - flèche retour intempestive sur Android, mauvaise page affichée sur
        // Windows) - test en cours sur ce seul picker avant généralisation aux ~9 autres XxxPickerService.
        var page = _provider.GetRequiredService<SpecialRuleSelectorPage>();

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<SpecialRule>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "SpecialRulePicker.Push");

        return await tcs.Task;
    }
}
