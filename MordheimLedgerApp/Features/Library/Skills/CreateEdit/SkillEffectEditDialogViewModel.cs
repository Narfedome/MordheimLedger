using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Library.Skills.Effects;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public enum SkillEffectEditResult { Cancelled, Saved, Deleted }

/// <summary>Popup d'un effet de compétence (section Effets de SkillEditDialog). Édite une copie : l'appelant
/// ne remplace l'effet d'origine que sur Saved. Les champs affichés suivent le type d'effet
/// (Core.Rules.EffectHandling.UsesXxx), et ceux qui ne servent plus sont vidés à la validation
/// (EffectHandling.ClearUnusedFields) - aucune règle n'est décidée ici.</summary>
public partial class SkillEffectEditDialogViewModel : DialogViewModel<SkillEffectEditResult>
{
    private readonly SkillEffectNames _names;

    protected override SkillEffectEditResult CancelResult => SkillEffectEditResult.Cancelled;

    protected override object? EditableState => new object?[]
    {
        SelectedMoment, SelectedKind, SelectedCondition, SelectedFrequency, ValueText, MinimumPriceText, Note,
        Targets.Select(t => t.Id), ConditionTargets.Select(t => t.Id),
    };

    /// <summary>L'effet édité - recopié depuis les champs à la validation seulement.</summary>
    public SkillEffect Effect { get; }

    public string Title { get; }
    public bool CanDelete { get; }

    public IReadOnlyList<string> MomentOptions { get; } = Enum.GetValues<EffectMoment>().Select(m => Services.LocalizationService.Instance[$"EffectMoment{m}"]).ToList();
    public IReadOnlyList<string> KindOptions { get; } = Enum.GetValues<EffectKind>().Select(k => Services.LocalizationService.Instance[$"EffectKind{k}"]).ToList();
    public IReadOnlyList<string> ConditionOptions { get; } = Enum.GetValues<EffectCondition>().Select(c => Services.LocalizationService.Instance[$"EffectCondition{c}"]).ToList();
    public IReadOnlyList<string> FrequencyOptions { get; } = Enum.GetValues<EffectFrequency>().Select(f => Services.LocalizationService.Instance[$"EffectFrequency{f}"]).ToList();

    // Index dans les listes ci-dessus = valeur de l'enum (Enum.GetValues suit l'ordre des valeurs).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPickTargets), nameof(TargetsEmptyHint))]
    private int selectedMoment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowValue), nameof(ShowNumericValue), nameof(ShowTextValue), nameof(ShowMinimumPrice),
        nameof(ShowTargets), nameof(CanPickTargets), nameof(TargetsEmptyHint))]
    private int selectedKind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConditionTargets), nameof(ConditionTargetsHeader))]
    private int selectedCondition;

    [ObservableProperty]
    private int selectedFrequency;

    [ObservableProperty]
    private string? valueText;

    [ObservableProperty]
    private string? minimumPriceText;

    [ObservableProperty]
    private string? note;

    [ObservableProperty]
    private string? valueError;

    public ObservableCollection<EffectTargetChip> Targets { get; }
    public ObservableCollection<EffectTargetChip> ConditionTargets { get; }

    private EffectKind Kind => (EffectKind)SelectedKind;
    private EffectMoment Moment => (EffectMoment)SelectedMoment;
    private EffectCondition Condition => (EffectCondition)SelectedCondition;

    public bool ShowValue => EffectHandling.UsesValue(Kind);
    /// <summary>Compteur −/+ (bonus, nombre de dés) ou saisie libre (réduction « 2D6 ») - voir
    /// EffectHandling.UsesNumericValue.</summary>
    public bool ShowNumericValue => EffectHandling.UsesNumericValue(Kind);
    public bool ShowTextValue => ShowValue && !ShowNumericValue;
    public bool ShowMinimumPrice => EffectHandling.UsesMinimumPrice(Kind);
    public bool ShowTargets => EffectHandling.UsesTargets(Kind);
    public bool ShowConditionTargets => EffectHandling.UsesConditionTargets(Condition);

    /// <summary>Le « + » des cibles n'existe que pour les moments où l'app sait proposer une liste.</summary>
    public bool CanPickTargets => ShowTargets && TargetChoices().Count > 0;

    public string TargetsEmptyHint => CanPickTargets ? Loc["EffectTargetsEmpty"] : Loc["EffectTargetsNoChoice"];

    public string ConditionTargetsHeader => Condition == EffectCondition.CarriesItem
        ? Loc["EffectConditionTargetsCarriesPh"]
        : Loc["EffectConditionTargetsExceptPh"];

    public SkillEffectEditDialogViewModel(SkillEffect effect, string skillName, bool isNew, SkillEffectNames names)
    {
        Effect = effect;
        _names = names;
        Title = string.Format(Loc["EffectDialogTitle"], string.IsNullOrWhiteSpace(skillName) ? "…" : skillName.Trim());
        CanDelete = !isNew;

        selectedMoment = (int)effect.Moment;
        selectedKind = (int)effect.Kind;
        selectedCondition = (int)effect.Condition;
        selectedFrequency = (int)effect.Frequency;
        valueText = EffectHandling.UsesNumericValue(effect.Kind)
            ? EffectHandling.StepNumericValue(effect.Kind, effect.Value, 0)
            : effect.Value;
        minimumPriceText = effect.MinimumPrice?.ToString();
        note = effect.Note;
        Targets = new ObservableCollection<EffectTargetChip>(effect.Targets.Select(t => new EffectTargetChip(t, names.NameOf(t, effect.Moment))));
        ConditionTargets = new ObservableCollection<EffectTargetChip>(effect.ConditionTargets.Select(t => new EffectTargetChip(t, names.NameOf(t))));
    }

    /// <summary>Ce que le « + » des cibles propose, selon le moment : catégories d'équipement ou de
    /// compétence, écoles de magie. Les autres moments gardent leurs cibles existantes sans en proposer.</summary>
    private IReadOnlyList<EffectTargetChip> TargetChoices() => Moment switch
    {
        EffectMoment.EquipmentChoice => Enum.GetValues<EquipmentCategory>()
            .Select(c => new EffectTargetChip(c.ToString(), _names.NameOf(c.ToString()))).ToList(),
        EffectMoment.SkillChoice => Enum.GetValues<SkillCategory>()
            .Select(c => new EffectTargetChip(c.ToString(), _names.NameOf(c.ToString()))).ToList(),
        EffectMoment.SpellChoice => _names.MagicSchools.Select(s => new EffectTargetChip(s.OfficialId, s.Name)).ToList(),
        EffectMoment.CharacteristicIncrease => Enum.GetValues<CharacteristicField>()
            .Select(f => new EffectTargetChip(f.ToString(), _names.NameOf(f.ToString(), EffectMoment.CharacteristicIncrease))).ToList(),
        _ => [],
    };

    private IReadOnlyList<EffectTargetChip> ConditionTargetChoices() => Condition == EffectCondition.CarriesItem
        ? _names.CarriableItems.Select(e => new EffectTargetChip(e.OfficialId, e.Name)).ToList()
        : _names.WarbandArchetypes.Select(w => new EffectTargetChip(w.OfficialId, w.Name)).ToList();

    /// <summary>Passage à un type à compteur : la valeur restée de l'ancien type (« 2D6 », vide) est remise
    /// dans les bornes du compteur.</summary>
    partial void OnSelectedKindChanged(int value)
    {
        if (EffectHandling.UsesNumericValue((EffectKind)value))
            ValueText = EffectHandling.StepNumericValue((EffectKind)value, ValueText, 0);
    }

    [RelayCommand]
    private void IncrementValue() => ValueText = EffectHandling.StepNumericValue(Kind, ValueText, 1);

    [RelayCommand]
    private void DecrementValue() => ValueText = EffectHandling.StepNumericValue(Kind, ValueText, -1);

    [RelayCommand]
    private Task AddTarget() => AddFromAsync(TargetChoices(), Targets);

    [RelayCommand]
    private void RemoveTarget(EffectTargetChip chip) => Targets.Remove(chip);

    [RelayCommand]
    private Task AddConditionTarget() => AddFromAsync(ConditionTargetChoices(), ConditionTargets);

    [RelayCommand]
    private void RemoveConditionTarget(EffectTargetChip chip) => ConditionTargets.Remove(chip);

    private async Task AddFromAsync(IReadOnlyList<EffectTargetChip> choices, ObservableCollection<EffectTargetChip> into)
    {
        var available = choices.Where(c => into.All(t => t.Id != c.Id)).ToList();
        if (available.Count == 0) return;
        var index = await ShowActionSheetIndexAsync(Loc["EffectAddTargetTitle"], available.Select(c => c.Name).ToArray());
        if (index >= 0 && index < available.Count) into.Add(available[index]);
    }

    [RelayCommand]
    private void Save()
    {
        // Une réduction ou un bonus sans valeur ne voudrait rien dire ; les autres types ont « 1 » par défaut.
        if (Kind is EffectKind.Modifier or EffectKind.Discount && string.IsNullOrWhiteSpace(ValueText))
        {
            ValueError = Loc["LibFieldRequired"];
            return;
        }
        ValueError = null;

        Effect.Moment = Moment;
        Effect.Kind = Kind;
        Effect.Condition = Condition;
        Effect.Frequency = (EffectFrequency)SelectedFrequency;
        Effect.Value = ValueText?.Trim();
        Effect.MinimumPrice = int.TryParse(MinimumPriceText?.Trim(), out var floor) ? floor : null;
        Effect.Note = Note?.Trim();
        Effect.Targets = Targets.Select(t => t.Id).ToList();
        Effect.ConditionTargets = ConditionTargets.Select(t => t.Id).ToList();
        EffectHandling.ClearUnusedFields(Effect);
        Close(SkillEffectEditResult.Saved);
    }

    [RelayCommand]
    private void Delete() => Close(SkillEffectEditResult.Deleted);
}
