using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Skills;

namespace MordheimLedgerApp.Services;

public interface ISkillPickerService
{
    /// <summary>warbandArchetypeId: only skills whose RestrictedToWarbandArchetypeIds is empty (common)
    /// or contains this id are selectable - see WarriorEditDialogViewModel.AddSkill/
    /// EndOfGameDialogViewModel's Advance roll skill choice.</summary>
    Task<IReadOnlyList<Skill>> PickSkillAsync(int warbandArchetypeId);
}

public class SkillPickerService : ISkillPickerService
{
    private readonly IServiceProvider _provider;

    public SkillPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Skill>> PickSkillAsync(int warbandArchetypeId)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Skill>>();

        var navigationService = _provider.GetRequiredService<ISkillPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<SkillSelectorPage>()) pour pouvoir poser le
        // filtre AllowedWarbandArchetypeId sur le ViewModel avant que la page ne charge ses données.
        var viewModel = _provider.GetRequiredService<SkillViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        var page = new SkillSelectorPage(viewModel);
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Skill>());
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
