using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems;

public partial class EquipmentItemViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IEquipmentPickerNavigationService _pickerNavigation;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private List<EquipmentItem> _allItems = new();
    private List<WarbandArchetype> _warbandArchetypes = new();

    /// <summary>Sections of the grid, one per EquipmentCategory - always grouped internally (cf.
    /// SpellViewModel.SpellGroups), the header is just hidden outside the "All" filter where it'd be
    /// redundant with the filter button already shown above (see ShowGroupHeaders).</summary>
    [ObservableProperty]
    private ObservableCollection<EquipmentItemGroup> equipmentItemGroups = new();

    /// <summary>Group headers are redundant once a single category is picked (its name is already on
    /// the filter button) - only shown for the "All" filter, cf. SpellViewModel.ShowGroupHeaders.</summary>
    public bool ShowGroupHeaders => SelectedCategory is null;

    // IsSelected porté par la ligne (SelectionMode="None"), pas la sélection native - cf.
    // SelectableGridItemBorderStyle.
    [ObservableProperty]
    private EquipmentItemRow? selectedRow;

    /// <summary>Le vrai critère de filtre - null = Tout. Distinct de SelectedCategoryLabel (juste
    /// l'affichage) : comparer des libellés résolus par LocalizationService cassait le filtre au
    /// changement de langue (le libellé "Tout" figé dans l'ancienne langue ne correspondait plus à
    /// rien après un rechargement en LoadData, filtrant silencieusement la liste à vide).</summary>
    [ObservableProperty]
    private EquipmentCategory? selectedCategory;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Set by EquipmentItemSelectorPage right after construction: en mode picker, un tap
    /// bascule la sélection d'une ligne (liseré bleu, plusieurs lignes à la fois) au lieu du
    /// remplacement à une seule ligne utilisé par l'onglet Library (IsCrud) pour Éditer/Supprimer.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par ApplyFilter.</summary>
    public ObservableCollection<EquipmentItemRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0;

    /// <summary>Null (Library CRUD tab) = no filter. Non-null (picker mode, set by
    /// EquipmentPickerService before construction) = picker mode is active. Only directly consulted for
    /// filtering when AllowedEquipmentListItemIds is null (the EquipmentList editor's "add item"
    /// picker) - see ApplyFilter.</summary>
    public int? AllowedWarbandArchetypeId { get; set; }

    /// <summary>Narrows RestrictedToWarriorArchetypeIds-tagged items within either channel below to just
    /// this warrior - null = no further narrowing (e.g. the EquipmentList editor's own "add item"
    /// picker, which isn't scoped to one specific warrior).</summary>
    public int? AllowedWarriorArchetypeId { get; set; }

    /// <summary>Set only by the roster recruit/purchase picker (EquipmentPickerService, when the
    /// recruit has an assigned EquipmentList) - member item ids of that list. When set, this is the
    /// SOLE source of truth for what the recruit may buy: the list is what "usable by this warrior"
    /// means, so Rare items a warrior can access must themselves be added as list members (with
    /// RestrictedToWarriorArchetypeIds narrowing them within a list shared by several archetypes, e.g.
    /// Holy Tome in the Witch Hunter list - Warrior-Priest only). AllowedWarbandArchetypeId is only
    /// consulted when this is null, i.e. the EquipmentList editor's own "add item" picker, which has no
    /// list of its own yet and needs the broad "common + this band's own items" browse instead.</summary>
    public HashSet<int>? AllowedEquipmentListItemIds { get; set; }

    /// <summary>Set only by the roster recruit/purchase picker (EquipmentPickerService) when a gold
    /// budget applies to this purchase - null (Library CRUD tab, EquipmentList editor's own "add item"
    /// picker) hides BudgetDisplay entirely. The gold available BEFORE this picker session, not yet
    /// debited.</summary>
    public int? AvailableGold { get; set; }

    /// <summary>Cost multiplier applied to BudgetDisplay's running total - a henchman row's effectif for
    /// a grouped purchase (WarbandEditDialogViewModel.AddEquipment), 1 for an individual purchase.</summary>
    public int UnitCount { get; set; } = 1;

    /// <summary>Set by the caller alongside AvailableGold - true when the target (warrior/henchman group)
    /// already carries a free dagger (EquipmentItem.IsFreeDagger), so the Dagger tile shows its normal
    /// price instead of "Gratuit"/"Free" - see EquipmentItemRow.IsFreeForThisPurchase.</summary>
    public bool AlreadyHasFreeDagger { get; set; }

    /// <summary>Set by EquipmentPickerService (before LoadData) when the caller wants the picker locked
    /// onto one category - e.g. WarriorEditDialogViewModel.SelectAnimal reuses this same picker pre-
    /// filtered to Animals instead of a separate Animal-only picker. Non-null hides the category-change
    /// button (see EquipmentItemView.xaml) and forces SelectedCategory to this value in LoadData.</summary>
    public EquipmentCategory? LockedCategory { get; set; }

    public bool IsCategoryLocked => LockedCategory.HasValue;

    public bool ShowBudget => AvailableGold.HasValue;

    /// <summary>Live "spent this session / remaining" line, recomputed on every Select/quantity change -
    /// ignores any material-rule cost multiplier (Gromril...), only decided by the caller after the
    /// picker closes, so this is an estimate, not the exact final cost. Sums EquipmentItemRow.TotalCost
    /// (Quantity-aware, and free-dagger-aware), not a flat per-row unit price.</summary>
    public string BudgetDisplay
    {
        get
        {
            var spent = SelectedRows.Sum(r => r.TotalCost) * UnitCount;
            return string.Format(Loc["EquipmentPickerBudgetDisplay"], spent, (AvailableGold ?? 0) - spent);
        }
    }

    public EquipmentItemViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs, IEquipmentPickerNavigationService pickerNavigation,
        IWarbandArchetypePickerService warbandPicker, ISpecialRulePickerService specialRulePicker)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _pickerNavigation = pickerNavigation;
        _warbandPicker = warbandPicker;
        _specialRulePicker = specialRulePicker;
        selectedCategoryLabel = Loc["LibFilterAll"];

        // Voir WarbandArchetypeViewModel - rechargement explicite requis sur changement de langue
        // (onglet TabBar gardé en mémoire par Shell).
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((EquipmentItemViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        _allItems = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        if (LockedCategory is { } locked) SelectedCategory = locked;
        RefreshSelectedCategoryLabel();
        ApplyFilter();
    }

    /// <summary>Recompute le libellé affiché depuis SelectedCategory (le vrai critère) - à refaire à
    /// chaque LoadData puisque le texte dépend de la langue courante.</summary>
    private void RefreshSelectedCategoryLabel() =>
        SelectedCategoryLabel = SelectedCategory is { } category ? CategoryLabel(category) : Loc["LibFilterAll"];

    partial void OnSelectedCategoryChanged(EquipmentCategory? value) => OnPropertyChanged(nameof(ShowGroupHeaders));

    private void ApplyFilter()
    {
        IEnumerable<EquipmentItem> filtered = _allItems;
        if (SelectedCategory is { } category)
            filtered = filtered.Where(i => i.Category == category);
        if (AllowedWarbandArchetypeId is { } warbandId)
        {
            bool WarriorOk(EquipmentItem i) => i.RestrictedToWarriorArchetypeIds.Count == 0
                || (AllowedWarriorArchetypeId is { } wa && i.RestrictedToWarriorArchetypeIds.Contains(wa));

            filtered = AllowedEquipmentListItemIds is { } listIds
                // Recruit picker: the assigned list is the sole source of truth for what this warrior
                // can buy - Rare items reachable by this warrior are list members too, just narrowed to
                // specific archetypes via RestrictedToWarriorArchetypeIds where the list is shared.
                ? filtered.Where(i => listIds.Contains(i.Id) && WarriorOk(i))
                // Broad warband-scoped browse (EquipmentList editor's own "add item" picker, or a
                // warrior with no assigned list) - common pool + this band's own items, unchanged from
                // before EquipmentList existed.
                : filtered.Where(i => i.RestrictedToWarbandArchetypeIds.Count == 0 || i.RestrictedToWarbandArchetypeIds.Contains(warbandId));
        }

        var groups = new ObservableCollection<EquipmentItemGroup>();
        foreach (var item in filtered)
        {
            var groupName = CategoryLabel(item.Category);
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group is null)
            {
                group = new EquipmentItemGroup(groupName);
                groups.Add(group);
            }
            var isFreeForThisPurchase = item.IsFreeDagger && AvailableGold.HasValue && !AlreadyHasFreeDagger;
            group.Add(new EquipmentItemRow(item, isFreeForThisPurchase));
        }
        EquipmentItemGroups = groups;

        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
        OnPropertyChanged(nameof(BudgetDisplay));
    }

    private string CategoryLabel(EquipmentCategory category) => Loc[$"EquipmentCategory{category}"];

    partial void OnSelectedRowChanged(EquipmentItemRow? oldValue, EquipmentItemRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    /// <summary>Tap sur la tuile elle-même (pas sur le stepper +/-) : bascule 0/1 exemplaire, comme avant
    /// l'ajout du stepper de quantité - IncrementQuantity/DecrementQuantity gèrent le reste une fois
    /// sélectionnée.</summary>
    [RelayCommand]
    private void Select(EquipmentItemRow row)
    {
        if (!IsSelectorMode)
        {
            SelectedRow = row;
            return;
        }

        if (row.Quantity > 0)
        {
            row.Quantity = 0;
            row.IsSelected = false;
            SelectedRows.Remove(row);
        }
        else
        {
            row.Quantity = 1;
            row.IsSelected = true;
            SelectedRows.Add(row);
        }
        OnPropertyChanged(nameof(HasSelectedRows));
        OnPropertyChanged(nameof(BudgetDisplay));
    }

    /// <summary>Un exemplaire de plus de cette tuile - le stepper reste affiché en permanence (même à
    /// Quantity == 0), donc "+" doit aussi pouvoir sélectionner la tuile en un seul geste (0 -&gt; 1), pas
    /// seulement l'incrémenter une fois déjà sélectionnée via Select. Pas de plafond ni de vérification de
    /// budget ici, même logique "estimation live" que BudgetDisplay ; l'affordabilité réelle est vérifiée
    /// objet par objet au moment de l'achat (WarbandEditDialogViewModel/WarriorEditDialogViewModel.
    /// AddEquipment), qui s'arrête au premier objet trop cher.</summary>
    [RelayCommand]
    private void IncrementQuantity(EquipmentItemRow row)
    {
        row.Quantity++;
        if (row.Quantity == 1)
        {
            row.IsSelected = true;
            SelectedRows.Add(row);
            OnPropertyChanged(nameof(HasSelectedRows));
        }
        OnPropertyChanged(nameof(BudgetDisplay));
    }

    /// <summary>Symétrique d'IncrementQuantity - retomber à 0 désélectionne complètement la tuile (même
    /// état que si on n'avait jamais tapé dessus). Le bouton "-" reste affiché même à Quantity == 0 (voir
    /// EquipmentItemRow.CanDecrement, qui le grise plutôt que le masquer) - no-op si rien à retirer.</summary>
    [RelayCommand]
    private void DecrementQuantity(EquipmentItemRow row)
    {
        if (row.Quantity <= 0) return;

        if (row.Quantity == 1)
        {
            row.Quantity = 0;
            row.IsSelected = false;
            SelectedRows.Remove(row);
            OnPropertyChanged(nameof(HasSelectedRows));
        }
        else
        {
            row.Quantity--;
        }
        OnPropertyChanged(nameof(BudgetDisplay));
    }

    [RelayCommand]
    private async Task SelectCategory()
    {
        var categories = Enum.GetValues<EquipmentCategory>();
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
        var newItem = new EquipmentItem();
        var dialogViewModel = new EquipmentItemEditDialogViewModel(newItem, Loc["EquipmentItemCreateTitle"], _warbandPicker, _warbandArchetypes, _specialRulePicker);
        if (await ShowDialogAsync(new EquipmentItemEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentItemAsync(newItem, LocalizationService.Instance.Language);
        _allItems.Add(newItem);
        ApplyFilter();

        // Sélecteur : le "+" doit se comporter comme si on avait tapé la nouvelle tuile - coché et
        // ajouté à SelectedRows, sans fermer le picker. Ne trouve rien si un filtre de catégorie actif
        // exclut la catégorie du nouvel item - cas limite accepté, pas de changement de filtre forcé.
        if (IsSelectorMode)
        {
            var row = EquipmentItemGroups.SelectMany(g => g).FirstOrDefault(r => r.Item.Id == newItem.Id);
            if (row != null) Select(row);
        }
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new EquipmentItem
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Cost = s.Cost,
            Rarity = s.Rarity,
            CostRandomMax = s.CostRandomMax,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath,
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds),
            RestrictedToWarriorArchetypeIds = new List<int>(s.RestrictedToWarriorArchetypeIds),
            SpecialRules = new List<SpecialRule>(s.SpecialRules),
            IsFreeDagger = s.IsFreeDagger,
            Movement = s.Movement,
            WeaponSkill = s.WeaponSkill,
            BallisticSkill = s.BallisticSkill,
            Strength = s.Strength,
            Toughness = s.Toughness,
            Wounds = s.Wounds,
            Initiative = s.Initiative,
            Attacks = s.Attacks,
            Leadership = s.Leadership
        };

        var dialogViewModel = new EquipmentItemEditDialogViewModel(copy, Loc["EquipmentItemEditTitle"], _warbandPicker, _warbandArchetypes, _specialRulePicker);
        if (await ShowDialogAsync(new EquipmentItemEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveEquipmentItemAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteEquipmentItemAsync(row.Item.Id);
        _allItems.Remove(row.Item);
        ApplyFilter();
    }

    /// <summary>Répète chaque EquipmentItem Quantity fois (ex. 2 épées longues sélectionnées -&gt; l'objet
    /// "Épée longue" apparaît 2 fois dans la liste retournée) - les deux appelants (WarbandEditDialogViewModel/
    /// WarriorEditDialogViewModel.AddEquipment) traitent déjà chaque entrée de la liste comme un achat
    /// indépendant (matériau propre, coût propre), donc aucun changement requis côté appelant pour que
    /// plusieurs exemplaires du même objet fonctionnent.</summary>
    [RelayCommand]
    private async Task ConfirmSelection()
    {
        var items = SelectedRows.SelectMany(r => Enumerable.Repeat(r.Item, r.Quantity)).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<EquipmentItem>());

    /// <summary>Read-only recap popup (tile info button). AllowConcurrentExecutions : voir
    /// WarbandArchetypeViewModel.ShowDetails - une seule commande partagée par toutes les tuiles, sinon
    /// elles se désactivent toutes ensemble tant qu'un dialog est ouvert.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(EquipmentItemRow row) => _detailDialogs.ShowEquipmentDetailDialogAsync(row.Item);
}
