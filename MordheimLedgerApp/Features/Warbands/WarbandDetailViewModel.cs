using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Warbands.CreateEdit;
using MordheimLedgerApp.Features.Warbands.EndOfGame;
using MordheimLedgerApp.Features.Warbands.Inventory;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands;

[QueryProperty(nameof(WarbandId), "warbandId")]
public partial class WarbandDetailViewModel : BaseViewModel
{
    private readonly IWarbandService _warbandService;
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly IInjuryPickerService _injuryPicker;
    private readonly ISpellPickerService _spellPicker;
    private readonly IMutationPickerService _mutationPicker;

    private List<WarriorArchetype> _recruitableArchetypes = new();
    private Dictionary<int, string> _archetypeNames = new();
    private List<SpecialRule> _bandWideSpecialRules = new();
    private List<MagicSchool> _bandMagicSchools = new();

    [ObservableProperty]
    private int warbandId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNextGameNote))]
    private Warband? warband;

    /// <summary>Warband.NextGameNote non-null (voir sa doc) - une bannière dédiée sur cette page plutôt
    /// qu'une entrée d'Historique de plus, pour rester visible tant qu'elle s'applique (jusqu'à la Fin de
    /// Partie suivante, qui la consomme - voir WarbandDetailViewModel.EndOfGame.ApplyExplorationOutcomeAsync).</summary>
    public bool HasNextGameNote => !string.IsNullOrWhiteSpace(Warband?.NextGameNote);

    /// <summary>Rulebook "calculate the warband rating" - sum over Heroes+Henchmen (active roster, dead
    /// warriors excluded) of (IsLargeCreature ? 20 : 5) + Experience. Recomputed after every LoadAsync -
    /// see IWarbandService.GetWarbandRatingAsync for the equivalent lightweight query used by
    /// WarbandListViewModel, which doesn't otherwise load the full roster.</summary>
    [ObservableProperty]
    private int rating;

    [ObservableProperty]
    private ObservableCollection<WarriorRow> heroes = new();

    [ObservableProperty]
    private ObservableCollection<WarriorRow> henchmen = new();

    [ObservableProperty]
    private ObservableCollection<WarriorRow> deadWarriors = new();

    [ObservableProperty]
    private bool heroesExpanded = true;

    [ObservableProperty]
    private bool henchmenExpanded = true;

    [ObservableProperty]
    private bool deadExpanded;

    /// <summary>Objets trouvés mais pas encore assignés à un guerrier (voir Models.WarbandEquipment,
    /// alimenté par l'étape Exploration du wizard Fin de Partie) - un bouton en en-tête de page
    /// (visible seulement si HasInventory) ouvre WarbandInventoryDialog pour les réattribuer, plutôt
    /// qu'une section dépliable dans le roster (retour utilisateur explicite, 2026-08-18).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInventory))]
    private ObservableCollection<WarbandEquipment> inventory = new();

    public bool HasInventory => Inventory.Count > 0;

    [ObservableProperty]
    private ObservableCollection<HistoryEntry> historyEntries = new();

    [ObservableProperty]
    private bool showHistory;

    public WarbandDetailViewModel(IWarbandService warbandService, ILibraryService libraryService, IDetailDialogService detailDialogs,
        IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker, IInjuryPickerService injuryPicker,
        ISpellPickerService spellPicker, IMutationPickerService mutationPicker)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _injuryPicker = injuryPicker;
        _spellPicker = spellPicker;
        _mutationPicker = mutationPicker;

        // Le roster affiche des noms d'Équipement/Compétences/Blessures résolus dans la langue courante
        // - sans ça, ils resteraient périmés si la langue change pendant que cette page est déjà
        // affichée (même besoin que les pages Bibliothèque, voir WarbandArchetypeViewModel).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) =>
        {
            var vm = (WarbandDetailViewModel)r;
            if (vm.Warband is not null) _ = vm.LoadAsync(vm.WarbandId);
        });
    }

    partial void OnWarbandIdChanged(int value) => _ = LoadAsync(value);

    [RelayCommand]
    private void ToggleHeroes() => HeroesExpanded = !HeroesExpanded;

    [RelayCommand]
    private void ToggleHenchmen() => HenchmenExpanded = !HenchmenExpanded;

    [RelayCommand]
    private void ToggleDead() => DeadExpanded = !DeadExpanded;

    private async Task LoadAsync(int id)
    {
        await Loading.RunAsync(async () =>
        {
            Warband = await _warbandService.GetWarbandAsync(id);
            if (Warband is null) return;

            _recruitableArchetypes = await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, LocalizationService.Instance.Language);
            _archetypeNames = _recruitableArchetypes.ToDictionary(a => a.Id, a => a.Name);
            var warbandArchetype = await _libraryService.GetWarbandArchetypeAsync(Warband.WarbandArchetypeId, LocalizationService.Instance.Language);
            _bandWideSpecialRules = warbandArchetype?.SpecialRules ?? new List<SpecialRule>();
            _bandMagicSchools = warbandArchetype?.MagicSchools ?? new List<MagicSchool>();

            var loaded = await _warbandService.GetWarriorsAsync(id, LocalizationService.Instance.Language);
            var rows = loaded.Select(ToRow).ToList();
            Heroes = new ObservableCollection<WarriorRow>(rows.Where(r => r.Warrior.IsHero && !r.IsDead));
            Henchmen = new ObservableCollection<WarriorRow>(rows.Where(r => !r.Warrior.IsHero && !r.IsDead));
            DeadWarriors = new ObservableCollection<WarriorRow>(rows.Where(r => r.IsDead));
            Rating = Heroes.Concat(Henchmen).Sum(r => (r.Warrior.IsLargeCreature ? 20 : 5) + r.Warrior.Experience);

            var inventory = await _warbandService.GetWarbandEquipmentAsync(id, LocalizationService.Instance.Language);
            Inventory = new ObservableCollection<WarbandEquipment>(inventory);

            var history = await _warbandService.GetHistoryEntriesAsync(id);
            HistoryEntries = new ObservableCollection<HistoryEntry>(history);
        });
    }

    /// <summary>Ouvre l'inventaire de bande dans un dialog dédié (WarbandInventoryDialog) où chaque objet
    /// peut être réattribué à un guerrier via un simple ActionSheet - toujours recharger le roster à la
    /// fermeture, quel que soit le mode de fermeture (X ou auto-fermeture sur liste vide côté dialog),
    /// même logique que OpenWarriorEditDialogAsync : le dialog persiste ses changements immédiatement,
    /// pas de distinction Enregistrer/Annuler à respecter ici.</summary>
    [RelayCommand]
    private async Task ShowInventory()
    {
        var candidates = Heroes.Concat(Henchmen).ToList();
        var dialogViewModel = new WarbandInventoryDialogViewModel(Inventory, candidates, _warbandService);
        await ShowDialogAsync(new WarbandInventoryDialog(dialogViewModel));
        await LoadAsync(WarbandId);
    }

    private WarriorRow ToRow(Warrior warrior)
    {
        var archetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == warrior.WarriorArchetypeId);
        var archetypeRules = archetype?.SpecialRules ?? new List<SpecialRule>();
        var mergedRules = _bandWideSpecialRules.Concat(archetypeRules).DistinctBy(r => r.Id);
        // Un lanceur de sorts pioche dans les écoles de SA bande (pas d'affiliation propre au guerrier) -
        // voir WarriorRow.MagicSchools. Vide pour tout autre guerrier.
        var magicSchools = archetype?.IsSpellcaster == true ? _bandMagicSchools : null;
        return new WarriorRow(warrior, _archetypeNames.GetValueOrDefault(warrior.WarriorArchetypeId, "?"), mergedRules, magicSchools);
    }

    [RelayCommand]
    private static async Task BackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void ShowRoster() => ShowHistory = false;

    [RelayCommand]
    private void ShowHistoryTab() => ShowHistory = true;

    [RelayCommand]
    private async Task EditWarrior(WarriorRow row)
    {
        if (Warband is null) return;

        var w = row.Warrior;
        var copy = new Warrior
        {
            Id = w.Id,
            WarbandId = w.WarbandId,
            WarriorArchetypeId = w.WarriorArchetypeId,
            Name = w.Name,
            IsHero = w.IsHero,
            Cost = w.Cost,
            Experience = w.Experience,
            Status = w.Status,
            HeadCount = w.HeadCount,
            Movement = w.Movement,
            MovementOverride = w.MovementOverride,
            WeaponSkill = w.WeaponSkill,
            BallisticSkill = w.BallisticSkill,
            Strength = w.Strength,
            Toughness = w.Toughness,
            Wounds = w.Wounds,
            Initiative = w.Initiative,
            Attacks = w.Attacks,
            Leadership = w.Leadership,
            EquipmentListId = w.EquipmentListId,
            Equipment = w.Equipment,
            Skills = w.Skills,
            Injuries = w.Injuries,
            Spells = w.Spells,
            Mutations = w.Mutations,
            Animal = w.Animal
        };

        var archetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == w.WarriorArchetypeId);
        await OpenWarriorEditDialogAsync(copy, archetype, row.Equipment);
    }

    /// <summary>Ouvre WarriorEditDialog sur warrior (déjà en base - un guerrier fraîchement recruté ou
    /// une copie défensive d'un guerrier existant, voir RecruitWarriorAsync/EditWarrior) et applique le
    /// résultat - factorisé ici, les deux appelants ne différaient que par la provenance du Warrior et
    /// l'équipement à rembourser en cas de suppression.</summary>
    private async Task OpenWarriorEditDialogAsync(Warrior warrior, WarriorArchetype? archetype, IEnumerable<WarriorEquipment> equipmentForRefund)
    {
        if (Warband is null) return;

        var isSpellcaster = archetype?.IsSpellcaster ?? false;
        var isMutant = archetype?.CanBuyMutations ?? false;
        var archetypeRules = archetype?.SpecialRules ?? new List<SpecialRule>();
        var specialRules = _bandWideSpecialRules.Concat(archetypeRules).DistinctBy(r => r.Id).ToList();

        var dialogViewModel = new WarriorEditDialogViewModel(warrior, Loc["WarriorEditTitle"], Warband, _warbandService,
            _libraryService, _detailDialogs, _equipmentPicker, _skillPicker, _injuryPicker, _spellPicker, isSpellcaster, _bandMagicSchools,
            _mutationPicker, isMutant, specialRules);
        var saved = await ShowDialogAsync(new WarriorEditDialog(dialogViewModel));

        // Toujours recharger, même si le dialog a été annulé : l'ajout/retrait de blessure suivie
        // (AddInjury/RemoveInjury) persiste immédiatement dans le dialog, indépendamment du bouton
        // Enregistrer/Annuler (même logique que l'Équipement/les Compétences sur la carte guerrier).
        await Loading.RunAsync(async () =>
        {
            // WasDeleted : le guerrier n'existe plus en base (supprimé depuis le dialog) - le
            // ré-enregistrer écraserait rien puisqu'il n'y a plus de ligne à mettre à jour. Rembourse
            // son coût de recrutement + son équipement d'ORIGINE (equipmentForRefund, snapshot avant
            // ouverture du dialog - vide pour un recrutement fraîchement créé) ; pas les compétences/
            // blessures, qui n'ont pas de coût.
            if (dialogViewModel.WasDeleted)
            {
                var refund = warrior.Cost + equipmentForRefund.Sum(e => e.Item.Cost * e.Quantity);
                Warband.Treasury += refund;
                await _warbandService.SaveWarbandAsync(Warband);
            }
            else if (saved == true)
            {
                await _warbandService.SaveWarriorAsync(warrior);
            }

            await LoadAsync(Warband.Id);
        });
    }

    // Puces de la carte guerrier (Règles spéciales/Blessures/Sorts/Mutations/Monture/Équipement/
    // Compétences) tapables - ouvrent le même dialog récap en lecture seule que la Bibliothèque/le
    // recrutement, via IDetailDialogService (voir ce service pour pourquoi il existe : cette
    // résolution de restrictions était dupliquée à la main dans ~28 endroits avant lui).
    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule);

    [RelayCommand]
    private Task ShowInjuryDetail(WarriorInjury injury) => _detailDialogs.ShowInjuryDetailDialogAsync(injury.Item);

    [RelayCommand]
    private Task ShowSpellDetail(WarriorSpell spell) => _detailDialogs.ShowSpellDetailDialogAsync(spell.Item);

    [RelayCommand]
    private Task ShowMutationDetail(WarriorMutation mutation) => _detailDialogs.ShowMutationDetailDialogAsync(mutation.Item);

    [RelayCommand]
    private Task ShowAnimalDetail(EquipmentItem animal) => _detailDialogs.ShowEquipmentDetailDialogAsync(animal);

    [RelayCommand]
    private Task ShowEquipmentDetail(WarriorEquipment equipment) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(equipment.Item, equipment.MaterialRule, equipment.FoundValueOverride);

    [RelayCommand]
    private Task ShowSkillDetail(WarriorSkill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill.Item);

    /// <summary>École de magie de la bande (puce band-wide, pas liée à un guerrier précis) - même popup
    /// Nom+Description+Sorts que WarbandArchetypeDetailDialogViewModel.ShowMagicSchoolDetail, pas de
    /// XxxDetailDialog dédié pour MagicSchool dans IDetailDialogService (voir ChipDetailDialogViewModel).</summary>
    [RelayCommand]
    private async Task ShowMagicSchoolDetail(MagicSchool school)
    {
        var language = LocalizationService.Instance.Language;
        var spells = (await _libraryService.GetSpellsAsync(language)).Where(s => s.MagicSchoolId == school.Id).ToList();
        await ShowDialogAsync(new ChipDetailDialog(new ChipDetailDialogViewModel(school.Name, school.Description, spells)));
    }

    [RelayCommand]
    private async Task AddNote()
    {
        if (Warband is null) return;

        var text = await ShowPromptAsync(Loc["HistoryNotePromptTitle"], Loc["PromptName"]);
        if (string.IsNullOrWhiteSpace(text)) return;

        await _warbandService.AddHistoryEntryAsync(Warband.Id, text);
        var history = await _warbandService.GetHistoryEntriesAsync(Warband.Id);
        HistoryEntries = new ObservableCollection<HistoryEntry>(history);
    }

}
