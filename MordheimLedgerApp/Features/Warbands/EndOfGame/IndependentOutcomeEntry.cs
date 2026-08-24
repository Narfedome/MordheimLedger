using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One row of a "roll for every item on the list separately" result (Trésor Caché/Bande
/// Massacrée - see Core.Rules.ExplorationOutcomeResolver.IsIndependentThresholdResult) - wraps one
/// ExplorationOutcome with its own independent D6 check against its own SubRollMin threshold, plus
/// whatever second roll its Kind needs once that check passes (an amount for Gold/Wyrdstone, nothing
/// for a fixed-quantity Item, a second D6 on the Magical Artefacts table for a TriggersArtefactRoll
/// branch - same ArtefactRoll/MagicalArtefactTable mechanism already built for Villa d'un Noble). The
/// Auto branch (SubRollMin null, e.g. Hidden Treasure's flat 5D6x5 gold) skips the check entirely -
/// always "passed", no CheckRoll UI shown for it (see EndOfGameDialog.xaml).</summary>
public partial class IndependentOutcomeEntry : ObservableObject
{
    private readonly IReadOnlyDictionary<string, EquipmentItem> _equipmentItemsByEnglishName;
    private readonly IReadOnlyDictionary<string, SpecialRule> _specialRulesByEnglishName;
    private readonly IDetailDialogService _detailDialogs;
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public ExplorationOutcome Outcome { get; }

    /// <summary>Nom de l'objet/or/pierre magique affiché en en-tête de ligne - résolu une fois à la
    /// construction (voir EndOfGameDialogViewModel.ResolveIndependentOutcomeLabel), pas recalculé ici.</summary>
    public string Label { get; }

    public bool IsAuto => Outcome.SubRollMin is null;
    public bool IsGold => Outcome.Kind == ExplorationOutcomeKind.Gold;
    public bool IsWyrdstone => Outcome.Kind == ExplorationOutcomeKind.Wyrdstone;
    public bool IsArtefact => Outcome.TriggersArtefactRoll;
    public bool IsItem => Outcome.Kind == ExplorationOutcomeKind.Item && !IsArtefact;

    public string ThresholdLabel => Outcome.SubRollMin is { } threshold
        ? string.Format(_loc["EndOfGameIndependentThresholdLabel"], threshold) : string.Empty;

    /// <summary>Le jet D6 comparé au seuil (Outcome.SubRollMin) - vide/sans objet pour la branche Auto,
    /// voir IsAuto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Passed))]
    [NotifyPropertyChangedFor(nameof(Missed))]
    [NotifyPropertyChangedFor(nameof(ShowResult))]
    private string checkRoll = string.Empty;

    [ObservableProperty]
    private string? checkRollError;

    partial void OnCheckRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) CheckRollError = null; }

    public bool Passed => IsAuto || (int.TryParse(CheckRoll, out var roll)
        && ExplorationOutcomeResolver.MeetsIndependentThreshold(roll, Outcome.SubRollMin!.Value));

    /// <summary>Jet fait mais seuil raté - distinct de "pas encore joué" (CheckRoll vide), même idiome
    /// que StatTestRoll/BonusStatTestRoll ailleurs dans ce wizard : une branche Auto n'est jamais
    /// "ratée".</summary>
    public bool Missed => !IsAuto && !string.IsNullOrWhiteSpace(CheckRoll) && !Passed;

    /// <summary>Affiche le bloc Or/Objet/Pierre magique/Artefact de cette ligne - toujours pour la
    /// branche Auto, seulement une fois le seuil passé pour les autres.</summary>
    public bool ShowResult => IsAuto || Passed;

    // --- Montant Or/Pierre(s) magique(s) --------------------------------------------------------

    [ObservableProperty]
    private string amountRoll = string.Empty;

    [ObservableProperty]
    private string? amountRollError;

    partial void OnAmountRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) AmountRollError = null; }

    public bool ShowAmountRoll => (IsGold || IsWyrdstone) && Outcome.GoldFormula is not null;

    /// <summary>"Or supplémentaire gagné"/"Pierre(s) magique(s) gagnée(s)" au-dessus du jet de montant -
    /// confirme ce qui vient d'être gagné une fois le seuil passé, avant de jeter combien (retour
    /// utilisateur 2026-08-24). Vide pour la branche Auto : rien à "gagner", elle est acquise d'office,
    /// pas de confirmation nécessaire.</summary>
    public string AmountEarnedLabel => this switch
    {
        { IsAuto: true } => string.Empty,
        { IsGold: true } => _loc["EndOfGameThresholdGoldEarnedLabel"],
        { IsWyrdstone: true } => _loc["EndOfGameWyrdstoneEarnedLabel"],
        _ => string.Empty
    };

    [RelayCommand]
    private void AutoRollAmount()
    {
        if (Outcome.GoldFormula is { } formula) AmountRoll = DiceFormula.Roll(formula).ToString();
    }

    // --- Objet (Relique Sainte "1" fixe, Armure légère "D3", Dague "D6"...) -----------------------

    /// <summary>Quantité de l'objet - pré-remplie directement à la construction pour une formule FIXE
    /// ("1", ex. Relique Sainte), sinon vide et à jeter par le joueur (ShowItemQuantityRoll) - même
    /// convention que ExplorationItemQuantity dans la forme à branche unique (voir ApplyResolvedOutcome).</summary>
    [ObservableProperty]
    private string itemQuantity = string.Empty;

    [ObservableProperty]
    private string? itemQuantityError;

    partial void OnItemQuantityChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) ItemQuantityError = null; }

    public bool ShowItemQuantityRoll => IsItem && Outcome.ItemQuantityFormula?.Contains('D', StringComparison.OrdinalIgnoreCase) == true;

    [RelayCommand]
    private void AutoRollItemQuantity()
    {
        if (Outcome.ItemQuantityFormula is { } formula) ItemQuantity = DiceFormula.Roll(formula).ToString();
    }

    public WarbandEquipment? ResolvedItem => IsItem ? BuildDisplayItem(Outcome.EquipmentItemName, Outcome.MaterialRuleName) : null;

    [RelayCommand]
    private Task ShowResolvedItemDetail() => ResolvedItem is { } item
        ? _detailDialogs.ShowEquipmentDetailDialogAsync(item.Item, item.MaterialRule)
        : Task.CompletedTask;

    // --- Artefact Magique (Villa d'un Noble, table dédiée réutilisée telle quelle) ---------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedArtefactItemName))]
    [NotifyPropertyChangedFor(nameof(ResolvedArtefactItem))]
    private string artefactRoll = string.Empty;

    [ObservableProperty]
    private string? artefactRollError;

    partial void OnArtefactRollChanged(string value) { if (!string.IsNullOrWhiteSpace(value)) ArtefactRollError = null; }

    public string? ResolvedArtefactItemName => IsArtefact && int.TryParse(ArtefactRoll, out var roll)
        ? MagicalArtefactTable.RollForItemName(roll) : null;

    public WarbandEquipment? ResolvedArtefactItem => BuildDisplayItem(ResolvedArtefactItemName, null);

    [RelayCommand]
    private void AutoRollArtefact() => ArtefactRoll = ExplorationChart.RollDie().ToString();

    [RelayCommand]
    private Task ShowResolvedArtefactDetail() => ResolvedArtefactItem is { } item
        ? _detailDialogs.ShowEquipmentDetailDialogAsync(item.Item, item.MaterialRule)
        : Task.CompletedTask;

    [RelayCommand]
    private void AutoRollCheck()
    {
        if (Outcome.SubRollMin is not null) CheckRoll = ExplorationChart.RollDie().ToString();
    }

    private WarbandEquipment? BuildDisplayItem(string? itemName, string? materialRuleName)
    {
        if (itemName is not { } name) return null;
        var item = _equipmentItemsByEnglishName.GetValueOrDefault(name);
        if (item is null) return null;

        var materialRule = materialRuleName is { } ruleName ? _specialRulesByEnglishName.GetValueOrDefault(ruleName) : null;
        return new WarbandEquipment { Item = item, MaterialRule = materialRule };
    }

    public IndependentOutcomeEntry(ExplorationOutcome outcome, string label, IDetailDialogService detailDialogs,
        IReadOnlyDictionary<string, EquipmentItem> equipmentItemsByEnglishName, IReadOnlyDictionary<string, SpecialRule> specialRulesByEnglishName)
    {
        Outcome = outcome;
        Label = label;
        _detailDialogs = detailDialogs;
        _equipmentItemsByEnglishName = equipmentItemsByEnglishName;
        _specialRulesByEnglishName = specialRulesByEnglishName;

        // Quantité fixe ("1", ex. Relique Sainte) : se renseigne directement, ce n'est pas un jet - même
        // convention que ApplyResolvedOutcome dans la forme à branche unique.
        if (IsItem && outcome.ItemQuantityFormula is { } formula && !formula.Contains('D', StringComparison.OrdinalIgnoreCase))
            itemQuantity = formula;
    }
}
