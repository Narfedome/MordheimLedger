using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Skills;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface ISkillPickerService
{
    /// <summary>warbandArchetypeId: only skills whose RestrictedToWarbandArchetypeIds is empty (common)
    /// or contains this id are selectable - see WarriorEditDialogViewModel.AddSkill/
    /// EndOfGameDialogViewModel's Advance roll skill choice. warriorArchetypeId: same idea one level
    /// down, null = no further filtering (e.g. picking for a Warrior whose archetype isn't relevant).
    /// allowedCategories: further restrict to these SkillCategory values (the warrior's own "skill
    /// table" row, e.g. WarriorArchetype/Warrior.AllowedSkillCategories) - null/omitted = no category
    /// filtering.</summary>
    Task<IReadOnlyList<Skill>> PickSkillAsync(int warbandArchetypeId, int? warriorArchetypeId = null,
        IReadOnlyList<SkillCategory>? allowedCategories = null);
}

public class SkillPickerService : ISkillPickerService
{
    private readonly IServiceProvider _provider;

    public SkillPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Skill>> PickSkillAsync(int warbandArchetypeId, int? warriorArchetypeId = null,
        IReadOnlyList<SkillCategory>? allowedCategories = null)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Skill>>();

        var navigationService = _provider.GetRequiredService<ISkillPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<SkillSelectorPage>()) pour pouvoir poser le
        // filtre AllowedWarbandArchetypeId sur le ViewModel avant que la page ne charge ses données.
        var viewModel = _provider.GetRequiredService<SkillViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        viewModel.AllowedWarriorArchetypeId = warriorArchetypeId;
        viewModel.AllowedCategories = allowedCategories is { Count: > 0 } ? allowedCategories : null;
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

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(modal), "SkillPicker.Push");

        return await tcs.Task;
    }
}
