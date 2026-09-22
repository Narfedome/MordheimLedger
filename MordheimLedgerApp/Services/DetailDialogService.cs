using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;
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

    /// <param name="foundValueOverride">Actual rolled resale value for one specific found
    /// WarbandEquipment row (see Models.WarbandEquipment.FoundValueOverride) - replaces the catalog's
    /// CostRandomMax-derived range in the popup when set (e.g. the Jewelsmith's Ruby, "45" instead of
    /// "15 - 90"). Null for every ordinary catalog-browsing call site.</param>
    /// <param name="blessingRule">The attached blessing rule (e.g. "Blessed Weapon"), loaded separately -
    /// see WarriorEquipment.BlessingRule. Independent of materialRule - both can be set at once.</param>
    Task ShowEquipmentDetailDialogAsync(EquipmentItem item, SpecialRule? materialRule = null, int? foundValueOverride = null, SpecialRule? blessingRule = null);
    Task ShowSkillDetailDialogAsync(Skill item);
    Task ShowSpecialRuleDetailDialogAsync(SpecialRule item);
    Task ShowMutationDetailDialogAsync(Mutation item);
    Task ShowHiredSwordDetailDialogAsync(HiredSword item);

    /// <summary>Always the plain full profile (stat line/equipment/skills) of THIS specific persona, even
    /// if it's one half of a pair (e.g. Ulli &amp; Marquand) - 2026-09-01, user request: the Codex must show
    /// the tapped persona's own sheet directly, not a pair summary. Recruitment-flavored callers
    /// (DramatisPersonaViewModel.ShowDetails in selector mode, EndOfGameDialogViewModel.RareItems.
    /// ShowCharacterDetail) call ShowDramatisPersonaPairDetailDialogAsync instead when the target is
    /// paired - see their own doc for the exact split.</summary>
    Task ShowDramatisPersonaDetailDialogAsync(DramatisPersona item);

    /// <summary>Pair summary (Description + shared SpecialRules + 2 profile chips, see
    /// DramatisPersonaPairDetailDialogViewModel) - only ever called explicitly by a recruitment-context
    /// caller for an item known to be paired (see ShowDramatisPersonaDetailDialogAsync's own doc).</summary>
    Task ShowDramatisPersonaPairDetailDialogAsync(DramatisPersona item);

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

    public async Task ShowEquipmentDetailDialogAsync(EquipmentItem item, SpecialRule? materialRule = null, int? foundValueOverride = null, SpecialRule? blessingRule = null)
    {
        var language = LocalizationService.Instance.Language;
        var categoryLabel = LocalizationService.Instance[$"EquipmentCategory{item.Category}"];

        var allWarbands = await _libraryService.GetWarbandArchetypesAsync(language);
        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : allWarbands.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var restrictedWarriors = item.RestrictedToWarbandArchetypeIds.Count == 0 || item.RestrictedToWarriorArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(item.RestrictedToWarbandArchetypeIds, language))
                .Where(w => item.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        await ShowAsync(new EquipmentItemDetailDialog(
            new EquipmentItemDetailDialogViewModel(item, categoryLabel, restrictedWarbands, allWarbands, restrictedWarriors, this, materialRule, foundValueOverride, blessingRule)));
    }

    public async Task ShowSkillDetailDialogAsync(Skill item)
    {
        var language = LocalizationService.Instance.Language;
        var categoryLabel = LocalizationService.Instance[$"SkillCategory{item.Category}"];

        var allWarbands = await _libraryService.GetWarbandArchetypesAsync(language);
        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : allWarbands.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var restrictedWarriors = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(item.RestrictedToWarbandArchetypeIds, language))
                .Where(w => item.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        await ShowAsync(new SkillDetailDialog(new SkillDetailDialogViewModel(item, categoryLabel, restrictedWarbands, allWarbands, restrictedWarriors)));
    }

    public Task ShowSpecialRuleDetailDialogAsync(SpecialRule item) =>
        ShowAsync(new SpecialRuleDetailDialog(new SpecialRuleDetailDialogViewModel(item)));

    public async Task ShowMutationDetailDialogAsync(Mutation item)
    {
        var language = LocalizationService.Instance.Language;
        var allWarbands = await _libraryService.GetWarbandArchetypesAsync(language);
        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : allWarbands.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        await ShowAsync(new MutationDetailDialog(new MutationDetailDialogViewModel(item, restrictedWarbands, allWarbands)));
    }

    public async Task ShowHiredSwordDetailDialogAsync(HiredSword item)
    {
        var language = LocalizationService.Instance.Language;
        var allWarbands = await _libraryService.GetWarbandArchetypesAsync(language);
        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : allWarbands.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var startingEquipment = item.StartingEquipmentIds.Count == 0
            ? new List<EquipmentQuantityChip>()
            : EquipmentQuantityChip.GroupFrom(item.StartingEquipmentIds, await _libraryService.GetEquipmentItemsAsync(language));

        var magicSchoolSpells = item.MagicSchool is null
            ? new List<Spell>()
            : (await _libraryService.GetSpellsAsync(language)).Where(s => s.MagicSchoolId == item.MagicSchool.Id).ToList();

        await ShowAsync(new HiredSwordDetailDialog(new HiredSwordDetailDialogViewModel(item, startingEquipment, restrictedWarbands, allWarbands, magicSchoolSpells, this)));
    }

    /// <summary>Toujours le profil complet de CE personnage précis (stats/équipement/compétences), même
    /// s'il fait partie d'une paire - 2026-09-01, retour utilisateur : le Codex doit montrer directement
    /// la fiche du personnage tapé, pas le résumé de paire. Les appelants "contexte recrutement" (picker/
    /// wizard Fin de Partie) appellent explicitement ShowDramatisPersonaPairDetailDialogAsync à la place
    /// quand ils veulent ce résumé - voir DramatisPersonaViewModel.ShowDetails/EndOfGameDialogViewModel.
    /// RareItems.ShowCharacterDetail pour le clivage exact.</summary>
    public async Task ShowDramatisPersonaDetailDialogAsync(DramatisPersona item)
    {
        var language = LocalizationService.Instance.Language;
        var allWarbands = await _libraryService.GetWarbandArchetypesAsync(language);
        var restrictedWarbands = item.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : allWarbands.Where(w => item.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var startingEquipment = item.StartingEquipmentIds.Count == 0
            ? new List<EquipmentQuantityChip>()
            : EquipmentQuantityChip.GroupFrom(item.StartingEquipmentIds, await _libraryService.GetEquipmentItemsAsync(language));

        var magicSchoolSpells = item.MagicSchool is null
            ? new List<Spell>()
            : (await _libraryService.GetSpellsAsync(language)).Where(s => s.MagicSchoolId == item.MagicSchool.Id).ToList();

        await ShowAsync(new DramatisPersonaDetailDialog(new DramatisPersonaDetailDialogViewModel(item, startingEquipment, restrictedWarbands, allWarbands, magicSchoolSpells, this)));
    }

    /// <summary>Résumé de paire (Description + règles communes + 2 chips profil) - 2026-09-01, contexte
    /// recrutement uniquement (voir ShowDramatisPersonaDetailDialogAsync's own doc). item n'a pas besoin
    /// d'être vérifié paired ici - l'appelant a déjà fait ce choix, seul un item réellement paired est
    /// jamais passé (DramatisPersonaPairDetailDialogViewModel gère un Profiles à 1 seul élément si jamais
    /// PairedWithDramatisPersona était null, par défense).</summary>
    public Task ShowDramatisPersonaPairDetailDialogAsync(DramatisPersona item) =>
        ShowAsync(new DramatisPersonaPairDetailDialog(new DramatisPersonaPairDetailDialogViewModel(item, this)));

    public Task ShowSpellDetailDialogAsync(Spell item) =>
        ShowAsync(new SpellDetailDialog(new SpellDetailDialogViewModel(item)));

    public Task ShowInjuryDetailDialogAsync(Injury item) =>
        ShowAsync(new InjuryDetailDialog(new InjuryDetailDialogViewModel(item, this)));
}
