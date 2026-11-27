using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

public partial class EquipmentItemEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, EquipmentCategory> _categoryByLabel = new();
    private readonly ISpecialRulePickerService _specialRulePicker;

    protected override bool CancelResult => false;

    protected override object? EditableState => new object?[] { Item, WarbandRestriction.SelectedIds, SpecialRules.Select(r => r.Id), NoEffectOnRaces.Select(r => r.Id), SelectedSingleUseLabel };

    public ObservableCollection<string> CategoryOptions { get; } = new();

    [ObservableProperty]
    private EquipmentItem item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Shows the StatRowView profile block (Movement/WS/BS/S/T/W/I/A/Ld) only when the selected
    /// category is Animal - those 9 fields are meaningless for any other EquipmentCategory.</summary>
    public bool IsAnimalCategory => Item.Category == EquipmentCategory.Animal;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsRulesTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsRulesTab => SelectedTab == 1;

    /// <summary>Édité en mémoire ici, recopié sur Item.RestrictedToWarbandArchetypeIds à la sauvegarde -
    /// même principe que WarriorArchetypeEditDialogViewModel.SpecialRules. Vide = commun à toutes les
    /// bandes (voir EquipmentItem.RestrictedToWarbandArchetypeIds). Include/Exclude mode toggle + chip
    /// collapse factored into WarbandRestrictionEditor - shared with SkillEditDialogViewModel/
    /// MutationEditDialogViewModel, which duplicate this exact editor.</summary>
    public WarbandRestrictionEditor WarbandRestriction { get; }

    public ObservableCollection<SpecialRule> SpecialRules { get; }

    /// <summary>« Sans effet sur » (EquipmentItem.NoEffectOnRaceIds), recopié à la sauvegarde.</summary>
    public ObservableCollection<Race> NoEffectOnRaces { get; } = new();
    private IReadOnlyList<Race> _allRaces = Array.Empty<Race>();

    /// <summary>« À usage unique » : Non, puis les 3 moments (SingleUseTiming). Changer de moment garde le reste du
    /// profil (bonus, table de conséquences) ; « Non » le retire à la sauvegarde.</summary>
    public ObservableCollection<string> SingleUseOptions { get; } = new();
    private readonly Dictionary<string, SingleUseTiming> _timingByLabel = new();

    [ObservableProperty]
    private string selectedSingleUseLabel = string.Empty;

    public EquipmentItemEditDialogViewModel(EquipmentItem item, string title, IWarbandArchetypePickerService warbandPicker,
        IReadOnlyList<WarbandArchetype> allWarbandArchetypes, ISpecialRulePickerService specialRulePicker, ILibraryService libraryService)
    {
        this.item = item;
        this.title = title;
        _specialRulePicker = specialRulePicker;

        foreach (var category in Enum.GetValues<EquipmentCategory>())
        {
            var label = Loc[$"EquipmentCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"EquipmentCategory{item.Category}"];
        WarbandRestriction = new WarbandRestrictionEditor(item.RestrictedToWarbandArchetypeIds, allWarbandArchetypes, warbandPicker);
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);

        SingleUseOptions.Add(Loc["SingleUseNone"]);
        foreach (var timing in Enum.GetValues<SingleUseTiming>())
        {
            var label = Loc[$"SingleUse{timing}"];
            _timingByLabel[label] = timing;
            SingleUseOptions.Add(label);
        }
        selectedSingleUseLabel = item.SingleUse is { } profile ? Loc[$"SingleUse{profile.Timing}"] : Loc["SingleUseNone"];
        _ = LoadRacesAsync(libraryService);
    }

    private async Task LoadRacesAsync(ILibraryService libraryService)
    {
        _allRaces = await libraryService.GetRacesAsync(LocalizationService.Instance.Language);
        foreach (var race in _allRaces.Where(r => Item.NoEffectOnRaceIds.Contains(r.Id)))
            NoEffectOnRaces.Add(race);
    }

    [RelayCommand]
    private async Task AddNoEffectRace()
    {
        var available = _allRaces.Where(r => NoEffectOnRaces.All(c => c.Id != r.Id)).ToList();
        if (available.Count == 0) return;
        var picked = await ShowActionSheetAsync(Loc["NoEffectOnRacesPh"], available.Select(r => r.Name).ToArray());
        if (available.FirstOrDefault(r => r.Name == picked) is { } race) NoEffectOnRaces.Add(race);
    }

    [RelayCommand]
    private void RemoveNoEffectRace(Race race) => NoEffectOnRaces.Remove(race);

    partial void OnSelectedCategoryLabelChanged(string value)
    {
        if (_categoryByLabel.TryGetValue(value, out var category))
            Item.Category = category;
        OnPropertyChanged(nameof(IsAnimalCategory));
    }

    [RelayCommand]
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowRulesTab() => SelectedTab = 1;

    [RelayCommand]
    private async Task AddSpecialRule()
    {
        var picked = await _specialRulePicker.PickSpecialRulesAsync();
        foreach (var rule in picked)
        {
            if (SpecialRules.Any(r => r.Id == rule.Id)) continue;
            SpecialRules.Add(rule);
        }
    }

    [RelayCommand]
    private void RemoveSpecialRule(SpecialRule rule) => SpecialRules.Remove(rule);

    private static SingleUseProfile WithTiming(SingleUseProfile? existing, SingleUseTiming timing)
    {
        var profile = SingleUseJson.Deserialize(SingleUseJson.Serialize(existing)) ?? new SingleUseProfile();
        profile.Timing = timing;
        return profile;
    }

    private bool ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(Item.Name))
        {
            NameError = Loc["LibFieldRequired"];
            return false;
        }
        NameError = null;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!ValidateRequiredFields())
        {
            SelectedTab = 0;
            return;
        }

        Item.RestrictedToWarbandArchetypeIds = WarbandRestriction.SelectedIds;
        Item.SpecialRules = SpecialRules.ToList();
        Item.NoEffectOnRaceIds = NoEffectOnRaces.Select(r => r.Id).ToList();
        Item.SingleUse = _timingByLabel.TryGetValue(SelectedSingleUseLabel, out var timing)
            ? WithTiming(Item.SingleUse, timing)
            : null;
        Close(true);
    }
}
