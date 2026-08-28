using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

public partial class WarriorEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Warband _warband;
    private readonly IWarbandService _warbandService;
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly IInjuryPickerService _injuryPicker;
    private readonly ISpellPickerService _spellPicker;
    private readonly IReadOnlyList<MagicSchool> _magicSchools;
    private readonly IMutationPickerService _mutationPicker;

    /// <summary>Mode Libre (voir WarbandEditDialogViewModel.IsExistingWarband, transmis tel quel par
    /// l'appelant) : AddEquipment n'impacte plus la trésorerie (aucune vérification, aucune déduction) et
    /// AddSpell repasse en choix libre au lieu du tirage 1D6 - même esprit "on enregistre un historique
    /// déjà déterminé" déjà appliqué ailleurs dans l'app pour ce mode. False = comportement d'origine
    /// (coûts réels), inchangé pour l'appelant existant (WarbandDetailViewModel.EditWarrior).</summary>
    private readonly bool _skipCosts;

    protected override bool CancelResult => false;

    /// <summary>Masque le bouton "x" de retrait d'un objet d'équipement pour un Franc-Tireur (livre des
    /// règles, section Hired Swords : "A player cannot buy extra weapons or equipment for a Hired Sword,
    /// and he cannot sell the Hired Sword's weapons or equipment") - CanUseEquipment est déjà ce qui
    /// masque le bouton "+" (AddEquipmentCommand, IsVisible direct sur le bouton dans WarriorEditDialog.
    /// xaml) ; ChipView.RemoveCommand n'a pas d'équivalent IsVisible, donc même bascule ici en renvoyant
    /// null - ChipView masque son "x" quand RemoveCommand est null (voir ChipView.xaml). Guerrier normal :
    /// comportement inchangé (RemoveEquipmentCommand direct).</summary>
    public ICommand? RemoveEquipmentCommandOrNull => Item.CanUseEquipment ? RemoveEquipmentCommand : null;

    [ObservableProperty]
    private Warrior item;

    [ObservableProperty]
    private string title;

    /// <summary>Règles spéciales bande-entière + propres à l'archétype de ce guerrier, déjà fusionnées et
    /// dédupliquées par l'appelant (WarbandDetailViewModel.ToRow.mergedRules, transmis via
    /// WarriorRow.SpecialRules) - lecture seule ici (se changent depuis la Bibliothèque, pas depuis ce
    /// dialog), affichées dans l'onglet Profil au même titre que sur la carte guerrier
    /// (WarbandDetailPage.xaml).</summary>
    public IReadOnlyList<SpecialRule> SpecialRules { get; }

    /// <summary>Champ texte unique pour le Mouvement - accepte un nombre ("4") ou une surcharge libre
    /// ("2D6" pour les Squigs des cavernes), résolu vers Item.Movement/Item.MovementOverride au Save
    /// selon que ça parse comme int ou non - même mécanisme qu'à la Bibliothèque
    /// (WarriorArchetypeEditDialogViewModel.MovementInput), affiché directement dans la colonne M de
    /// StatRowView plutôt qu'un champ "Surcharge Mouvement" séparé en plus.</summary>
    [ObservableProperty]
    private string movementInput;

    /// <summary>Set when Delete succeeds - the caller (WarbandDetailViewModel.EditWarrior) checks this
    /// instead of trying to SaveWarriorAsync a warrior that no longer exists.</summary>
    public bool WasDeleted { get; private set; }

    /// <summary>Onglets Profil/Équipement/Compétences/Blessures/Sorts/Mutations - même pattern toggle
    /// (pas de vrai TabbedPage) que Roster/Historique sur WarbandDetailPage, adapté à 6 sections avec un
    /// index plutôt que des bools séparés (précédent local : CurrentStep/IsStepN de
    /// EndOfGameDialogViewModel). Profil (Nom/XP/stats/Mouvement/Animal) sorti de la zone toujours
    /// visible au-dessus des onglets vers son propre onglet, par défaut - même précédent que l'onglet
    /// Profil de WarriorArchetypeEditDialog (Bibliothèque), pour désencombrer un dialog qui avait grossi
    /// jusqu'à empiler 5 champs fixes au-dessus de 5 onglets.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProfilTab))]
    [NotifyPropertyChangedFor(nameof(IsEquipmentTab))]
    [NotifyPropertyChangedFor(nameof(IsSkillsTab))]
    [NotifyPropertyChangedFor(nameof(IsInjuriesTab))]
    [NotifyPropertyChangedFor(nameof(IsSpellsTab))]
    [NotifyPropertyChangedFor(nameof(IsMutationsTab))]
    private int selectedTab;

    public bool IsProfilTab => SelectedTab == 0;
    public bool IsEquipmentTab => SelectedTab == 1;
    public bool IsSkillsTab => SelectedTab == 2;
    public bool IsInjuriesTab => SelectedTab == 3;
    public bool IsSpellsTab => SelectedTab == 4;
    public bool IsMutationsTab => SelectedTab == 5;

    /// <summary>Set by the caller (WarbandDetailViewModel.EditWarrior, which already looks up the
    /// warrior's WarriorArchetype) from WarriorArchetype.IsSpellcaster - gates the Sorts tab, hidden
    /// entirely for non-casters.</summary>
    public bool IsSpellcaster { get; }

    /// <summary>Set by the caller from WarriorArchetype.CanBuyMutations - gates the Mutations tab,
    /// hidden entirely for ordinary (non-Mutant/Possessed) archetypes.</summary>
    public bool IsMutant { get; }

    [RelayCommand]
    private void ShowProfilTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowEquipmentTab() => SelectedTab = 1;

    [RelayCommand]
    private void ShowSkillsTab() => SelectedTab = 2;

    [RelayCommand]
    private void ShowInjuriesTab() => SelectedTab = 3;

    [RelayCommand]
    private void ShowSpellsTab() => SelectedTab = 4;

    [RelayCommand]
    private void ShowMutationsTab() => SelectedTab = 5;

    /// <summary>Équipement/Compétences/Blessures/Sorts/Mutations sont toutes persistées immédiatement
    /// (pas soumises à Enregistrer/Annuler) - regroupées ici depuis la carte guerrier (WarbandDetailPage,
    /// qui reste lecture seule pour ces listes) plutôt que gérées à deux endroits différents.</summary>
    public ObservableCollection<WarriorEquipment> Equipment { get; }
    public ObservableCollection<WarriorSkill> Skills { get; }
    public ObservableCollection<WarriorInjury> Injuries { get; }
    public ObservableCollection<WarriorSpell> Spells { get; }
    public ObservableCollection<WarriorMutation> Mutations { get; }

    public WarriorEditDialogViewModel(Warrior item, string title, Warband warband, IWarbandService warbandService,
        ILibraryService libraryService, IDetailDialogService detailDialogs, IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker,
        IInjuryPickerService injuryPicker, ISpellPickerService spellPicker, bool isSpellcaster, IReadOnlyList<MagicSchool> magicSchools,
        IMutationPickerService mutationPicker, bool isMutant, IReadOnlyList<SpecialRule> specialRules,
        bool skipCosts = false)
    {
        this.item = item;
        this.title = title;
        _warband = warband;
        _warbandService = warbandService;
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _injuryPicker = injuryPicker;
        _spellPicker = spellPicker;
        IsSpellcaster = isSpellcaster;
        _magicSchools = magicSchools;
        _mutationPicker = mutationPicker;
        IsMutant = isMutant;
        SpecialRules = specialRules;
        _skipCosts = skipCosts;
        movementInput = item.MovementOverride ?? item.Movement.ToString();

        Equipment = new ObservableCollection<WarriorEquipment>(item.Equipment);
        Skills = new ObservableCollection<WarriorSkill>(item.Skills);
        Injuries = new ObservableCollection<WarriorInjury>(item.Injuries);
        Spells = new ObservableCollection<WarriorSpell>(item.Spells);
        Mutations = new ObservableCollection<WarriorMutation>(item.Mutations);
    }

    [RelayCommand]
    private async Task AddEquipment()
    {
        var items = await _equipmentPicker.PickEquipmentAsync(_warband.WarbandArchetypeId, Item.EquipmentListId, Item.WarriorArchetypeId, _warband.Treasury,
            alreadyHasFreeDagger: Equipment.Any(e => e.Item.IsFreeDagger));

        // Un seul dialog paginé pour toutes les armes de corps à corps du lot plutôt qu'une ActionSheet
        // fermée/rouverte pour chacune - voir MaterialPickerDialogViewModel. Annuler le dialog revient à
        // choisir "Normal" pour toutes (même comportement qu'annuler l'ancienne ActionSheet par arme).
        // File plutôt que Dictionary&lt;EquipmentItem, ...&gt; : items peut contenir le MÊME EquipmentItem
        // plusieurs fois (le picker permet d'acheter plusieurs exemplaires d'un même objet, voir
        // EquipmentItemViewModel.ConfirmSelection) - un dictionnaire écraserait le premier choix (ex.
        // Gromril sur la 1re épée longue) par le second (Normal sur la 2e), les deux partageant la même
        // clé. La file consomme les choix dans le même ordre que meleeItems, qui suit lui-même l'ordre de
        // items - correct même avec des objets non-armes intercalés.
        var meleeMaterials = new Queue<SpecialRule?>();
        var meleeItems = items.Where(i => i.Category == EquipmentCategory.MeleeWeapon).ToList();
        if (meleeItems.Count > 0)
        {
            var materialRules = (await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language))
                .Where(r => r.CostMultiplier.HasValue).ToList();
            if (materialRules.Count > 0)
            {
                // hasFreeDaggerSlot suit l'ordre des items comme la boucle d'achat plus bas, pour que le
                // prix affiché ici (MaterialChoice.isFreeEligible) corresponde exactement à ce qui sera
                // effectivement facturé - seule la PREMIÈRE dague du lot (existante ou dans ce même lot)
                // est éligible, achetée en "Normal" (un matériau Gromril/Ithilmar reste payant même sur
                // cette dague-là - voir MaterialChoice).
                var hasFreeDaggerSlot = Equipment.Any(e => e.Item.IsFreeDagger);
                var choices = new List<MaterialChoice>();
                foreach (var item in meleeItems)
                {
                    choices.Add(new MaterialChoice(item, materialRules, Loc["WarriorsMaterialNormal"], EquipmentPricing.IsFreeDaggerEligible(item.IsFreeDagger, hasFreeDaggerSlot)));
                    if (item.IsFreeDagger) hasFreeDaggerSlot = true;
                }
                var confirmed = await ShowDialogAsync(new MaterialPickerDialog(new MaterialPickerDialogViewModel(choices)));
                foreach (var choice in choices)
                    meleeMaterials.Enqueue(confirmed == true ? choice.SelectedMaterial : null);
            }
        }

        foreach (var equipmentItem in items)
        {
            var materialRule = equipmentItem.Category == EquipmentCategory.MeleeWeapon && meleeMaterials.Count > 0
                ? meleeMaterials.Dequeue()
                : null;

            // La première dague est gratuite, uniquement en "Normal" (livre des règles : "in addition to
            // his free dagger") - une deuxième dague, ou un matériau délibérément choisi sur celle-ci,
            // coûte le prix normal (voir EquipmentItem.IsFreeDagger/WarbandEditDialogViewModel.
            // AddEquipment pour la même logique côté wizard). Pas besoin de mémoriser "était-ce gratuit"
            // au-delà de cette déduction ponctuelle - contrairement au wizard (EquipmentPick.IsFree), le
            // trésor est débité immédiatement et définitivement ici.
            var isFreeDagger = EquipmentPricing.IsFreeDaggerEligible(equipmentItem.IsFreeDagger, Equipment.Any(e => e.Item.IsFreeDagger)) && materialRule is null;
            var cost = EquipmentPricing.CalculateCost(equipmentItem.Cost, materialRule?.CostMultiplier, isFreeDagger);

            // Sélection multiple : on paye/ajoute un par un, et on s'arrête au premier objet trop cher
            // plutôt que de tout annuler - même logique que l'ancien AddEquipment de WarbandDetailViewModel.
            // Mode Libre (_skipCosts) : aucune vérification ni déduction, même esprit que WarbandEditDialogViewModel
            // en mode Bande existante - on enregistre un historique déjà déterminé, pas un nouvel achat.
            if (!_skipCosts)
            {
                if (_warband.Treasury < cost)
                {
                    await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                    break;
                }

                _warband.Treasury -= cost;
                await _warbandService.SaveWarbandAsync(_warband);
            }

            var carried = await _warbandService.AddWarriorEquipmentAsync(Item.Id, equipmentItem, materialRule: materialRule);
            Equipment.Add(carried);
        }

        // Avertissement non-bloquant (2 armes de corps à corps / 2 armes de tir différentes max par
        // guerrier) - voir WeaponLimits/WarbandEditDialogViewModel.AddEquipment pour la même logique côté
        // wizard de création.
        if (WeaponLimits.ExceedsLimits(Equipment.Select(e => e.Item)))
            await ShowInfoAsync(Loc["WarbandsWeaponLimitWarningTitle"], string.Format(Loc["WarbandsWeaponLimitWarningMessage"], Item.Name));
    }

    [RelayCommand]
    private async Task RemoveEquipment(WarriorEquipment carried)
    {
        await _warbandService.RemoveWarriorEquipmentAsync(carried.Id);
        Equipment.Remove(carried);
    }

    /// <summary>Même recap qu'à l'étape Équipement du wizard de création (WarbandEditDialogViewModel.
    /// ShowEquipmentDetail) - inclut le matériau choisi (Gromril/Ithilmar...) dans la liste de règles
    /// spéciales affichée, pas seulement l'abréviation "(G)" du chip.</summary>
    [RelayCommand]
    private Task ShowEquipmentDetail(WarriorEquipment carried) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(carried.Item, carried.MaterialRule, carried.FoundValueOverride, carried.BlessingRule);

    // SkillEligibility.EffectiveAllowedCategories plutôt que Item.AllowedSkillCategories brut : certains
    // objets déjà portés élargissent les listes accessibles (ex. Carnet de l'Alchimiste -> Érudition,
    // voir EquipmentItem.GrantsSkillCategory) - même besoin que PickAdvanceSkill du wizard Fin de
    // Partie, ce guerrier peut tout aussi bien gagner une compétence ici, hors wizard.
    [RelayCommand]
    private async Task AddSkill()
    {
        // Résolution "en" -> ids : EffectiveExtraSkillNames est en anglais (locale-agnostic côté Core),
        // le picker lui-même travaille sur son propre catalogue localisé - même idiome que les
        // résolutions XxxByEnglishName du wizard Fin de Partie.
        var extraSkillNames = SkillEligibility.EffectiveExtraSkillNames(Item);
        var extraSkillIds = extraSkillNames.Count == 0 ? null : (await _libraryService.GetSkillsAsync("en"))
            .Where(s => extraSkillNames.Contains(s.Name)).Select(s => s.Id).ToList();

        var skills = await _skillPicker.PickSkillAsync(_warband.WarbandArchetypeId, Item.WarriorArchetypeId,
            SkillEligibility.EffectiveAllowedCategories(Item), extraSkillIds);
        foreach (var skill in skills)
        {
            var learned = await _warbandService.AddWarriorSkillAsync(Item.Id, skill);
            Skills.Add(learned);
        }
    }

    [RelayCommand]
    private async Task RemoveSkill(WarriorSkill learned)
    {
        await _warbandService.RemoveWarriorSkillAsync(learned.Id);
        Skills.Remove(learned);
    }

    [RelayCommand]
    private Task ShowSkillDetail(WarriorSkill learned) => _detailDialogs.ShowSkillDetailDialogAsync(learned.Item);

    [RelayCommand]
    private async Task AddInjury()
    {
        var injuries = await _injuryPicker.PickInjuriesAsync();
        foreach (var injury in injuries)
        {
            var tracked = await _warbandService.AddWarriorInjuryAsync(Item.Id, injury);
            Injuries.Add(tracked);
        }
    }

    [RelayCommand]
    private async Task RemoveInjury(WarriorInjury tracked)
    {
        await _warbandService.RemoveWarriorInjuryAsync(tracked.Id);
        Injuries.Remove(tracked);
    }

    [RelayCommand]
    private Task ShowInjuryDetail(WarriorInjury tracked) => _detailDialogs.ShowInjuryDetailDialogAsync(tracked.Item);

    /// <summary>Mode Libre (_skipCosts) : choix libre parmi les écoles de la bande (on enregistre un
    /// historique déjà déterminé). Sinon : tirage 1D6 via SpellRollDialog, même mécanisme que le sort de
    /// départ des recrues fraîches (WarbandEditDialogViewModel.ShowSpellRollDialog) - livre des règles,
    /// un lanceur de sorts obtient toujours un sort au hasard, jamais un choix libre. Pas de plafond ici
    /// (contrairement au sort de départ) : un guerrier déjà en jeu peut apprendre plusieurs sorts au fil
    /// des parties (Avancement).</summary>
    [RelayCommand]
    private async Task AddSpell()
    {
        if (_skipCosts)
        {
            var freeSpells = await _spellPicker.PickSpellsAsync(_magicSchools.Select(s => s.Id).ToList());
            foreach (var spell in freeSpells)
            {
                var learned = await _warbandService.AddWarriorSpellAsync(Item.Id, spell);
                Spells.Add(learned);
            }
            return;
        }

        var knownSpellIds = Spells.Select(s => s.Item.Id).ToList();
        var rolled = await ShowDialogAsync(new SpellRollDialog(new SpellRollDialogViewModel(_magicSchools.ToList(), _libraryService, _detailDialogs, knownSpellIds)));
        if (rolled is null) return;

        var rolledLearned = await _warbandService.AddWarriorSpellAsync(Item.Id, rolled);
        Spells.Add(rolledLearned);
    }

    [RelayCommand]
    private async Task RemoveSpell(WarriorSpell learned)
    {
        await _warbandService.RemoveWarriorSpellAsync(learned.Id);
        Spells.Remove(learned);
    }

    [RelayCommand]
    private Task ShowSpellDetail(WarriorSpell learned) => _detailDialogs.ShowSpellDetailDialogAsync(learned.Item);

    [RelayCommand]
    private async Task AddMutation()
    {
        var mutations = await _mutationPicker.PickMutationsAsync(_warband.WarbandArchetypeId);
        foreach (var mutation in mutations)
        {
            var bought = await _warbandService.AddWarriorMutationAsync(Item.Id, mutation);
            Mutations.Add(bought);
        }
    }

    [RelayCommand]
    private async Task RemoveMutation(WarriorMutation bought)
    {
        await _warbandService.RemoveWarriorMutationAsync(bought.Id);
        Mutations.Remove(bought);
    }

    [RelayCommand]
    private Task ShowMutationDetail(WarriorMutation bought) => _detailDialogs.ShowMutationDetailDialogAsync(bought.Item);

    /// <summary>Animal n'est pas un onglet ni une liste : c'est un simple champ 0..1 sur Item.Animal,
    /// soumis comme les stats au bouton Enregistrer/Annuler (pas de persistance immédiate ni de méthode
    /// de service dédiée - SaveWarriorAsync côté appelant écrit WarriorEntity.AnimalId). Réutilise le
    /// picker d'équipement partagé, verrouillé sur EquipmentCategory.Animal, plutôt qu'un picker dédié -
    /// une monture est juste un EquipmentItem de cette catégorie. Pas de budget passé (availableGold
    /// omis) : le choix d'une monture n'est pas traité comme un achat ici, même comportement qu'avant.</summary>
    [RelayCommand]
    private async Task SelectAnimal()
    {
        var animals = await _equipmentPicker.PickEquipmentAsync(_warband.WarbandArchetypeId, lockedCategory: EquipmentCategory.Animal);
        if (animals.Count > 0)
        {
            Item.Animal = animals[0];
            // Item lui-même (Warrior) n'implémente pas INotifyPropertyChanged - Animal est modifié en
            // place sur la même instance, donc les liaisons "Item.Animal.Name" ne se rafraîchiraient pas
            // sans ce signal explicite sur la propriété racine.
            OnPropertyChanged(nameof(Item));
        }
    }

    [RelayCommand]
    private void ClearAnimal()
    {
        Item.Animal = null;
        OnPropertyChanged(nameof(Item));
    }

    [RelayCommand]
    private Task ShowAnimalDetail() => Item.Animal is null ? Task.CompletedTask : _detailDialogs.ShowEquipmentDetailDialogAsync(Item.Animal);

    [RelayCommand]
    private async Task Delete()
    {
        // Message dédié (pas ConfirmDeleteAsync générique) : précise que ce n'est pas une mort du
        // personnage (ça se joue via Fin de partie) et que le coût est remboursé au trésor.
        if (!await ConfirmAsync(Loc["DialogDelete"], string.Format(Loc["WarriorDeleteConfirm"], Item.Name)))
            return;

        await _warbandService.DeleteWarriorAsync(Item.Id);
        WasDeleted = true;
        Close(true);
    }

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule);

    [RelayCommand]
    private void Save()
    {
        if (int.TryParse(MovementInput, out var movement))
        {
            Item.Movement = movement;
            Item.MovementOverride = null;
        }
        else
        {
            Item.MovementOverride = MovementInput;
        }

        Close(true);
    }
}
