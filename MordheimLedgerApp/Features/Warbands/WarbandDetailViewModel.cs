using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
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
    private readonly IHiredSwordPickerService _hiredSwordPicker;

    private List<WarriorArchetype> _recruitableArchetypes = new();
    private List<HiredSword> _recruitableHiredSwords = new();
    private List<SpecialRule> _bandWideSpecialRules = new();
    private List<MagicSchool> _bandMagicSchools = new();

    /// <summary>All WarbandArchetype names (not just this warband's own), needed to resolve a Hatred
    /// rule's target - which can point at any band type, not just the current one. See
    /// BuildSpecialRuleChips.</summary>
    private Dictionary<int, string> _warbandArchetypeNames = new();

    [ObservableProperty]
    private int warbandId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNextGameNote))]
    [NotifyPropertyChangedFor(nameof(HasGameInProgress))]
    private Warband? warband;

    /// <summary>Warband.NextGameNote non-null (voir sa doc) - une bannière dédiée sur cette page plutôt
    /// qu'une entrée d'Historique de plus, pour rester visible tant qu'elle s'applique (jusqu'à la Fin de
    /// Partie suivante, qui la consomme - voir WarbandDetailViewModel.EndOfGame.ApplyExplorationOutcomeAsync).</summary>
    public bool HasNextGameNote => !string.IsNullOrWhiteSpace(Warband?.NextGameNote);

    /// <summary>Warband.GameInProgress (voir sa doc) - bascule quel des deux boutons "Lancer la partie"/
    /// "Fin de partie" s'affiche sur cette page (jamais les deux).</summary>
    public bool HasGameInProgress => Warband?.GameInProgress ?? false;

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
    private ObservableCollection<WarriorRow> retiredWarriors = new();

    [ObservableProperty]
    private bool heroesExpanded = true;

    [ObservableProperty]
    private bool henchmenExpanded = true;

    [ObservableProperty]
    private bool deadExpanded;

    [ObservableProperty]
    private bool retiredExpanded;

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
        ISpellPickerService spellPicker, IMutationPickerService mutationPicker, IHiredSwordPickerService hiredSwordPicker)
    {
        _warbandService = warbandService;
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _injuryPicker = injuryPicker;
        _spellPicker = spellPicker;
        _mutationPicker = mutationPicker;
        _hiredSwordPicker = hiredSwordPicker;

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

    [RelayCommand]
    private void ToggleRetired() => RetiredExpanded = !RetiredExpanded;

    private async Task LoadAsync(int id)
    {
        await Loading.RunAsync(async () =>
        {
            Warband = await _warbandService.GetWarbandAsync(id);
            if (Warband is null) return;

            _recruitableArchetypes = await _libraryService.GetWarriorArchetypesAsync(Warband.WarbandArchetypeId, LocalizationService.Instance.Language);
            // Pour la résolution du RoleName d'un guerrier recruté d'un Franc-Tireur (voir ToRow) - le
            // catalogue complet, pas filtré par restriction de bande (un Franc-Tireur déjà recruté doit
            // toujours afficher son nom, même si son entrée catalogue a depuis été restreinte à d'autres
            // bandes).
            _recruitableHiredSwords = await _libraryService.GetHiredSwordsAsync(LocalizationService.Instance.Language);
            var warbandArchetype = await _libraryService.GetWarbandArchetypeAsync(Warband.WarbandArchetypeId, LocalizationService.Instance.Language);
            _bandWideSpecialRules = warbandArchetype?.SpecialRules ?? new List<SpecialRule>();
            _bandMagicSchools = warbandArchetype?.MagicSchools ?? new List<MagicSchool>();
            var allWarbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
            _warbandArchetypeNames = allWarbandArchetypes.ToDictionary(a => a.Id, a => a.Name);

            var loaded = await _warbandService.GetWarriorsAsync(id, LocalizationService.Instance.Language);
            var rows = loaded.Select(ToRow).ToList();
            Heroes = new ObservableCollection<WarriorRow>(rows.Where(r => r.Warrior.IsHero && !r.IsDead && !r.IsRetired));
            Henchmen = new ObservableCollection<WarriorRow>(rows.Where(r => !r.Warrior.IsHero && !r.IsDead && !r.IsRetired));
            DeadWarriors = new ObservableCollection<WarriorRow>(rows.Where(r => r.IsDead));
            RetiredWarriors = new ObservableCollection<WarriorRow>(rows.Where(r => r.IsRetired));
            Rating = Heroes.Concat(Henchmen).Sum(r => WarbandRatingRules.WarriorContribution(
                r.Warrior.IsLargeCreature, r.Warrior.Experience, r.Warrior.HeadCount, r.Warrior.HiredSwordBaseRating));

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
        // Un guerrier recruté d'un Franc-Tireur (voir Warrior.IsHiredSword) n'a pas de WarriorArchetype
        // à résoudre - RoleName vient du catalogue HiredSword à la place, ses propres SpecialRules
        // jouant le rôle que jouerait archetypeRules ci-dessous. École de magie : PROPRE au Franc-Tireur
        // (HiredSword.MagicSchoolId, ex. le Sorcier/Magie Mineure) plutôt que celle de la bande qui
        // l'engage - contrairement à un WarriorArchetype normal (IsSpellcaster + écoles DE LA BANDE).
        if (warrior.HiredSwordId is { } hiredSwordId)
        {
            var hiredSword = _recruitableHiredSwords.FirstOrDefault(h => h.Id == hiredSwordId);
            var hiredSwordEquipmentRules = warrior.Equipment.SelectMany(e => e.Item.SpecialRules);
            var hiredSwordMergedRules = _bandWideSpecialRules.Concat(hiredSword?.SpecialRules ?? new List<SpecialRule>())
                .Concat(hiredSwordEquipmentRules).DistinctBy(r => r.Id);
            var hiredSwordHatredChips = warrior.Hatreds.Select(h => new WarriorHatredChip { Item = h, Name = string.Format(Loc["WarriorsHatredChipFormat"], h.Name) });
            var hiredSwordMagicSchools = hiredSword?.MagicSchool is { } school ? new List<MagicSchool> { school } : null;
            return new WarriorRow(warrior, hiredSword?.Name ?? "?", BuildSpecialRuleChips(hiredSwordMergedRules), hiredSwordMagicSchools, hiredSwordHatredChips);
        }

        var archetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == warrior.WarriorArchetypeId);
        var archetypeRules = archetype?.SpecialRules ?? new List<SpecialRule>();
        // Un objet équipé (ex. Marteau des Sorcières) peut lui aussi accorder une règle spéciale - avant
        // ce correctif, seule la fusion bande+archétype était faite, les règles portées par
        // l'équipement n'apparaissaient jamais en chip sur la carte guerrier.
        var equipmentRules = warrior.Equipment.SelectMany(e => e.Item.SpecialRules);
        // Une Blessure Grave qui accorde une règle permanente (Folie 24 -> Stupidité/Frénésie, Bras
        // amputé -> Armes à une main uniquement, voir Injury.SpecialRules) n'est PAS fusionnée ici -
        // contrairement à l'équipement, le rappel de règle vit uniquement derrière la puce Blessure
        // elle-même (InjuryDetailDialog affiche la SpecialRule en puce imbriquée) : demande explicite de
        // l'utilisateur 2026-08-26, pour éviter la puce en double (une fois via "Folie : Stupidité" dans
        // Blessures, une fois via "Stupidité" dans Règles spéciales).
        var mergedRules = _bandWideSpecialRules.Concat(archetypeRules).Concat(equipmentRules).DistinctBy(r => r.Id);
        // Un lanceur de sorts pioche dans les écoles de SA bande (pas d'affiliation propre au guerrier) -
        // voir WarriorRow.MagicSchools. Vide pour tout autre guerrier.
        var magicSchools = archetype?.IsSpellcaster == true ? _bandMagicSchools : null;
        var hatredChips = warrior.Hatreds.Select(h => new WarriorHatredChip { Item = h, Name = string.Format(Loc["WarriorsHatredChipFormat"], h.Name) });
        return new WarriorRow(warrior, archetype?.Name ?? "?", BuildSpecialRuleChips(mergedRules), magicSchools, hatredChips);
    }

    /// <summary>A rule with a mechanized Hatred target (SpecialRule.HatredTargetWarbandArchetypeIds)
    /// explodes into one chip per target ("Haine : Skavens") instead of a single generic "Haine" chip -
    /// every other rule maps 1:1 to its own catalog Name, unchanged.</summary>
    private List<SpecialRuleChip> BuildSpecialRuleChips(IEnumerable<SpecialRule> rules)
    {
        var chips = new List<SpecialRuleChip>();
        foreach (var rule in rules)
        {
            if (rule.HatredTargetWarbandArchetypeIds.Count == 0)
            {
                chips.Add(new SpecialRuleChip { Item = rule, Name = rule.Name });
                continue;
            }

            foreach (var targetId in rule.HatredTargetWarbandArchetypeIds)
            {
                var targetName = _warbandArchetypeNames.GetValueOrDefault(targetId, "?");
                chips.Add(new SpecialRuleChip { Item = rule, Name = string.Format(Loc["WarriorsHatredChipFormat"], targetName) });
            }
        }
        return chips;
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
        // Copie exhaustive de TOUS les champs de Warrior - SaveWarriorAsync fait un UPDATE complet
        // (warrior.ToEntity()), donc un champ oublié ici est silencieusement remis à sa valeur par
        // défaut au premier Enregistrer (bug trouvé en ajoutant les 3 champs Franc-Tireur ci-dessous :
        // IsLargeCreature/GainsExperience/IsLeader/AllowedSkillCategories/CanUseEquipment/tous les Max*/
        // tous les Starting*/IncreasedCharacteristics/SickGamesRemaining/Hatreds manquaient déjà tous,
        // silencieusement effacés à chaque édition - CanUseEquipment=false est précisément ce qui masque
        // le bouton "+" équipement d'un Franc-Tireur, donc ce trou touchait directement la nouvelle
        // fonctionnalité, pas seulement un bug préexistant sans rapport).
        var copy = new Warrior
        {
            Id = w.Id,
            WarbandId = w.WarbandId,
            WarriorArchetypeId = w.WarriorArchetypeId,
            HiredSwordId = w.HiredSwordId,
            HiredSwordBaseRating = w.HiredSwordBaseRating,
            HiredSwordUpkeepPrepaid = w.HiredSwordUpkeepPrepaid,
            Name = w.Name,
            IsHero = w.IsHero,
            Cost = w.Cost,
            Experience = w.Experience,
            Status = w.Status,
            SickGamesRemaining = w.SickGamesRemaining,
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
            StartingMovement = w.StartingMovement,
            StartingWeaponSkill = w.StartingWeaponSkill,
            StartingBallisticSkill = w.StartingBallisticSkill,
            StartingStrength = w.StartingStrength,
            StartingToughness = w.StartingToughness,
            StartingWounds = w.StartingWounds,
            StartingInitiative = w.StartingInitiative,
            StartingAttacks = w.StartingAttacks,
            StartingLeadership = w.StartingLeadership,
            EquipmentListId = w.EquipmentListId,
            CanUseEquipment = w.CanUseEquipment,
            AllowedSkillCategories = w.AllowedSkillCategories,
            Equipment = w.Equipment,
            Skills = w.Skills,
            Injuries = w.Injuries,
            Hatreds = w.Hatreds,
            Spells = w.Spells,
            Mutations = w.Mutations,
            Animal = w.Animal,
            IsLargeCreature = w.IsLargeCreature,
            GainsExperience = w.GainsExperience,
            IsLeader = w.IsLeader,
            MaxMovement = w.MaxMovement,
            MaxWeaponSkill = w.MaxWeaponSkill,
            MaxBallisticSkill = w.MaxBallisticSkill,
            MaxStrength = w.MaxStrength,
            MaxToughness = w.MaxToughness,
            MaxWounds = w.MaxWounds,
            MaxInitiative = w.MaxInitiative,
            MaxAttacks = w.MaxAttacks,
            MaxLeadership = w.MaxLeadership,
            IncreasedCharacteristics = w.IncreasedCharacteristics
        };

        var archetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == w.WarriorArchetypeId);
        var hiredSword = _recruitableHiredSwords.FirstOrDefault(h => h.Id == w.HiredSwordId);
        await OpenWarriorEditDialogAsync(copy, archetype, hiredSword, row.Equipment);
    }

    /// <summary>Ouvre WarriorEditDialog sur warrior (déjà en base - un guerrier fraîchement recruté ou
    /// une copie défensive d'un guerrier existant, voir RecruitWarriorAsync/EditWarrior) et applique le
    /// résultat - factorisé ici, les deux appelants ne différaient que par la provenance du Warrior et
    /// l'équipement à rembourser en cas de suppression. hiredSword non-null seulement pour un guerrier
    /// recruté d'un Franc-Tireur (archetype reste alors null, voir Warrior.IsHiredSword) - son école de
    /// magie propre (HiredSword.MagicSchool, ex. le Sorcier) prime sur celle de la bande.</summary>
    private async Task OpenWarriorEditDialogAsync(Warrior warrior, WarriorArchetype? archetype, HiredSword? hiredSword, IEnumerable<WarriorEquipment> equipmentForRefund)
    {
        if (Warband is null) return;

        var isSpellcaster = archetype?.IsSpellcaster == true || hiredSword?.MagicSchool is not null;
        var isMutant = archetype?.CanBuyMutations ?? false;
        var archetypeRules = archetype?.SpecialRules ?? hiredSword?.SpecialRules ?? new List<SpecialRule>();
        var specialRules = _bandWideSpecialRules.Concat(archetypeRules).DistinctBy(r => r.Id).ToList();
        // École(s) proposée(s) au picker de sort : celles DE LA BANDE pour un guerrier normal, mais la
        // seule école PROPRE au Franc-Tireur pour lui (voir HiredSword.MagicSchoolId) - jamais les deux
        // mélangées, un Franc-Tireur n'a pas accès aux écoles de la bande qui l'engage.
        var magicSchools = hiredSword?.MagicSchool is { } hiredSwordSchool ? new List<MagicSchool> { hiredSwordSchool } : _bandMagicSchools;

        var dialogViewModel = new WarriorEditDialogViewModel(warrior, Loc["WarriorEditTitle"], Warband, _warbandService,
            _libraryService, _detailDialogs, _equipmentPicker, _skillPicker, _injuryPicker, _spellPicker, isSpellcaster, magicSchools,
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
    private Task ShowSpecialRuleDetail(SpecialRuleChip chip) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(chip.Item);

    [RelayCommand]
    private Task ShowInjuryDetail(InjuryChipGroup group) => _detailDialogs.ShowInjuryDetailDialogAsync(group.Representative.Item);

    [RelayCommand]
    private Task ShowSpellDetail(WarriorSpell spell) => _detailDialogs.ShowSpellDetailDialogAsync(spell.Item);

    [RelayCommand]
    private Task ShowMutationDetail(WarriorMutation mutation) => _detailDialogs.ShowMutationDetailDialogAsync(mutation.Item);

    [RelayCommand]
    private Task ShowAnimalDetail(EquipmentItem animal) => _detailDialogs.ShowEquipmentDetailDialogAsync(animal);

    [RelayCommand]
    private Task ShowEquipmentDetail(WarriorEquipment equipment) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(equipment.Item, equipment.MaterialRule, equipment.FoundValueOverride, equipment.BlessingRule);

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
