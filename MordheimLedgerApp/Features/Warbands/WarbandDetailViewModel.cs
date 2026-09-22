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
    private readonly IDramatisPersonaPickerService _dramatisPersonaPicker;
    private readonly ISellEquipmentPickerService _sellEquipmentPicker;

    private List<WarriorArchetype> _recruitableArchetypes = new();
    private List<HiredSword> _recruitableHiredSwords = new();
    private List<DramatisPersona> _recruitableDramatisPersonae = new();
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

    /// <summary>Own block, split out of Henchmen (2026-09-01, user request) - a recruited Hired Sword
    /// (Warrior.IsHiredSword) used to land in Henchmen (IsHero stays false for these, see the model's own
    /// doc) with just a distinguishing RoleName label, no separate visual grouping. Empty for most bands
    /// (no Hired Sword recruited) - see HasHiredSwords, which hides the whole block then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHiredSwords))]
    private ObservableCollection<WarriorRow> hiredSwords = new();

    /// <summary>Same split, for a recruited Dramatis Persona (Warrior.IsDramatisPersona) - see
    /// HiredSwords' own doc, identical reasoning/history.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDramatisPersonae))]
    private ObservableCollection<WarriorRow> dramatisPersonae = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeadWarriors))]
    private ObservableCollection<WarriorRow> deadWarriors = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRetiredWarriors))]
    private ObservableCollection<WarriorRow> retiredWarriors = new();

    public bool HasHiredSwords => HiredSwords.Count > 0;
    public bool HasDramatisPersonae => DramatisPersonae.Count > 0;

    /// <summary>Every living, active-roster warrior across all four groups (Heroes/Henchmen/HiredSwords/
    /// DramatisPersonae) - the same set "Heroes.Concat(Henchmen)" used to mean before Francs-Tireurs/
    /// Dramatis Personae got their own blocks (2026-09-01). Callers that need "everyone who could fight
    /// this battle" (Rating, Start Game, End of Game, inventory reassignment) should use this instead of
    /// concatenating the four groups by hand.</summary>
    public IEnumerable<WarriorRow> AllActiveWarriorRows => Heroes.Concat(Henchmen).Concat(HiredSwords).Concat(DramatisPersonae);

    /// <summary>Hides the whole Morts/Retraités block when empty (user request 2026-09-01) - unlike
    /// Heroes/Henchmen, which every warband always has at least one of, these (and the two groups above)
    /// are the exception rather than the rule for most bands.</summary>
    public bool HasDeadWarriors => DeadWarriors.Count > 0;
    public bool HasRetiredWarriors => RetiredWarriors.Count > 0;

    [ObservableProperty]
    private bool heroesExpanded = true;

    [ObservableProperty]
    private bool henchmenExpanded = true;

    [ObservableProperty]
    private bool hiredSwordsExpanded = true;

    [ObservableProperty]
    private bool dramatisPersonaeExpanded = true;

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
        ISpellPickerService spellPicker, IMutationPickerService mutationPicker, IHiredSwordPickerService hiredSwordPicker,
        IDramatisPersonaPickerService dramatisPersonaPicker, ISellEquipmentPickerService sellEquipmentPicker)
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
        _dramatisPersonaPicker = dramatisPersonaPicker;
        _sellEquipmentPicker = sellEquipmentPicker;

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
    private void ToggleHiredSwords() => HiredSwordsExpanded = !HiredSwordsExpanded;

    [RelayCommand]
    private void ToggleDramatisPersonae() => DramatisPersonaeExpanded = !DramatisPersonaeExpanded;

    [RelayCommand]
    private void ToggleDead() => DeadExpanded = !DeadExpanded;

    [RelayCommand]
    private void ToggleRetired() => RetiredExpanded = !RetiredExpanded;

    internal async Task LoadAsync(int id)
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
            // Même besoin, pour la résolution du RoleName/SpecialRules/MagicSchool d'un guerrier recruté
            // depuis le catalogue Dramatis Personae (voir ToRow) - catalogue complet, même raison.
            _recruitableDramatisPersonae = await _libraryService.GetDramatisPersonaeAsync(LocalizationService.Instance.Language);
            var warbandArchetype = await _libraryService.GetWarbandArchetypeAsync(Warband.WarbandArchetypeId, LocalizationService.Instance.Language);
            _bandWideSpecialRules = warbandArchetype?.SpecialRules ?? new List<SpecialRule>();
            _bandMagicSchools = warbandArchetype?.MagicSchools ?? new List<MagicSchool>();
            var allWarbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
            _warbandArchetypeNames = allWarbandArchetypes.ToDictionary(a => a.Id, a => a.Name);

            var loaded = await _warbandService.GetWarriorsAsync(id, LocalizationService.Instance.Language);
            var rosterBuilder = new WarriorRosterBuilder(_recruitableArchetypes, _recruitableHiredSwords, _recruitableDramatisPersonae,
                _bandWideSpecialRules, _bandMagicSchools, _warbandArchetypeNames);
            var rows = rosterBuilder.BuildRows(loaded);
            var alive = rows.Where(r => !r.IsDead && !r.IsRetired).ToList();
            // Francs-Tireurs/Dramatis Personae : blocs à part depuis le 2026-09-01 (retour utilisateur) -
            // avant, un Franc-Tireur recruté tombait dans Henchmen (IsHero=false pour lui, voir sa propre
            // doc). Un Dramatis Persona recruté a IsHero=TRUE en revanche (règle du livre : "suffer
            // serious injuries, just like Heroes" - voir EntityMapping.ToWarrior(this DramatisPersona,
            // ...) pour le raisonnement complet), donc explicitement exclu ici pour ne pas doublonner
            // avec son propre bloc DramatisPersonae ci-dessous.
            Heroes = new ObservableCollection<WarriorRow>(alive.Where(r => r.Warrior.IsHero && !r.Warrior.IsDramatisPersona));
            Henchmen = new ObservableCollection<WarriorRow>(alive.Where(r => !r.Warrior.IsHero && !r.Warrior.IsHiredSword && !r.Warrior.IsDramatisPersona));
            HiredSwords = new ObservableCollection<WarriorRow>(alive.Where(r => r.Warrior.IsHiredSword));
            DramatisPersonae = new ObservableCollection<WarriorRow>(alive.Where(r => r.Warrior.IsDramatisPersona));
            DeadWarriors = new ObservableCollection<WarriorRow>(rows.Where(r => r.IsDead));
            RetiredWarriors = new ObservableCollection<WarriorRow>(rows.Where(r => r.IsRetired));
            // Exclut du calcul tout guerrier basculé "hostile" via "Une Poignée d'Or" (2026-09-01, Ulli &
            // Marquand corrompus par l'adversaire pour cette bataille - voir Warrior.IsHostileThisBattle) :
            // il reste dans le roster (pas supprimé), juste sans contribution à la Valeur tant que le
            // flag est actif.
            Rating = alive.Where(r => !r.Warrior.IsHostileThisBattle).Sum(r => WarbandRatingRules.WarriorContribution(
                r.Warrior.IsLargeCreature, r.Warrior.Experience, r.Warrior.HeadCount, r.Warrior.HiredSwordBaseRating, r.Warrior.DramatisPersonaRatingBonus));

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
        var candidates = AllActiveWarriorRows.ToList();
        var dialogViewModel = new WarbandInventoryDialogViewModel(Inventory, candidates, _warbandService);
        await ShowDialogAsync(new WarbandInventoryDialog(dialogViewModel));
        await LoadAsync(WarbandId);
    }

    [RelayCommand]
    private static async Task BackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void ShowRoster() => ShowHistory = false;

    [RelayCommand]
    private void ShowHistoryTab() => ShowHistory = true;

    /// <summary>"Rendre hostile" - Une Poignée d'Or (2026-09-01, Ulli &amp; Marquand uniquement, see
    /// WarriorRow.CanToggleHostile). Purely a manual player-facing bookkeeping toggle: the app can't
    /// detect a mid-battle secret bribe itself (see the SpecialRule's own description), so the player
    /// flips this once an enemy successfully corrupts the pair. Persists immediately (same
    /// no-Enregistrer-button convention as ShowInventory) and recomputes Rating right away, excluding
    /// their contribution while the flag is set (see LoadAsync). Bascule TOUJOURS les deux moitiés de la
    /// paire ensemble (2026-09-01, retour utilisateur) - Ulli et Marquand sont corrompus ou non comme un
    /// seul bloc dans le livre, jamais l'un sans l'autre ; tapoter la carte de l'un des deux suffit,
    /// l'autre suit automatiquement au même état (pas un toggle indépendant du sien).</summary>
    [RelayCommand]
    private async Task ToggleHostile(WarriorRow row)
    {
        if (Warband is null) return;

        var newValue = !row.IsHostileThisBattle;
        row.IsHostileThisBattle = newValue;
        row.Warrior.IsHostileThisBattle = newValue;
        await _warbandService.SaveWarriorAsync(row.Warrior);

        var persona = _recruitableDramatisPersonae.FirstOrDefault(p => p.Id == row.Warrior.DramatisPersonaId);
        if (persona?.PairedWithDramatisPersonaId is { } partnerId
            && DramatisPersonae.FirstOrDefault(r => r.Warrior.DramatisPersonaId == partnerId) is { } partnerRow
            && partnerRow.IsHostileThisBattle != newValue)
        {
            partnerRow.IsHostileThisBattle = newValue;
            partnerRow.Warrior.IsHostileThisBattle = newValue;
            await _warbandService.SaveWarriorAsync(partnerRow.Warrior);
        }

        Rating = AllActiveWarriorRows.Where(r => !r.Warrior.IsHostileThisBattle).Sum(r => WarbandRatingRules.WarriorContribution(
            r.Warrior.IsLargeCreature, r.Warrior.Experience, r.Warrior.HeadCount, r.Warrior.HiredSwordBaseRating, r.Warrior.DramatisPersonaRatingBonus));
    }

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
        // fonctionnalité, pas seulement un bug préexistant sans rapport). Même classe de bug trouvée en
        // passant le 2026-09-01 en câblant IsHostileThisBattle (Une Poignée d'Or) :
        // DramatisPersonaId/DramatisPersonaRatingBonus manquaient déjà tous les deux - éditer un guerrier
        // recruté depuis le catalogue Dramatis Personae via ce bouton Éditer effaçait silencieusement son
        // lien vers son personnage catalogue au premier Enregistrer. Corrigé au passage.
        var copy = new Warrior
        {
            Id = w.Id,
            WarbandId = w.WarbandId,
            WarriorArchetypeId = w.WarriorArchetypeId,
            HiredSwordId = w.HiredSwordId,
            HiredSwordBaseRating = w.HiredSwordBaseRating,
            HiredSwordUpkeepPrepaid = w.HiredSwordUpkeepPrepaid,
            DramatisPersonaId = w.DramatisPersonaId,
            DramatisPersonaRatingBonus = w.DramatisPersonaRatingBonus,
            IsHostileThisBattle = w.IsHostileThisBattle,
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

    /// <summary>Haine section : contrairement à ShowSpecialRuleDetail (qui ouvre la vraie fiche catalogue
    /// de la règle source), ce recap est volontairement générique - la règle de Haine elle-même
    /// (relance des jets Pour Toucher au 1er round) est toujours la même quel que soit ce qui l'accorde
    /// (Rancune ou une règle spéciale de bande), donc pas de Item à résoudre ici (voir
    /// WarriorHatredChip). chip.Name ("Haine : X") sert de titre, la cible reste donc visible.</summary>
    [RelayCommand]
    private Task ShowHatredDetail(WarriorHatredChip chip) =>
        ShowDialogAsync(new ChipDetailDialog(new ChipDetailDialogViewModel(chip.Name, Loc["HatredRuleDescription"])));

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
