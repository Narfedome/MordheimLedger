using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.HiredSwords.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.HiredSwords;

/// <summary>Catalog of Hired Sword archetypes (e.g. "Gladiateur"/"Pit Fighter") - CRUD only, no picker
/// mode: unlike Skill/Mutation/EquipmentItem, a Hired Sword is never actually recruited into a Warband
/// by this pass (see Models.Library.HiredSword), so there's no selector/confirm-selection flow to
/// support here. Flat list, no grouping (removed on user request - too few entries so far to warrant
/// it).</summary>
public partial class HiredSwordViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly IEquipmentPickerService _equipmentPicker;
    private List<WarbandArchetype> _warbandArchetypes = new();
    private List<EquipmentItem> _equipmentItems = new();

    [ObservableProperty]
    private ObservableCollection<HiredSwordRow> hiredSwordRows = new();

    [ObservableProperty]
    private HiredSwordRow? selectedRow;

    public HiredSwordViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs,
        IWarbandArchetypePickerService warbandPicker, IEquipmentPickerService equipmentPicker)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _warbandPicker = warbandPicker;
        _equipmentPicker = equipmentPicker;

        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((HiredSwordViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetHiredSwordsAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        _equipmentItems = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);

        HiredSwordRows = new ObservableCollection<HiredSwordRow>(items.Select(i => new HiredSwordRow(i)));
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(HiredSwordRow? oldValue, HiredSwordRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(HiredSwordRow row) => SelectedRow = row;

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new HiredSword();
        var dialogViewModel = new HiredSwordEditDialogViewModel(newItem, Loc["HiredSwordCreateTitle"],
            _warbandPicker, _equipmentPicker, _detailDialogs, _warbandArchetypes, Array.Empty<EquipmentItem>());
        if (await ShowDialogAsync(new HiredSwordEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveHiredSwordAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();
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
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds)
        };

        var initialEquipment = _equipmentItems.Where(e => s.StartingEquipmentIds.Contains(e.Id)).ToList();
        var dialogViewModel = new HiredSwordEditDialogViewModel(copy, Loc["HiredSwordEditTitle"],
            _warbandPicker, _equipmentPicker, _detailDialogs, _warbandArchetypes, initialEquipment);
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
}
