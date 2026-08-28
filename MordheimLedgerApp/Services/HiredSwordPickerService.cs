using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.HiredSwords;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IHiredSwordPickerService
{
    /// <summary>Multi-sélection (ex. l'étape Mercenaires du wizard de création - engager plusieurs
    /// types d'un coup).</summary>
    /// <param name="warbandArchetypeId">Narrowe aux Francs-Tireurs éligibles à cette bande (voir
    /// HiredSword.RestrictedToWarbandArchetypeIds) - null (usage Codex) montre tout le catalogue.</param>
    /// <param name="excludedHiredSwordIds">Types déjà activement engagés dans la bande - jamais réoffert
    /// ("un seul de chaque type", voir RecruitmentRules.CanRecruitHiredSword).</param>
    Task<IReadOnlyList<HiredSword>> PickHiredSwordsAsync(int? warbandArchetypeId = null, IReadOnlyList<int>? excludedHiredSwordIds = null);

    /// <summary>Sélection unique (ex. "Une Faveur Rendue" - un seul Franc-Tireur gratuit à la fois, voir
    /// EndOfGameDialogViewModel.Exploration.cs) - même filtres que PickHiredSwordsAsync, null si annulé.</summary>
    Task<HiredSword?> PickHiredSwordAsync(int? warbandArchetypeId = null, IReadOnlyList<int>? excludedHiredSwordIds = null);
}

public class HiredSwordPickerService : IHiredSwordPickerService
{
    private readonly IServiceProvider _provider;

    public HiredSwordPickerService(IServiceProvider provider) => _provider = provider;

    public Task<IReadOnlyList<HiredSword>> PickHiredSwordsAsync(int? warbandArchetypeId = null, IReadOnlyList<int>? excludedHiredSwordIds = null) =>
        PickAsync(SelectionMode.Multiple, warbandArchetypeId, excludedHiredSwordIds);

    public async Task<HiredSword?> PickHiredSwordAsync(int? warbandArchetypeId = null, IReadOnlyList<int>? excludedHiredSwordIds = null)
    {
        var result = await PickAsync(SelectionMode.Single, warbandArchetypeId, excludedHiredSwordIds);
        return result.FirstOrDefault();
    }

    private async Task<IReadOnlyList<HiredSword>> PickAsync(SelectionMode selectionMode, int? warbandArchetypeId, IReadOnlyList<int>? excludedHiredSwordIds)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<HiredSword>>();

        var navigationService = _provider.GetRequiredService<IHiredSwordPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<HiredSwordSelectorPage>()) pour pouvoir poser les
        // filtres sur le ViewModel avant que la page ne charge ses données - même idiome que
        // SkillPickerService.PickSkillAsync.
        var viewModel = _provider.GetRequiredService<HiredSwordViewModel>();
        viewModel.AllowedWarbandArchetypeId = warbandArchetypeId;
        viewModel.ExcludedHiredSwordIds = excludedHiredSwordIds is { Count: > 0 } ? excludedHiredSwordIds : null;
        // Poussée nue (pas de NavigationPage) - voir PickerSelectorLayout pour le pourquoi.
        var page = new HiredSwordSelectorPage(viewModel) { SelectionMode = selectionMode };

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<HiredSword>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "HiredSwordPicker.Push");

        return await tcs.Task;
    }
}
