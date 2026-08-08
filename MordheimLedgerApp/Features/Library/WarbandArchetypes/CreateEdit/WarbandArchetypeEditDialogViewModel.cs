using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentLists.CreateEdit;
using MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarbandArchetypes.CreateEdit;

/// <summary>3 onglets (Général/Guerriers/Équipement), même visuel que WarbandArchetypeDetailDialog mais
/// modifiable. Guerriers/Équipement sont édités entièrement en mémoire (comme SpecialRules/MagicSchools
/// déjà avant ce changement) - aucun appel service tant qu'on n'a pas cliqué Enregistrer, voir Save().
/// Nécessaire car WarriorArchetype.WarbandArchetypeId/EquipmentList.WarbandArchetypeId sont des int non
/// nullables : pour une bande en cours de création (Item.Id == 0), impossible d'insérer un guerrier/une
/// liste "flottants" avant que la bande elle-même existe en base - persister immédiatement obligerait à
/// sauvegarder la bande en douce dès le premier ajout, ce qui casserait Annuler (la bande resterait en
/// base malgré tout). Tout différé au Save évite ce problème : Annuler à n'importe quel moment ne laisse
/// jamais rien en base.</summary>
public partial class WarbandArchetypeEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, WarbandGrade> _gradeByLabel = new();
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IMagicSchoolPickerService _magicSchoolPicker;
    private readonly ILibraryService _libraryService;
    private readonly IEquipmentPickerService _equipmentPicker;

    private bool _warriorsLoaded;
    private bool _equipmentListsLoaded;

    /// <summary>Ids des guerriers/listes qui existaient déjà en base (Id != 0) et ont été retirés de
    /// Warriors/EquipmentLists pendant cette session d'édition - une ObservableCollection seule ne
    /// suffit pas à distinguer "jamais existé" de "existait, supprimé ici", nécessaire pour savoir quoi
    /// supprimer réellement au Save (voir Save()).</summary>
    private readonly List<int> _removedWarriorIds = new();
    private readonly List<int> _removedEquipmentListIds = new();

    protected override bool CancelResult => false;

    [ObservableProperty]
    private WarbandArchetype item;

    [ObservableProperty]
    private string title;

    public ObservableCollection<string> GradeOptions { get; } = new();

    [ObservableProperty]
    private string selectedGradeLabel = string.Empty;

    /// <summary>Édité en mémoire ici, recopié sur Item.SpecialRules à la sauvegarde (voir Save) - même
    /// principe que WarriorArchetypeEditDialogViewModel.SpecialRules.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    /// <summary>Édité en mémoire ici, recopié sur Item.MagicSchools à la sauvegarde - même principe que
    /// SpecialRules. Détermine quels sorts sont proposés aux casters de cette bande (voir
    /// WarriorEditDialogViewModel.AddSpell).</summary>
    public ObservableCollection<MagicSchool> MagicSchools { get; }

    /// <summary>Objets complets (pas NamedRef comme WarbandArchetypeDetailDialogViewModel) : ici,
    /// quasiment toutes les interactions (tap = éditer) ouvrent de toute façon l'objet complet - un
    /// aller-retour Id+Nom puis re-fetch au tap serait un fetch en plus, pas en moins, dans ce contexte
    /// d'édition.</summary>
    [ObservableProperty]
    private ObservableCollection<WarriorArchetype> warriors = new();

    [ObservableProperty]
    private ObservableCollection<EquipmentList> equipmentLists = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsWarriorsTab))]
    [NotifyPropertyChangedFor(nameof(IsEquipmentTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsWarriorsTab => SelectedTab == 1;
    public bool IsEquipmentTab => SelectedTab == 2;

    public WarbandArchetypeEditDialogViewModel(WarbandArchetype item, string title, ISpecialRulePickerService specialRulePicker,
        IMagicSchoolPickerService magicSchoolPicker, ILibraryService libraryService, IEquipmentPickerService equipmentPicker)
    {
        this.item = item;
        this.title = title;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;
        _libraryService = libraryService;
        _equipmentPicker = equipmentPicker;
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
        MagicSchools = new ObservableCollection<MagicSchool>(item.MagicSchools);

        foreach (var grade in Enum.GetValues<WarbandGrade>())
        {
            var label = Loc[$"WarbandGrade{grade}"];
            _gradeByLabel[label] = grade;
            GradeOptions.Add(label);
        }
        selectedGradeLabel = Loc[$"WarbandGrade{item.Grade}"];
    }

    partial void OnSelectedGradeLabelChanged(string value)
    {
        if (_gradeByLabel.TryGetValue(value, out var grade))
            Item.Grade = grade;
    }

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private async Task ShowMagicSchoolDetail(MagicSchool school)
    {
        var language = LocalizationService.Instance.Language;
        var spells = (await _libraryService.GetSpellsAsync(language)).Where(s => s.MagicSchoolId == school.Id).ToList();
        await ShowChipDetailAsync(school.Name, school.Description, spells);
    }

    [RelayCommand]
    private async Task AddSpecialRule()
    {
        var picked = await _specialRulePicker.PickSpecialRulesAsync(SpecialRuleScope.Warband);
        foreach (var rule in picked)
        {
            if (SpecialRules.Any(r => r.Id == rule.Id)) continue;
            SpecialRules.Add(rule);
        }
    }

    [RelayCommand]
    private void RemoveSpecialRule(SpecialRule rule) => SpecialRules.Remove(rule);

    [RelayCommand]
    private async Task AddMagicSchool()
    {
        var picked = await _magicSchoolPicker.PickMagicSchoolsAsync();
        foreach (var school in picked)
        {
            if (MagicSchools.Any(s => s.Id == school.Id)) continue;
            MagicSchools.Add(school);
        }
    }

    [RelayCommand]
    private void RemoveMagicSchool(MagicSchool school) => MagicSchools.Remove(school);

    [RelayCommand]
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private async Task ShowWarriorsTab()
    {
        SelectedTab = 1;
        await EnsureWarriorsLoadedAsync();
    }

    [RelayCommand]
    private async Task ShowEquipmentTab()
    {
        SelectedTab = 2;
        await EnsureEquipmentListsLoadedAsync();
    }

    /// <summary>Item.Id == 0 (bande en cours de création) : collection vide de départ, rien à charger.
    /// Sinon (édition d'une bande existante) : charge les guerriers réellement en base.</summary>
    private async Task EnsureWarriorsLoadedAsync()
    {
        if (_warriorsLoaded) return;
        if (Item.Id != 0)
        {
            var language = LocalizationService.Instance.Language;
            await Loading.RunAsync(async () =>
            {
                var loaded = await Task.Run(() => _libraryService.GetWarriorArchetypesAsync(Item.Id, language));
                Warriors = new ObservableCollection<WarriorArchetype>(loaded);
            });
        }
        _warriorsLoaded = true;
    }

    private async Task EnsureEquipmentListsLoadedAsync()
    {
        if (_equipmentListsLoaded) return;
        if (Item.Id != 0)
        {
            var language = LocalizationService.Instance.Language;
            await Loading.RunAsync(async () =>
            {
                var loaded = await Task.Run(() => _libraryService.GetEquipmentListsAsync(Item.Id, language));
                EquipmentLists = new ObservableCollection<EquipmentList>(loaded);
            });
        }
        _equipmentListsLoaded = true;
    }

    [RelayCommand]
    private async Task AddWarrior()
    {
        // EquipmentListId (picker de liste de départ) a besoin des listes de la bande, y compris celles
        // ajoutées en mémoire pendant cette même session - garanti chargé même si l'utilisateur n'est
        // jamais passé par l'onglet Équipement.
        await EnsureEquipmentListsLoadedAsync();

        var newItem = new WarriorArchetype();
        var dialogViewModel = new WarriorArchetypeEditDialogViewModel(newItem, Loc["WarriorArchetypeCreateTitle"],
            _specialRulePicker, EquipmentLists.ToList());
        if (await ShowDialogAsync(new WarriorArchetypeEditDialog(dialogViewModel)) != true) return;

        Warriors.Add(newItem);
    }

    [RelayCommand]
    private async Task EditWarrior(WarriorArchetype warrior)
    {
        await EnsureEquipmentListsLoadedAsync();

        // Copie manuelle (comme WarriorArchetypeViewModel.Edit) : Annuler côté guerrier doit laisser le
        // chip affiché intact, pas de mutation en place tant que ce dialog imbriqué n'a pas confirmé.
        var copy = new WarriorArchetype
        {
            Id = warrior.Id,
            WarbandArchetypeId = warrior.WarbandArchetypeId,
            Name = warrior.Name,
            IsHero = warrior.IsHero,
            Cost = warrior.Cost,
            Source = warrior.Source,
            MaxCount = warrior.MaxCount,
            Movement = warrior.Movement,
            MovementOverride = warrior.MovementOverride,
            WeaponSkill = warrior.WeaponSkill,
            BallisticSkill = warrior.BallisticSkill,
            Strength = warrior.Strength,
            Toughness = warrior.Toughness,
            Wounds = warrior.Wounds,
            Initiative = warrior.Initiative,
            Attacks = warrior.Attacks,
            Leadership = warrior.Leadership,
            StartingExperience = warrior.StartingExperience,
            Description = warrior.Description,
            NameKey = warrior.NameKey,
            DescriptionKey = warrior.DescriptionKey,
            ImagePath = warrior.ImagePath,
            SpecialRules = new List<SpecialRule>(warrior.SpecialRules),
            IsSpellcaster = warrior.IsSpellcaster,
            CanBuyMutations = warrior.CanBuyMutations,
            EquipmentListId = warrior.EquipmentListId,
            AllowedSkillCategories = new List<SkillCategory>(warrior.AllowedSkillCategories)
        };

        var dialogViewModel = new WarriorArchetypeEditDialogViewModel(copy, Loc["WarriorArchetypeEditTitle"],
            _specialRulePicker, EquipmentLists.ToList());
        if (await ShowDialogAsync(new WarriorArchetypeEditDialog(dialogViewModel)) != true) return;

        var index = Warriors.IndexOf(warrior);
        if (index >= 0) Warriors[index] = copy;
    }

    [RelayCommand]
    private void RemoveWarrior(WarriorArchetype warrior)
    {
        Warriors.Remove(warrior);
        if (warrior.Id != 0) _removedWarriorIds.Add(warrior.Id);
    }

    [RelayCommand]
    private async Task AddEquipmentList()
    {
        var newItem = new EquipmentList();
        var dialogViewModel = new EquipmentListEditDialogViewModel(newItem, Loc["EquipmentListCreateTitle"],
            _equipmentPicker, Array.Empty<EquipmentItem>());
        if (await ShowDialogAsync(new EquipmentListEditDialog(dialogViewModel)) != true) return;

        EquipmentLists.Add(newItem);
    }

    [RelayCommand]
    private async Task EditEquipmentList(EquipmentList list)
    {
        var copy = new EquipmentList
        {
            Id = list.Id,
            WarbandArchetypeId = list.WarbandArchetypeId,
            Name = list.Name,
            NameKey = list.NameKey,
            Source = list.Source,
            ItemIds = new List<int>(list.ItemIds)
        };

        var language = LocalizationService.Instance.Language;
        var initialItems = await _libraryService.GetEquipmentItemsAsync(copy.ItemIds, language);
        var dialogViewModel = new EquipmentListEditDialogViewModel(copy, Loc["EquipmentListEditTitle"],
            _equipmentPicker, initialItems);
        if (await ShowDialogAsync(new EquipmentListEditDialog(dialogViewModel)) != true) return;

        var index = EquipmentLists.IndexOf(list);
        if (index >= 0) EquipmentLists[index] = copy;
    }

    [RelayCommand]
    private void RemoveEquipmentList(EquipmentList list)
    {
        EquipmentLists.Remove(list);
        if (list.Id != 0) _removedEquipmentListIds.Add(list.Id);
    }

    /// <summary>Seul point d'écriture en base de tout le dialog - la bande d'abord (Item.Id devient réel
    /// si c'était une création), puis les guerriers/listes en attente avec Item.Id réassigné, puis les
    /// suppressions en attente. Le dialog est donc auto-persistant (contrairement à la plupart des autres
    /// dialogs Edit de l'app où l'appelant persiste après fermeture) - le seul dont le Save doit
    /// orchestrer plusieurs entités liées par clé étrangère, pas juste un item + ses listes de
    /// références.</summary>
    [RelayCommand]
    private async Task Save()
    {
        Item.SpecialRules = SpecialRules.ToList();
        Item.MagicSchools = MagicSchools.ToList();

        var language = LocalizationService.Instance.Language;
        await Loading.RunAsync(async () =>
        {
            await _libraryService.SaveWarbandArchetypeAsync(Item, language);

            foreach (var warrior in Warriors)
            {
                warrior.WarbandArchetypeId = Item.Id;
                await _libraryService.SaveWarriorArchetypeAsync(warrior, language);
            }
            foreach (var id in _removedWarriorIds)
                await _libraryService.DeleteWarriorArchetypeAsync(id);

            foreach (var list in EquipmentLists)
            {
                list.WarbandArchetypeId = Item.Id;
                await _libraryService.SaveEquipmentListAsync(list, language);
            }
            foreach (var id in _removedEquipmentListIds)
                await _libraryService.DeleteEquipmentListAsync(id);
        });

        Close(true);
    }
}
