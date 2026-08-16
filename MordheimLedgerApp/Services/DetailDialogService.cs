using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Features.Library.Injuries.CreateEdit;
using MordheimLedgerApp.Features.Library.Mutations.CreateEdit;
using MordheimLedgerApp.Features.Library.Skills.CreateEdit;
using MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;
using MordheimLedgerApp.Features.Library.Spells.CreateEdit;
using MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

namespace MordheimLedgerApp.Services;

/// <summary>Single entry point for the 9 catalog types' full read-only recap dialog (stats/cost/
/// restrictions/special rules - not to be confused with ChipDetailDialog, the generic Name+
/// Description popup used for a restriction chip *inside* one of these dialogs, which stays separate).
/// Before this service existed, every one of the ~28 call sites across the app (Codex tile info
/// buttons, chip taps on the warrior card, chip taps inside recruitment/edit dialogs) duplicated its
/// own restriction-resolution logic by hand - found via an audit after two of them (WarbandDetailPage's
/// chips, added in the same session) briefly used the wrong dialog (the generic ChipDetailDialog)
/// instead of the full recap. Centralizing here means every future caller gets full parity for free.
///
/// Singleton (matches ILibraryService/IWarbandService) - MAUI has no real per-request DI scope like
/// ASP.NET Core, so Scoped would behave identically to Singleton here anyway, and this service is
/// stateless besides. Doesn't extend BaseViewModel/DialogViewModel (this is a plain service, not a
/// ViewModel) - CurrentPage is duplicated from BaseViewModel.ShowDialogAsync's own resolution rather
/// than shared, since that property is private there and this is the only other place that needs it.</summary>
public interface IDetailDialogService
{
    Task ShowWarbandArchetypeDetailDialogAsync(WarbandArchetype item);

    /// <summary>warbandEquipmentLists: resolved by the caller, not this service - existing call sites
    /// disagree on what to pass (one always has the band's lists already loaded, the other
    /// deliberately passes an empty list because only the list's content, not its name, matters at
    /// that point) and this method preserves that choice rather than picking one.</summary>
    Task ShowWarriorArchetypeDetailDialogAsync(WarriorArchetype item, IReadOnlyList<NamedRef> warbandEquipmentLists);

    Task ShowEquipmentDetailDialogAsync(EquipmentItem item, SpecialRule? materialRule = null);
    Task ShowSkillDetailDialogAsync(Skill item);
    Task ShowSpecialRuleDetailDialogAsync(SpecialRule item);
    Task ShowMutationDetailDialogAsync(Mutation item);
    Task ShowSpellDetailDialogAsync(Spell item);
    Task ShowInjuryDetailDialogAsync(Injury item);
}

public class DetailDialogService : IDetailDialogService
{
    private readonly ILibraryService _libraryService;

    public DetailDialogService(ILibraryService libraryService) => _libraryService = libraryService;

    // Voir BaseViewModel.CurrentPage - même résolution exacte, dupliquée ici car privée là-bas et
    // c'est le seul autre endroit qui en a besoin (ce service n'est pas un ViewModel).
    private static Page CurrentPage => (Page?)Shell.Current ?? Application.Current!.Windows[0].Page!;

    private static Task ShowAsync<TResult>(DialogContent<TResult> content) =>
        DialogStack.Instance.PushAsync(content, CurrentPage);

    public Task ShowWarbandArchetypeDetailDialogAsync(WarbandArchetype item) =>
        ShowAsync(new WarbandArchetypeDetailDialog(new WarbandArchetypeDetailDialogViewModel(item, _libraryService, this)));

    public Task ShowWarriorArchetypeDetailDialogAsync(WarriorArchetype item, IReadOnlyList<NamedRef> warbandEquipmentLists) =>
        ShowAsync(new WarriorArchetypeDetailDialog(new WarriorArchetypeDetailDialogViewModel(item, warbandEquipmentLists)));

    public async Task ShowEquipmentDetailDialogAsync(EquipmentItem item, SpecialRule? materialRule = null)
    {
        var language = LocalizationService.Instance.Language;
        var categoryLabel = LocalizationService.Instance[$"EquipmentCategory{item.Category}"];

        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : (await _libraryService.GetWarbandArchetypesAsync(language))
                .Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var restrictedWarriors = item.RestrictedToWarbandArchetypeIds.Count == 0 || item.RestrictedToWarriorArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(item.RestrictedToWarbandArchetypeIds, language))
                .Where(w => item.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        await ShowAsync(new EquipmentItemDetailDialog(
            new EquipmentItemDetailDialogViewModel(item, categoryLabel, restrictedWarbands, restrictedWarriors, this, materialRule)));
    }

    public async Task ShowSkillDetailDialogAsync(Skill item)
    {
        var language = LocalizationService.Instance.Language;
        var categoryLabel = LocalizationService.Instance[$"SkillCategory{item.Category}"];

        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : (await _libraryService.GetWarbandArchetypesAsync(language))
                .Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var restrictedWarriors = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(item.RestrictedToWarbandArchetypeIds, language))
                .Where(w => item.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        await ShowAsync(new SkillDetailDialog(new SkillDetailDialogViewModel(item, categoryLabel, restrictedWarbands, restrictedWarriors)));
    }

    public Task ShowSpecialRuleDetailDialogAsync(SpecialRule item) =>
        ShowAsync(new SpecialRuleDetailDialog(new SpecialRuleDetailDialogViewModel(item)));

    public async Task ShowMutationDetailDialogAsync(Mutation item)
    {
        var language = LocalizationService.Instance.Language;
        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : (await _libraryService.GetWarbandArchetypesAsync(language))
                .Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        await ShowAsync(new MutationDetailDialog(new MutationDetailDialogViewModel(item, restrictedWarbands)));
    }

    public Task ShowSpellDetailDialogAsync(Spell item) =>
        ShowAsync(new SpellDetailDialog(new SpellDetailDialogViewModel(item)));

    public Task ShowInjuryDetailDialogAsync(Injury item) =>
        ShowAsync(new InjuryDetailDialog(new InjuryDetailDialogViewModel(item)));
}
