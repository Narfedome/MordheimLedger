using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.HiredSwords;

/// <summary>Catalog of Hired Sword archetypes (e.g. "Gladiateur"/"Pit Fighter") - CRUD AND picker mode
/// (added when Hired Swords became actually recruitable into a Warband - see WarbandEditDialogViewModel/
/// EndOfGameDialogViewModel's HiredSwords steps) - same IsSelectorMode/SelectedRows/ConfirmSelection
/// bascule as MagicSchoolViewModel. Flat list, no grouping (removed on user request - too few entries so
/// far to warrant it).</summary>
public partial class HiredSwordViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IMagicSchoolPickerService _magicSchoolPicker;
    private readonly IHiredSwordPickerNavigationService _pickerNavigation;
    private List<WarbandArchetype> _warbandArchetypes = new();
    private List<EquipmentItem> _equipmentItems = new();

    [ObservableProperty]
    private ObservableCollection<HiredSwordRow> hiredSwordRows = new();

    [ObservableProperty]
    private HiredSwordRow? selectedRow;

    /// <summary>Set by HiredSwordSelectorPage right after construction - même bascule multi-sélection
    /// que MagicSchoolViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.</summary>
    public ObservableCollection<HiredSwordRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0 || SelectedRow != null;

    /// <summary>Single (ex. "Une Faveur Rendue" - un seul Franc-Tireur gratuit à la fois) vs Multiple
    /// (ex. l'étape Mercenaires du wizard de création - engager plusieurs types d'un coup) - même
    /// bascule que WarbandArchetypeViewModel.SelectionMode. Set by HiredSwordSelectorPage.</summary>
    public SelectionMode SelectionMode { get; set; }

    /// <summary>Set by HiredSwordPickerService before pushing the picker - même idiome que
    /// SkillViewModel.AllowedWarbandArchetypeId (narrowe aux Francs-Tireurs éligibles à CETTE bande,
    /// RestrictedToWarbandArchetypeIds vide ou la contenant). Null en usage Codex normal (CRUD), où
    /// tout le catalogue doit rester visible.</summary>
    public int? AllowedWarbandArchetypeId { get; set; }

    /// <summary>Set by HiredSwordPickerService - types déjà activement engagés dans la bande (voir
    /// WarbandEditDialogViewModel.HiredSwordRows/EndOfGameDialogViewModel.HiredSwordUpkeepEntries),
    /// jamais réofferts au picker ("un seul de chaque type", livre des règles). Null en usage Codex.</summary>
    public IReadOnlyList<int>? ExcludedHiredSwordIds { get; set; }

    public HiredSwordViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs,
        IWarbandArchetypePickerService warbandPicker, IEquipmentPickerService equipmentPicker,
        ISpecialRulePickerService specialRulePicker, IMagicSchoolPickerService magicSchoolPicker,
        IHiredSwordPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _warbandPicker = warbandPicker;
        _equipmentPicker = equipmentPicker;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;
        _pickerNavigation = pickerNavigation;

        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((HiredSwordViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetHiredSwordsAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        _equipmentItems = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);

        IEnumerable<HiredSword> filtered = items;
        if (AllowedWarbandArchetypeId is { } warbandId)
            filtered = filtered.Where(h => h.RestrictedToWarbandArchetypeIds.Count == 0 || h.RestrictedToWarbandArchetypeIds.Contains(warbandId));
        if (ExcludedHiredSwordIds is { } excluded)
            filtered = filtered.Where(h => !excluded.Contains(h.Id));

        HiredSwordRows = new ObservableCollection<HiredSwordRow>(filtered.Select(i => new HiredSwordRow(i)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(HiredSwordRow? oldValue, HiredSwordRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(HiredSwordRow row)
    {
        if (!IsSelectorMode || SelectionMode == SelectionMode.Single)
        {
            SelectedRow = row;
            OnPropertyChanged(nameof(HasSelectedRows));
            return;
        }

        row.IsSelected = !row.IsSelected;
        if (row.IsSelected) SelectedRows.Add(row);
        else SelectedRows.Remove(row);
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new HiredSword();
        var dialogViewModel = new HiredSwordEditDialogViewModel(newItem, Loc["HiredSwordCreateTitle"],
            _warbandPicker, _equipmentPicker, _specialRulePicker, _magicSchoolPicker, _detailDialogs, _libraryService, _warbandArchetypes, Array.Empty<EquipmentItem>());
        if (await ShowDialogAsync(new HiredSwordEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveHiredSwordAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();

        // Sélecteur : le "+" doit se comporter comme si on avait tapé la nouvelle tuile - coché et
        // ajouté à SelectedRows, sans fermer le picker.
        if (IsSelectorMode)
        {
            var row = HiredSwordRows.FirstOrDefault(r => r.Item.Id == newItem.Id);
            if (row != null) Select(row);
        }
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new HiredSword
        {
            Id = s.Id,
            Name = s.Name,
            HireCost = s.HireCost,
            Upkeep = s.Upkeep,
            BaseRating = s.BaseRating,
            Description = s.Description,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath,
            Movement = s.Movement,
            WeaponSkill = s.WeaponSkill,
            BallisticSkill = s.BallisticSkill,
            Strength = s.Strength,
            Toughness = s.Toughness,
            Wounds = s.Wounds,
            Initiative = s.Initiative,
            Attacks = s.Attacks,
            Leadership = s.Leadership,
            AllowedSkillCategories = new List<SkillCategory>(s.AllowedSkillCategories),
            StartingEquipmentIds = new List<int>(s.StartingEquipmentIds),
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds),
            SpecialRules = new List<SpecialRule>(s.SpecialRules),
            MagicSchoolId = s.MagicSchoolId,
            MagicSchool = s.MagicSchool
        };

        var initialEquipment = _equipmentItems.Where(e => s.StartingEquipmentIds.Contains(e.Id)).ToList();
        var dialogViewModel = new HiredSwordEditDialogViewModel(copy, Loc["HiredSwordEditTitle"],
            _warbandPicker, _equipmentPicker, _specialRulePicker, _magicSchoolPicker, _detailDialogs, _libraryService, _warbandArchetypes, initialEquipment);
        if (await ShowDialogAsync(new HiredSwordEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveHiredSwordAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteHiredSwordAsync(row.Item.Id);
        await LoadData();
    }

    /// <summary>Read-only recap popup (tile info button). AllowConcurrentExecutions : voir
    /// WarbandArchetypeViewModel.ShowDetails.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(HiredSwordRow row) => _detailDialogs.ShowHiredSwordDetailDialogAsync(row.Item);

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        if (SelectionMode == SelectionMode.Single && SelectedRow != null)
        {
            await _pickerNavigation.ClosePickerAsync(new[] { SelectedRow.Item });
            return;
        }

        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<HiredSword>());
}
