using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Mutations;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IMutationPickerService
{
    /// <summary>warbandArchetypeId: only mutations whose RestrictedToWarbandArchetypeIds is empty
    /// (common) or contains this id are selectable - see WarriorEditDialogViewModel.AddMutation.</summary>
    Task<IReadOnlyList<Mutation>> PickMutationsAsync(int warbandArchetypeId);
}

public class MutationPickerService : IMutationPickerService
{
    private readonly IServiceProvider _provider;

    public MutationPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Mutation>> PickMutationsAsync(int warbandArchetypeId)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Mutation>>();

        var navigationService = _provider.GetRequiredService<IMutationPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<MutationSelectorPage>()) pour pouvoir poser le
        // filtre AllowedWarbandArchetypeId sur le ViewModel avant que la page ne charge ses données.
        var viewModel = _provider.GetRequiredService<MutationViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        var page = new MutationSelectorPage(viewModel);
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Mutation>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "MutationPicker.Push");

        return await tcs.Task;
    }
}
