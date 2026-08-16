using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Animals;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IAnimalPickerService
{
    /// <summary>warbandArchetypeId : ne montre que les animaux communs (RestrictedToWarbandArchetypeIds
    /// vide) ou explicitement restreints à cette bande (ex. "Sanglier de guerre" - Orques only) - voir
    /// AnimalViewModel.AllowedWarbandArchetypeId, même principe qu'IEquipmentPickerService.
    /// PickEquipmentAsync.</summary>
    Task<IReadOnlyList<Animal>> PickAnimalsAsync(int warbandArchetypeId);
}

public class AnimalPickerService : IAnimalPickerService
{
    private readonly IServiceProvider _provider;

    public AnimalPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Animal>> PickAnimalsAsync(int warbandArchetypeId)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Animal>>();

        var navigationService = _provider.GetRequiredService<IAnimalPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<AnimalSelectorPage>()) pour pouvoir poser le
        // filtre AllowedWarbandArchetypeId sur le ViewModel avant que la page ne charge ses données -
        // même idiome qu'EquipmentPickerService.
        var viewModel = _provider.GetRequiredService<AnimalViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        var page = new AnimalSelectorPage(viewModel);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Animal>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "AnimalPicker.Push");

        return await tcs.Task;
    }
}
