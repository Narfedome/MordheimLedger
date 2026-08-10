using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IWarriorArchetypePickerService
{
    /// <summary>warbandArchetypeIds: only warriors belonging to one of these warbands are selectable -
    /// see SkillEditDialogViewModel.AddRestrictedWarrior, which scopes to whichever warband(s) the
    /// Skill is already restricted to.</summary>
    Task<IReadOnlyList<WarriorArchetype>> PickWarriorArchetypesAsync(IEnumerable<int> warbandArchetypeIds);
}

public class WarriorArchetypePickerService : IWarriorArchetypePickerService
{
    private readonly IServiceProvider _provider;

    public WarriorArchetypePickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<WarriorArchetype>> PickWarriorArchetypesAsync(IEnumerable<int> warbandArchetypeIds)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<WarriorArchetype>>();

        var navigationService = _provider.GetRequiredService<IWarriorArchetypePickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<WarriorArchetypeSelectorPage>()) pour pouvoir
        // poser le filtre AllowedWarbandArchetypeIds sur le ViewModel avant que la page ne charge ses
        // données - même trick que SkillPickerService.
        var viewModel = _provider.GetRequiredService<WarriorArchetypeSelectorViewModel>();
        viewModel.AllowedWarbandArchetypeIds = warbandArchetypeIds.ToList();
        var page = new WarriorArchetypeSelectorPage(viewModel);
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<WarriorArchetype>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "WarriorArchetypePicker.Push");

        return await tcs.Task;
    }
}
