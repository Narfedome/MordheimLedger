using System.Collections.ObjectModel;
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
    private readonly IReadOnlyList<int> _allowedMagicSchoolIds;
    private readonly IMutationPickerService _mutationPicker;
    private readonly IAnimalPickerService _animalPicker;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private Warrior item;

    [ObservableProperty]
    private string title;

    /// <summary>Set when Delete succeeds - the caller (WarbandDetailViewModel.EditWarrior) checks this
    /// instead of trying to SaveWarriorAsync a warrior that no longer exists.</summary>
    public bool WasDeleted { get; private set; }

    /// <summary>Onglets Équipement/Compétences/Blessures/Sorts/Mutations - même pattern toggle (pas de
    /// vrai TabbedPage) que Roster/Historique sur WarbandDetailPage, adapté à 5 sections avec un index
    /// plutôt que des bools séparés (précédent local : CurrentStep/IsStepN de EndOfGameDialogViewModel).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEquipmentTab))]
    [NotifyPropertyChangedFor(nameof(IsSkillsTab))]
    [NotifyPropertyChangedFor(nameof(IsInjuriesTab))]
    [NotifyPropertyChangedFor(nameof(IsSpellsTab))]
    [NotifyPropertyChangedFor(nameof(IsMutationsTab))]
    private int selectedTab;

    public bool IsEquipmentTab => SelectedTab == 0;
    public bool IsSkillsTab => SelectedTab == 1;
    public bool IsInjuriesTab => SelectedTab == 2;
    public bool IsSpellsTab => SelectedTab == 3;
    public bool IsMutationsTab => SelectedTab == 4;

    /// <summary>Set by the caller (WarbandDetailViewModel.EditWarrior, which already looks up the
    /// warrior's WarriorArchetype) from WarriorArchetype.IsSpellcaster - gates the Sorts tab, hidden
    /// entirely for non-casters.</summary>
    public bool IsSpellcaster { get; }

    /// <summary>Set by the caller from WarriorArchetype.CanBuyMutations - gates the Mutations tab,
    /// hidden entirely for ordinary (non-Mutant/Possessed) archetypes.</summary>
    public bool IsMutant { get; }

    [RelayCommand]
    private void ShowEquipmentTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowSkillsTab() => SelectedTab = 1;

    [RelayCommand]
    private void ShowInjuriesTab() => SelectedTab = 2;

    [RelayCommand]
    private void ShowSpellsTab() => SelectedTab = 3;

    [RelayCommand]
    private void ShowMutationsTab() => SelectedTab = 4;

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
        IInjuryPickerService injuryPicker, ISpellPickerService spellPicker, bool isSpellcaster, IReadOnlyList<int> allowedMagicSchoolIds,
        IMutationPickerService mutationPicker, bool isMutant, IAnimalPickerService animalPicker)
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
        _allowedMagicSchoolIds = allowedMagicSchoolIds;
        _mutationPicker = mutationPicker;
        IsMutant = isMutant;
        _animalPicker = animalPicker;

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
            if (_warband.Treasury < cost)
            {
                await ShowInfoAsync(Loc["WarbandsInsufficientFundsTitle"], Loc["WarbandsInsufficientFundsMessage"]);
                break;
            }

            _warband.Treasury -= cost;
            await _warbandService.SaveWarbandAsync(_warband);

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
        _detailDialogs.ShowEquipmentDetailDialogAsync(carried.Item, carried.MaterialRule);

    [RelayCommand]
    private async Task AddSkill()
    {
        var skills = await _skillPicker.PickSkillAsync(_warband.WarbandArchetypeId, Item.WarriorArchetypeId, Item.AllowedSkillCategories);
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

    [RelayCommand]
    private async Task AddSpell()
    {
        var spells = await _spellPicker.PickSpellsAsync(_allowedMagicSchoolIds);
        foreach (var spell in spells)
        {
            var learned = await _warbandService.AddWarriorSpellAsync(Item.Id, spell);
            Spells.Add(learned);
        }
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
    /// de service dédiée - SaveWarriorAsync côté appelant écrit WarriorEntity.AnimalId).</summary>
    [RelayCommand]
    private async Task SelectAnimal()
    {
        var animals = await _animalPicker.PickAnimalsAsync();
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
    private Task ShowAnimalDetail() => Item.Animal is null ? Task.CompletedTask : _detailDialogs.ShowAnimalDetailDialogAsync(Item.Animal);

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
    private void Save() => Close(true);
}
