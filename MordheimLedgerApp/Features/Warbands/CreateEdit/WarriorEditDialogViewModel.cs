using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

public partial class WarriorEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Warband _warband;
    private readonly IWarbandService _warbandService;
    private readonly ILibraryService _libraryService;
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
        ILibraryService libraryService, IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker,
        IInjuryPickerService injuryPicker, ISpellPickerService spellPicker, bool isSpellcaster, IReadOnlyList<int> allowedMagicSchoolIds,
        IMutationPickerService mutationPicker, bool isMutant, IAnimalPickerService animalPicker)
    {
        this.item = item;
        this.title = title;
        _warband = warband;
        _warbandService = warbandService;
        _libraryService = libraryService;
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
        var items = await _equipmentPicker.PickEquipmentAsync(_warband.WarbandArchetypeId, Item.EquipmentListId, Item.WarriorArchetypeId, _warband.Treasury);
        foreach (var equipmentItem in items)
        {
            // Arme de corps à corps : propose un matériau (Gromril/Ithilmar/...) avant de calculer le
            // prix - toute SpecialRule dotée d'un CostMultiplier est éligible, pas seulement ces deux-là.
            // Annuler le picker (< 0) revient à choisir "Normal".
            SpecialRule? materialRule = null;
            if (equipmentItem.Category == EquipmentCategory.MeleeWeapon)
            {
                var materialRules = (await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language))
                    .Where(r => r.CostMultiplier.HasValue).ToList();
                if (materialRules.Count > 0)
                {
                    var options = new[] { Loc["WarriorsMaterialNormal"] }.Concat(materialRules.Select(r => r.Name)).ToArray();
                    var index = await ShowActionSheetIndexAsync(Loc["WarriorsMaterialPickerTitle"], options);
                    if (index > 0) materialRule = materialRules[index - 1];
                }
            }

            var cost = equipmentItem.Cost * (materialRule?.CostMultiplier ?? 1);

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
    private async Task ShowEquipmentDetail(WarriorEquipment carried)
    {
        var equipmentItem = carried.Item;
        var language = LocalizationService.Instance.Language;
        var categoryLabel = Loc[$"EquipmentCategory{equipmentItem.Category}"];

        var restrictedWarbands = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarbandArchetype>()
            : (await _libraryService.GetWarbandArchetypesAsync(language))
                .Where(w => equipmentItem.RestrictedToWarbandArchetypeIds.Contains(w.Id)).ToList();

        var restrictedWarriors = equipmentItem.RestrictedToWarbandArchetypeIds.Count == 0 || equipmentItem.RestrictedToWarriorArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(equipmentItem.RestrictedToWarbandArchetypeIds, language))
                .Where(w => equipmentItem.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        await ShowDialogAsync(new EquipmentItemDetailDialog(
            new EquipmentItemDetailDialogViewModel(equipmentItem, categoryLabel, restrictedWarbands, restrictedWarriors, carried.MaterialRule)));
    }

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
