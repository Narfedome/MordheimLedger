using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

/// <summary>Listes de choix partagées par les lignes du formulaire (caractéristiques, dés, effets).</summary>
internal static class SingleUseChoices
{
    private static LocalizationService Loc => LocalizationService.Instance;

    public static List<string> StatLabels => Enum.GetValues<CharacteristicField>().Select(StatLabel).ToList();
    public static string StatLabel(CharacteristicField field) => $"{Loc[$"Stat{field}Name"]} ({Loc[$"Stat{field}Abbr"]})";
    public static CharacteristicField StatOf(string label) =>
        Enum.GetValues<CharacteristicField>().FirstOrDefault(f => StatLabel(f) == label);

    public static readonly int[] Dice = [3, 6];
    public static string DieLabel(int? sides) => sides is > 0 ? $"1D{sides}" : Loc["SingleUseNoDie"];
    public static List<string> DieLabels => new[] { DieLabel(null) }.Concat(Dice.Select(d => DieLabel(d))).ToList();
    public static int? DieOf(string label) => Dice.Cast<int?>().FirstOrDefault(d => DieLabel(d) == label);

    public static string KindLabel(AfterBattleOutcomeKind kind) => Loc[$"SingleUseKind{kind}"];
    public static List<string> KindLabels => Enum.GetValues<AfterBattleOutcomeKind>().Select(KindLabel).ToList();
    public static AfterBattleOutcomeKind KindOf(string label) =>
        Enum.GetValues<AfterBattleOutcomeKind>().FirstOrDefault(k => KindLabel(k) == label);

    public static int? Int(string? text) => int.TryParse(text?.Trim().Replace("+", ""), out var value) ? value : null;
}

/// <summary>Une ligne « Bonus pour la partie » : caractéristique, valeur fixe, dé lancé au début de partie.</summary>
public partial class StatModifierEditRow : ObservableObject
{
    private readonly Action _changed;

    public List<string> StatOptions { get; } = SingleUseChoices.StatLabels;
    public List<string> DieOptions { get; } = SingleUseChoices.DieLabels;

    [ObservableProperty] private string selectedStat;
    [ObservableProperty] private string value;
    [ObservableProperty] private string selectedDie;

    public StatModifierEditRow(GameStatModifier modifier, Action changed)
    {
        _changed = changed;
        selectedStat = SingleUseChoices.StatLabel(modifier.Field);
        value = modifier.Value == 0 ? "0" : $"{modifier.Value:+0;-0}";
        selectedDie = SingleUseChoices.DieLabel(modifier.DiceSides);
    }

    public string RemoveLabel => string.Format(LocalizationService.Instance["SingleUseRemoveBonusFormat"], SelectedStat);

    partial void OnSelectedStatChanged(string value) => _changed();
    partial void OnValueChanged(string value) => _changed();
    partial void OnSelectedDieChanged(string value) => _changed();

    public GameStatModifier ToModel() => new()
    {
        Field = SingleUseChoices.StatOf(SelectedStat),
        Value = SingleUseChoices.Int(Value) ?? 0,
        DiceSides = SingleUseChoices.DieOf(SelectedDie),
    };
}

/// <summary>Une ligne de la table d'après-bataille : tranche « De – À », effet, puis selon l'effet la caractéristique et sa
/// valeur, ou la règle gagnée (sélecteur de règles du Codex).</summary>
public partial class AfterBattleEditRow : ObservableObject
{
    private readonly Action _changed;

    public List<string> KindOptions { get; } = SingleUseChoices.KindLabels;
    public List<string> StatOptions { get; } = SingleUseChoices.StatLabels;

    [ObservableProperty] private string min;
    [ObservableProperty] private string max;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCharacteristic), nameof(IsRule))]
    private string selectedKind;

    [ObservableProperty] private string selectedStat;
    [ObservableProperty] private string delta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuleName), nameof(HasRule))]
    private SpecialRule? rule;

    /// <summary>Règle d'origine, gardée telle quelle si elle n'a pas pu être retrouvée dans le catalogue (Rule null).</summary>
    private readonly string? _officialRuleId;
    private readonly int? _localRuleId;

    public AfterBattleEditRow(AfterBattleOutcome outcome, SpecialRule? rule, Action changed)
    {
        _changed = changed;
        min = outcome.Min.ToString();
        max = outcome.Max.ToString();
        selectedKind = SingleUseChoices.KindLabel(outcome.Kind);
        selectedStat = SingleUseChoices.StatLabel(outcome.Field ?? CharacteristicField.Initiative);
        delta = outcome.Delta is { } d ? $"{d:+0;-0;0}" : "+1";
        this.rule = rule;
        _officialRuleId = outcome.SpecialRuleId;
        _localRuleId = outcome.SpecialRuleLocalId;
    }

    public bool IsCharacteristic => SingleUseChoices.KindOf(SelectedKind) == AfterBattleOutcomeKind.CharacteristicChange;
    public bool IsRule => SingleUseChoices.KindOf(SelectedKind) == AfterBattleOutcomeKind.GainSpecialRule;
    public string RuleName => Rule?.Name ?? LocalizationService.Instance["SingleUseNoRuleChosen"];
    public bool HasRule => Rule is not null;
    public string RemoveLabel => string.Format(LocalizationService.Instance["SingleUseRemoveRowFormat"], Min, Max);

    partial void OnMinChanged(string value) => _changed();
    partial void OnMaxChanged(string value) => _changed();
    partial void OnSelectedKindChanged(string value) => _changed();
    partial void OnSelectedStatChanged(string value) => _changed();
    partial void OnDeltaChanged(string value) => _changed();
    partial void OnRuleChanged(SpecialRule? value) => _changed();

    public AfterBattleOutcome ToModel()
    {
        var kind = SingleUseChoices.KindOf(SelectedKind);
        var outcome = new AfterBattleOutcome
        {
            Min = SingleUseChoices.Int(Min) ?? 0,
            Max = SingleUseChoices.Int(Max) ?? 0,
            Kind = kind,
        };
        if (kind == AfterBattleOutcomeKind.CharacteristicChange)
        {
            outcome.Field = SingleUseChoices.StatOf(SelectedStat);
            outcome.Delta = SingleUseChoices.Int(Delta) ?? 0;
        }
        else if (kind == AfterBattleOutcomeKind.GainSpecialRule)
        {
            // Règle officielle : par id officiel (la synchro du contenu le comprend) ; règle du joueur : par id local.
            outcome.SpecialRuleId = Rule is { } chosen ? chosen.OfficialId : _officialRuleId;
            outcome.SpecialRuleLocalId = Rule is { } local ? (local.OfficialId is null ? local.Id : null) : _localRuleId;
        }
        return outcome;
    }
}
