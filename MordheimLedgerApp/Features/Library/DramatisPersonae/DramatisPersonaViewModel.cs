using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae;

/// <summary>Catalog of Dramatis Personae/special characters (e.g. "Aenur, the Sword of Twilight") -
/// CRUD only, no picker mode (unlike HiredSwordViewModel): catalog-only pass, not yet a recruitment
/// source - see Models.Library.DramatisPersona's own doc. Flat list, no grouping, same "too few entries
/// to warrant it" call as HiredSwordView.</summary>
public partial class DramatisPersonaViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IMagicSchoolPickerService _magicSchoolPicker;
    private List<WarbandArchetype> _warbandArchetypes = new();
    private List<EquipmentItem> _equipmentItems = new();

    [ObservableProperty]
    private ObservableCollection<DramatisPersonaRow> dramatisPersonaRows = new();

    [ObservableProperty]
    private DramatisPersonaRow? selectedRow;

    public DramatisPersonaViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs,
        IWarbandArchetypePickerService warbandPicker, IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker,
        ISpecialRulePickerService specialRulePicker, IMagicSchoolPickerService magicSchoolPicker)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _warbandPicker = warbandPicker;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;

        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((DramatisPersonaViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetDramatisPersonaeAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        _equipmentItems = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);

        DramatisPersonaRows = new ObservableCollection<DramatisPersonaRow>(items.Select(i => new DramatisPersonaRow(i)));
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(DramatisPersonaRow? oldValue, DramatisPersonaRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(DramatisPersonaRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new DramatisPersona();
        var dialogViewModel = new DramatisPersonaEditDialogViewModel(newItem, Loc["DramatisPersonaCreateTitle"],
            _warbandPicker, _equipmentPicker, _skillPicker, _specialRulePicker, _magicSchoolPicker, _detailDialogs,
            _libraryService, _warbandArchetypes, Array.Empty<EquipmentItem>());
        if (await ShowDialogAsync(new DramatisPersonaEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveDramatisPersonaAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new DramatisPersona
        {
            Id = s.Id,
            Name = s.Name,
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
            FeeKind = s.FeeKind,
            HireCost = s.HireCost,
            Upkeep = s.Upkeep,
            RatingBonus = s.RatingBonus,
            IsWanderer = s.IsWanderer,
            RequiresRatingDisadvantage = s.RequiresRatingDisadvantage,
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds),
            SpecialRules = new List<SpecialRule>(s.SpecialRules),
            StartingEquipmentIds = new List<int>(s.StartingEquipmentIds),
            Skills = new List<Skill>(s.Skills),
            MagicSchoolId = s.MagicSchoolId,
            MagicSchool = s.MagicSchool
        };

        var initialEquipment = _equipmentItems.Where(e => s.StartingEquipmentIds.Contains(e.Id)).ToList();
        var dialogViewModel = new DramatisPersonaEditDialogViewModel(copy, Loc["DramatisPersonaEditTitle"],
            _warbandPicker, _equipmentPicker, _skillPicker, _specialRulePicker, _magicSchoolPicker, _detailDialogs,
            _libraryService, _warbandArchetypes, initialEquipment);
        if (await ShowDialogAsync(new DramatisPersonaEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveDramatisPersonaAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteDramatisPersonaAsync(row.Item.Id);
        await LoadData();
    }

    /// <summary>Read-only recap popup (tile info button). AllowConcurrentExecutions : voir
    /// WarbandArchetypeViewModel.ShowDetails.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(DramatisPersonaRow row) => _detailDialogs.ShowDramatisPersonaDetailDialogAsync(row.Item);
}
