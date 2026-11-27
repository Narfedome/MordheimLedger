using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

public partial class EquipmentItemEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, EquipmentCategory> _categoryByLabel = new();
    private readonly ISpecialRulePickerService _specialRulePicker;

    protected override bool CancelResult => false;

    protected override object? EditableState => new object?[] { Item, WarbandRestriction.SelectedIds, SpecialRules.Select(r => r.Id), NoEffectOnRaces.Select(r => r.Id),
        SingleUseJson.Serialize(BuildProfile()), HasLoss, LossUpTo, LossLeaderOnly };

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
    [NotifyPropertyChangedFor(nameof(IsSingleUse))]
    private string selectedSingleUseLabel = string.Empty;

    public bool IsSingleUse => _timingByLabel.ContainsKey(SelectedSingleUseLabel);

    // --- Profil d'usage unique (maquette « Codex : effets modifiables ») ---

    public List<string> TargetOptions { get; } = Enum.GetValues<SingleUseTarget>().Select(SingleUseText.Target).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetHint))]
    private string selectedTargetLabel = string.Empty;

    public string TargetHint => Loc[$"SingleUseTargetHint{TargetOf(SelectedTargetLabel)}"];

    private static SingleUseTarget TargetOf(string label) =>
        Enum.GetValues<SingleUseTarget>().FirstOrDefault(t => SingleUseText.Target(t) == label);

    public ObservableCollection<StatModifierEditRow> Bonuses { get; } = new();

    [ObservableProperty]
    private string bonusPreview = string.Empty;

    [ObservableProperty]
    private string afterBattleDiceCount = string.Empty;

    public List<string> AfterBattleDiceOptions { get; } = SingleUseChoices.Dice.Select(d => $"D{d}").ToList();

    [ObservableProperty]
    private string selectedAfterBattleDie = "D6";

    public ObservableCollection<AfterBattleEditRow> AfterBattleRows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTableWarning))]
    private string? tableWarning;

    public bool HasTableWarning => TableWarning is not null;

    [ObservableProperty]
    private bool hasLoss;

    [ObservableProperty]
    private string lossUpTo = "3";

    [ObservableProperty]
    private bool lossLeaderOnly;

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
        selectedTargetLabel = SingleUseText.Target(item.SingleUse?.Target ?? SingleUseTarget.Warrior);
        foreach (var modifier in item.SingleUse?.StatModifiers ?? new List<GameStatModifier>())
            Bonuses.Add(new StatModifierEditRow(modifier, RefreshSingleUse));
        afterBattleDiceCount = item.SingleUse?.AfterBattleDiceCount?.ToString() ?? "2";
        selectedAfterBattleDie = $"D{item.SingleUse?.AfterBattleDiceSides ?? 6}";
        foreach (var outcome in item.SingleUse?.AfterBattle ?? new List<AfterBattleOutcome>())
            AfterBattleRows.Add(new AfterBattleEditRow(outcome, null, RefreshSingleUse));
        hasLoss = item.LostAfterOutOfActionOn is > 0;
        lossUpTo = (item.LostAfterOutOfActionOn ?? 3).ToString();
        lossLeaderOnly = item.LostAfterOutOfActionLeaderOnly;
        _libraryService = libraryService;
        RefreshSingleUse();
        _ = LoadRacesAsync(libraryService);
        _ = ResolveRulesAsync(libraryService);
    }

    private readonly ILibraryService _libraryService;

    /// <summary>Règles gagnées de la table : retrouvées dans le catalogue pour afficher leur nom.</summary>
    private async Task ResolveRulesAsync(ILibraryService libraryService)
    {
        var outcomes = Item.SingleUse?.AfterBattle ?? new List<AfterBattleOutcome>();
        if (outcomes.All(o => o.Kind != AfterBattleOutcomeKind.GainSpecialRule)) return;
        var rules = await libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language);
        for (var i = 0; i < outcomes.Count && i < AfterBattleRows.Count; i++)
        {
            var outcome = outcomes[i];
            if (outcome.Kind != AfterBattleOutcomeKind.GainSpecialRule) continue;
            AfterBattleRows[i].Rule = rules.FirstOrDefault(r => outcome.SpecialRuleId is { } official ? r.OfficialId == official : r.Id == outcome.SpecialRuleLocalId);
        }
    }

    partial void OnAfterBattleDiceCountChanged(string value) => RefreshSingleUse();
    partial void OnSelectedAfterBattleDieChanged(string value) => RefreshSingleUse();

    private int DiceSides => SingleUseChoices.Int(SelectedAfterBattleDie.TrimStart('D')) ?? 6;

    /// <summary>Aperçu des bonus et message ambre de la table (tranches qui se chevauchent ou hors des dés) - jamais bloquant.</summary>
    private void RefreshSingleUse()
    {
        var mods = Bonuses.Select(b => b.ToModel()).ToList();
        BonusPreview = mods.Count == 0 ? Loc["SingleUseNoBonus"]
            : string.Format(Loc["SingleUseBonusPreviewFormat"], string.Join(", ", mods.Select(SingleUseText.Modifier)));

        var table = AfterBattleRows.Select(r => r.ToModel()).ToList();
        var count = SingleUseChoices.Int(AfterBattleDiceCount) ?? 0;
        var outside = table.FirstOrDefault(o => SingleUseRules.IsOutsideDice(o, count, DiceSides));
        TableWarning = SingleUseRules.FirstOverlap(table) is { } overlap
            ? string.Format(Loc["SingleUseOverlapWarning"], SingleUseText.Range(overlap.First), SingleUseText.Range(overlap.Second))
            : outside is not null
                ? string.Format(Loc["SingleUseOutsideDiceWarning"], SingleUseText.Range(outside), count, DiceSides, count, count * DiceSides)
                : null;
    }

    [RelayCommand]
    private void AddBonus()
    {
        Bonuses.Add(new StatModifierEditRow(new GameStatModifier { Field = CharacteristicField.Initiative, Value = 1 }, RefreshSingleUse));
        RefreshSingleUse();
    }

    [RelayCommand]
    private void RemoveBonus(StatModifierEditRow row)
    {
        Bonuses.Remove(row);
        RefreshSingleUse();
    }

    [RelayCommand]
    private void AddAfterBattleRow()
    {
        var count = SingleUseChoices.Int(AfterBattleDiceCount) ?? 2;
        var top = count * DiceSides;
        AfterBattleRows.Add(new AfterBattleEditRow(new AfterBattleOutcome { Min = top, Max = top, Kind = AfterBattleOutcomeKind.CharacteristicChange,
            Field = CharacteristicField.Initiative, Delta = 1 }, null, RefreshSingleUse));
        RefreshSingleUse();
    }

    [RelayCommand]
    private void RemoveAfterBattleRow(AfterBattleEditRow row)
    {
        AfterBattleRows.Remove(row);
        RefreshSingleUse();
    }

    /// <summary>Même sélecteur de règles spéciales que le reste du Codex ; une ligne garde la première règle cochée.</summary>
    [RelayCommand]
    private async Task ChangeAfterBattleRule(AfterBattleEditRow row)
    {
        var picked = await _specialRulePicker.PickSpecialRulesAsync();
        if (picked.FirstOrDefault() is { } rule) row.Rule = rule;
    }

    /// <summary>Profil tel qu'à l'écran, null si l'objet n'est pas à usage unique.</summary>
    private SingleUseProfile? BuildProfile()
    {
        if (!_timingByLabel.TryGetValue(SelectedSingleUseLabel, out var timing)) return null;
        var table = AfterBattleRows.Select(r => r.ToModel()).ToList();
        return new SingleUseProfile
        {
            Timing = timing,
            Target = TargetOf(SelectedTargetLabel),
            StatModifiers = Bonuses.Select(b => b.ToModel()).ToList(),
            AfterBattleDiceCount = table.Count > 0 ? SingleUseChoices.Int(AfterBattleDiceCount) : null,
            AfterBattleDiceSides = table.Count > 0 ? DiceSides : null,
            AfterBattle = table,
        };
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
        Item.SingleUse = BuildProfile();
        Item.LostAfterOutOfActionOn = HasLoss && SingleUseChoices.Int(LossUpTo) is > 0 and var upTo ? upTo : null;
        Item.LostAfterOutOfActionLeaderOnly = HasLoss && LossLeaderOnly;
        Close(true);
    }
}
