using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Skills.CreateEdit;
using MordheimLedgerApp.Features.Library.Skills.Effects;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Skills;

public partial class SkillViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly ISkillPickerNavigationService _pickerNavigation;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly IWarriorArchetypePickerService _warriorPicker;
    private List<Skill> _allItems = new();
    private List<WarbandArchetype> _warbandArchetypes = new();

    /// <summary>Sections of the grid, one per SkillCategory - always grouped internally (cf.
    /// SpellViewModel.SpellGroups), the header is just hidden outside the "All" filter where it'd be
    /// redundant with the filter button already shown above (see ShowGroupHeaders).</summary>
    [ObservableProperty]
    private PagedGroupCollection<SkillGroup, SkillRow> skillGroups = new();

    /// <summary>Group headers are redundant once a single category is picked (its name is already on
    /// the filter button) - only shown for the "All" filter, cf. SpellViewModel.ShowGroupHeaders.</summary>
    public bool ShowGroupHeaders => SelectedCategory is null;

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private SkillRow? selectedRow;

    /// <summary>Le vrai critère de filtre - null = Tout. Distinct de SelectedCategoryLabel (juste
    /// l'affichage) : comparer des libellés résolus par LocalizationService cassait le filtre au
    /// changement de langue (le libellé "Tout" figé dans l'ancienne langue ne correspondait plus à
    /// rien après un rechargement en LoadData, filtrant silencieusement la liste à vide).</summary>
    [ObservableProperty]
    private SkillCategory? selectedCategory;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Set by SkillSelectorPage right after construction - même bascule multi-sélection
    /// qu'EquipmentItemViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par ApplyFilter.</summary>
    public ObservableCollection<SkillRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    /// <summary>Restreint le picker à au plus UNE compétence à la fois (2026-09-04, retour utilisateur -
    /// "dans la section des advance, il faut passer en sélection unique") : un jet de Progression
    /// n'accorde jamais plus d'une compétence, contrairement au picker générique de la Bibliothèque
    /// (ajout de plusieurs compétences d'un coup à un guerrier - WarbandEditDialogViewModel/
    /// WarriorEditDialogViewModel/DramatisPersonaEditDialogViewModel, qui laissent ce flag à false). Set
    /// par SkillPickerService avant construction (même idiome qu'AllowedCategories). Ne change pas
    /// l'interaction "tap + bouton Confirmer" - juste : taper une nouvelle compétence désélectionne
    /// automatiquement la précédente plutôt que de les cumuler.</summary>
    public bool SingleSelectMode { get; set; }

    /// <summary>Guerrier qui recevra la compétence (fiche guerrier, Progression du wizard) - null ailleurs.
    /// Sert seulement au message ambre des prérequis (PrerequisiteWarning) : toutes les compétences restent
    /// visibles et choisissables, comme les branches d'Exploration.</summary>
    public Warrior? ForWarrior { get; set; }
    public bool ForWarriorIsSpellcaster { get; set; }

    /// <summary>ForWarrior vu par les règles (bande et type résolus en OfficialId au chargement).</summary>
    private EffectSubject? _prerequisiteSubject;
    private SkillEffectNames _prerequisiteNames = SkillEffectNames.Empty;

    /// <summary>Message ambre sous la liste : un paragraphe par prérequis non rempli parmi les compétences
    /// cochées (Core.Rules.SkillPrerequisites.Unmet) - vide si rien à signaler.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrerequisiteWarning))]
    private string prerequisiteWarning = string.Empty;

    public bool HasPrerequisiteWarning => PrerequisiteWarning.Length > 0;

    /// <summary>Null (Library CRUD tab) = no filter. Non-null (picker mode, set by SkillPickerService
    /// before construction) = only skills whose RestrictedToWarbandArchetypeIds is empty (common) or
    /// contains this id are shown - see WarriorEditDialogViewModel.AddSkill/EndOfGamePageViewModel.</summary>
    public int? AllowedWarbandArchetypeId { get; set; }

    /// <summary>Same idea as AllowedWarbandArchetypeId, one level down: only skills whose
    /// RestrictedToWarriorArchetypeIds is empty (any warrior of the restricted warband) or contains
    /// this id are shown - lets e.g. Orc Mob's "Da Cunnin' Plan" only appear when picking for the Orc
    /// Boss specifically, not every Orc warrior.</summary>
    public int? AllowedWarriorArchetypeId { get; set; }

    /// <summary>Null (Library CRUD tab) = no filter. Non-null (picker mode, set by SkillPickerService
    /// before construction) = only skills whose Category is in this list are shown - the warband's
    /// per-warrior "skill table" (WarriorArchetype/Warrior.AllowedSkillCategories), e.g. a Bear Tamer
    /// only ever sees Combat/Strength/Speed skills, never Academic ones.</summary>
    public IReadOnlyList<SkillCategory>? AllowedCategories { get; set; }

    /// <summary>Null (the common case) = no extra skills beyond AllowedCategories. Non-null (set by
    /// SkillPickerService when a carried item unlocks one specific skill outside the Warrior's normal
    /// categories, see Core.Rules.SkillEligibility.EffectiveExtraSkillOfficialIds) = these Skill ids are shown
    /// too, regardless of Category - an "OR" alongside AllowedCategories, not a further narrowing.</summary>
    public IReadOnlyList<int>? AllowedExtraSkillIds { get; set; }

    public SkillViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs, ISkillPickerNavigationService pickerNavigation,
        IWarbandArchetypePickerService warbandPicker, IWarriorArchetypePickerService warriorPicker)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _pickerNavigation = pickerNavigation;
        _warbandPicker = warbandPicker;
        _warriorPicker = warriorPicker;
        selectedCategoryLabel = Loc["LibFilterAll"];

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((SkillViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetSkillsAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        await LoadPrerequisiteSubjectAsync();
        RefreshSelectedCategoryLabel();
        ApplyFilter();
    }

    private async Task LoadPrerequisiteSubjectAsync()
    {
        if (ForWarrior is not { } warrior)
        {
            _prerequisiteSubject = null;
            return;
        }
        var language = LocalizationService.Instance.Language;
        var warband = _warbandArchetypes.FirstOrDefault(w => w.Id == AllowedWarbandArchetypeId);
        var warriorArchetype = warband is null || warrior.WarriorArchetypeId is not { } archetypeId
            ? null
            : (await _libraryService.GetWarriorArchetypesAsync(warband.Id, language)).FirstOrDefault(a => a.Id == archetypeId);
        _prerequisiteSubject = new EffectSubject(warrior, IsSpellcaster: ForWarriorIsSpellcaster,
            WarbandOfficialId: warband?.OfficialId, WarriorArchetypeOfficialId: warriorArchetype?.OfficialId);
        _prerequisiteNames = await SkillEffectNames.LoadAsync(_libraryService, language);
    }

    /// <summary>Texte de chaque prérequis non rempli par ForWarrior parmi les compétences cochées.</summary>
    private void RefreshPrerequisiteWarning()
    {
        if (_prerequisiteSubject is not { } subject)
        {
            PrerequisiteWarning = string.Empty;
            return;
        }
        var warriorName = string.IsNullOrWhiteSpace(subject.Warrior.Name) ? Loc["SkillPrereqThisWarrior"] : subject.Warrior.Name;
        var lines = SelectedRows
            .SelectMany(row => SkillPrerequisites.Unmet(row.Item, subject).Select(effect => (row.Item.Name, effect)))
            .Select(p => PrerequisiteText(p.Name, warriorName, p.effect));
        PrerequisiteWarning = string.Join(Environment.NewLine, lines);
    }

    private string PrerequisiteText(string skillName, string warriorName, SkillEffect effect)
    {
        if (!string.IsNullOrWhiteSpace(effect.Note)) return $"{skillName} : {effect.Note.Trim()}";
        var targets = string.Join(", ", effect.ConditionTargets.Select(t => _prerequisiteNames.NameOf(t)));
        return effect.Condition switch
        {
            EffectCondition.Leader => string.Format(Loc["SkillPrereqLeader"], skillName, warriorName),
            EffectCondition.Spellcaster => string.Format(Loc["SkillPrereqSpellcaster"], skillName, warriorName),
            EffectCondition.NotInWarbandOrArchetype => string.Format(Loc["SkillPrereqExcluded"], skillName, warriorName, targets),
            EffectCondition.CarriesItem => string.Format(Loc["SkillPrereqCarries"], skillName, warriorName, targets),
            _ => string.Format(Loc["SkillPrereqOther"], skillName, warriorName, Loc[$"EffectCondition{effect.Condition}"]),
        };
    }

    /// <summary>Recompute le libellé affiché depuis SelectedCategory (le vrai critère) - à refaire à
    /// chaque LoadData puisque le texte dépend de la langue courante.</summary>
    private void RefreshSelectedCategoryLabel() =>
        SelectedCategoryLabel = SelectedCategory is { } category ? CategoryLabel(category) : Loc["LibFilterAll"];

    partial void OnSelectedCategoryChanged(SkillCategory? value) => OnPropertyChanged(nameof(ShowGroupHeaders));

    private void ApplyFilter()
    {
        IEnumerable<Skill> filtered = _allItems;
        if (SelectedCategory is { } category)
            filtered = filtered.Where(i => i.Category == category);
        if (AllowedWarbandArchetypeId is { } warbandId)
            filtered = filtered.Where(i => i.RestrictedToWarbandArchetypeIds.Count == 0 || i.RestrictedToWarbandArchetypeIds.Contains(warbandId));
        if (AllowedWarriorArchetypeId is { } warriorId)
            filtered = filtered.Where(i => i.RestrictedToWarriorArchetypeIds.Count == 0 || i.RestrictedToWarriorArchetypeIds.Contains(warriorId));
        if (AllowedCategories is { } categories)
            filtered = filtered.Where(i => categories.Contains(i.Category) || (AllowedExtraSkillIds?.Contains(i.Id) ?? false));

        var groups = new ObservableCollection<SkillGroup>();
        foreach (var item in filtered)
        {
            var groupName = CategoryLabel(item.Category);
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group is null)
            {
                group = new SkillGroup(groupName);
                groups.Add(group);
            }
            group.Add(new SkillRow(item));
        }
        SkillGroups = new PagedGroupCollection<SkillGroup, SkillRow>(groups, g => new SkillGroup(g.Name));

        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
        RefreshPrerequisiteWarning();
    }

    private string CategoryLabel(SkillCategory category) => Loc[$"SkillCategory{category}"];

    partial void OnSelectedRowChanged(SkillRow? oldValue, SkillRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(SkillRow row)
    {
        if (!IsSelectorMode)
        {
            SelectedRow = row;
            return;
        }

        // SingleSelectMode : taper une compétence PAS encore sélectionnée vide d'abord la sélection en
        // cours (au plus une ligne) - taper la compétence déjà sélectionnée reste une simple bascule
        // (désélection), même comportement que le multi-select pour ce cas précis.
        if (SingleSelectMode && !row.IsSelected)
        {
            foreach (var selected in SelectedRows.ToList())
                selected.IsSelected = false;
            SelectedRows.Clear();
        }

        row.IsSelected = !row.IsSelected;
        if (row.IsSelected) SelectedRows.Add(row);
        else SelectedRows.Remove(row);
        OnPropertyChanged(nameof(HasSelectedRows));
        RefreshPrerequisiteWarning();
    }

    [RelayCommand]
    private async Task SelectCategory()
    {
        var categories = AllowedCategories?.ToArray() ?? Enum.GetValues<SkillCategory>();
        var options = new[] { Loc["LibFilterAll"] }.Concat(categories.Select(CategoryLabel)).ToArray();

        var index = await ShowActionSheetIndexAsync(Loc["LibFilterCategory"], options);
        if (index < 0) return;

        SelectedCategory = index == 0 ? null : categories[index - 1];
        RefreshSelectedCategoryLabel();
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new Skill();
        var effectNames = await SkillEffectNames.LoadAsync(_libraryService, LocalizationService.Instance.Language);
        var dialogViewModel = new SkillEditDialogViewModel(newItem, Loc["SkillCreateTitle"], _warbandPicker,
            _warriorPicker, _warbandArchetypes, Array.Empty<WarriorArchetype>(), effectNames);
        if (await ShowDialogAsync(new SkillEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSkillAsync(newItem, LocalizationService.Instance.Language);
        _allItems.Add(newItem);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new Skill
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            OfficialId = s.OfficialId,
            ImagePath = s.ImagePath,
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds),
            RestrictedToWarriorArchetypeIds = new List<int>(s.RestrictedToWarriorArchetypeIds),
            // Recopiés eux aussi : la sauvegarde réécrit la ligne entière, une copie sans eux effaçait la
            // Haine accordée par la compétence (et désormais ses effets) à la première édition.
            HatredTargetWarbandArchetypeIds = new List<int>(s.HatredTargetWarbandArchetypeIds),
            Effects = s.Effects.Select(e => e.Clone()).ToList()
        };

        var initialWarriors = copy.RestrictedToWarbandArchetypeIds.Count == 0
            ? new List<WarriorArchetype>()
            : (await _libraryService.GetWarriorArchetypesAsync(copy.RestrictedToWarbandArchetypeIds, LocalizationService.Instance.Language))
                .Where(w => copy.RestrictedToWarriorArchetypeIds.Contains(w.Id)).ToList();

        var effectNames = await SkillEffectNames.LoadAsync(_libraryService, LocalizationService.Instance.Language);
        var dialogViewModel = new SkillEditDialogViewModel(copy, Loc["SkillEditTitle"], _warbandPicker,
            _warriorPicker, _warbandArchetypes, initialWarriors, effectNames);
        if (await ShowDialogAsync(new SkillEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveSkillAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteSkillAsync(row.Item.Id);
        _allItems.Remove(row.Item);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<Skill>());

    /// <summary>Read-only recap popup (tile info button). AllowConcurrentExecutions : voir
    /// WarbandArchetypeViewModel.ShowDetails.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(SkillRow row) => _detailDialogs.ShowSkillDetailDialogAsync(row.Item);
}
