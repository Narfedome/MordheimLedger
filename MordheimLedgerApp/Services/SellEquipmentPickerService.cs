using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Features.Warbands.EndOfGame;

namespace MordheimLedgerApp.Services;

public interface ISellEquipmentPickerService
{
    /// <summary>candidates : lignes réellement vendables à cet instant précis (réserve + trouvailles
    /// d'Exploration + équipement porté, quantité déjà disponible calculée par l'appelant - voir
    /// EndOfGamePageViewModel.EquipmentTrading.cs's BuildSellableCandidates) - jamais interrogé ici via
    /// un service Library, contrairement aux autres pickers de cette famille (EquipmentPickerService...).</summary>
    Task<IReadOnlyList<SellableEquipmentCandidate>> PickSaleAsync(IEnumerable<SellableEquipmentCandidate> candidates);
}

public class SellEquipmentPickerService : ISellEquipmentPickerService
{
    private readonly IServiceProvider _provider;

    public SellEquipmentPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<SellableEquipmentCandidate>> PickSaleAsync(IEnumerable<SellableEquipmentCandidate> candidates)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<SellableEquipmentCandidate>>();

        var navigationService = _provider.GetRequiredService<ISellEquipmentPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        var viewModel = _provider.GetRequiredService<SellEquipmentSelectorViewModel>();
        viewModel.Initialize(candidates, navigationService);
        // Poussée nue (pas de NavigationPage) - voir PickerSelectorLayout pour le pourquoi (un
        // NavigationPage déjà au sommet de la pile modale absorbait le push modal suivant).
        var page = new SellEquipmentSelectorPage(viewModel);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<SellableEquipmentCandidate>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "SellEquipmentPicker.Push");

        return await tcs.Task;
    }
}
