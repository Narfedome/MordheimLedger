using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.DramatisPersonae;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IDramatisPersonaPickerService
{
    /// <summary>Sélection unique - c'est le seul mode utilisé pour l'instant (l'étape 7 du wizard de Fin
    /// de Partie, "un seul personnage recherché par Héros"), même idiome que HiredSwordPickerService.
    /// PickHiredSwordAsync.</summary>
    /// <param name="warbandArchetypeId">Narrowe aux Dramatis Personae éligibles à cette bande (voir
    /// DramatisPersona.RestrictedToWarbandArchetypeIds) - null (usage Codex) montre tout le catalogue.</param>
    Task<DramatisPersona?> PickDramatisPersonaAsync(int? warbandArchetypeId = null);
}

public class DramatisPersonaPickerService : IDramatisPersonaPickerService
{
    private readonly IServiceProvider _provider;

    public DramatisPersonaPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<DramatisPersona?> PickDramatisPersonaAsync(int? warbandArchetypeId = null)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<DramatisPersona>>();

        var navigationService = _provider.GetRequiredService<IDramatisPersonaPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<DramatisPersonaSelectorPage>()) pour pouvoir poser
        // le filtre sur le ViewModel avant que la page ne charge ses données - même idiome que
        // HiredSwordPickerService.PickAsync.
        var viewModel = _provider.GetRequiredService<DramatisPersonaViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        var page = new DramatisPersonaSelectorPage(viewModel);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<DramatisPersona>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "DramatisPersonaPicker.Push");

        var result = await tcs.Task;
        return result.FirstOrDefault();
    }
}
