using System.Collections.ObjectModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>"Roll for every item on the list separately" shape (Trésor Caché/Bande Massacrée - see
/// Core.Rules.ExplorationOutcomeResolver.IsIndependentThresholdResult) - populated once per triggered
/// résultat (ResolveExplorationResult, EndOfGameDialogViewModel.Exploration.cs) rather than resolving
/// to a SINGLE ResolvedExplorationOutcome like every other shape in this wizard : plusieurs lignes
/// peuvent franchir leur propre seuil à la fois, donc ça a besoin de sa propre liste plutôt que la
/// plomberie à branche unique (IsExplorationGold/Item/Wyrdstone, ShowExplorationXxxRoll...) utilisée
/// partout ailleurs. Mockup confirmé avec l'utilisateur (2026-08-24) avant implémentation, calqué sur
/// les patterns déjà en place (sous-jet, Objet, Artefact) plutôt qu'un nouveau langage visuel.</summary>
public partial class EndOfGameDialogViewModel
{
    public bool IsIndependentThresholdResult => TriggeredExplorationResult is { } result
        && ExplorationOutcomeResolver.IsIndependentThresholdResult(result);

    public ObservableCollection<IndependentOutcomeEntry> IndependentOutcomeEntries { get; } = new();

    /// <summary>Reconstruit entièrement la liste à chaque nouveau résultat déclenché (voir
    /// ResolveExplorationResult) - jamais appelée seule, toujours après que TriggeredExplorationResult
    /// soit à jour. Sans effet (liste vide) pour tout résultat qui n'est pas cette forme précise.</summary>
    private void SyncIndependentOutcomeEntries()
    {
        IndependentOutcomeEntries.Clear();
        if (!IsIndependentThresholdResult || TriggeredExplorationResult is not { } result) return;

        // Ordre demandé par l'utilisateur (2026-08-24) : Or, Or supplémentaire, Pierre(s) magique(s),
        // Objets, Artefact Magique - plutôt que l'ordre brut du JSON (celui de la table du livre, pas
        // pensé pour une liste à l'écran).
        foreach (var outcome in result.Outcomes.OrderBy(IndependentOutcomeSortOrder))
            IndependentOutcomeEntries.Add(new IndependentOutcomeEntry(outcome, ResolveIndependentOutcomeLabel(outcome),
                _detailDialogs, _equipmentItemsByEnglishName, _specialRulesByEnglishName));
    }

    private static int IndependentOutcomeSortOrder(ExplorationOutcome outcome) => outcome switch
    {
        { TriggersArtefactRoll: true } => 4,
        { Kind: ExplorationOutcomeKind.Item } => 3,
        { Kind: ExplorationOutcomeKind.Wyrdstone } => 2,
        { Kind: ExplorationOutcomeKind.Gold, SubRollMin: null } => 0,
        { Kind: ExplorationOutcomeKind.Gold } => 1,
        _ => 5
    };

    /// <summary>Nom affiché en en-tête de chaque ligne - dérivé du Kind/EquipmentItemName plutôt qu'un
    /// nouveau champ JSON : sans ambiguïté pour Objet (nom du catalogue)/Artefact/Pierre magique, sauf
    /// pour Or où la branche Auto ("Or trouvé") doit se distinguer d'une branche à seuil (ex. Trésor
    /// Caché : "Gemmes" modélisées en Or D3x10 - voir ExplorationResults.json) - un libellé générique
    /// "Or supplémentaire" suffit ici, le paragraphe complet en haut de l'étape donne déjà le vrai nom
    /// ("Gemmes").</summary>
    private string ResolveIndependentOutcomeLabel(ExplorationOutcome outcome) => outcome switch
    {
        { TriggersArtefactRoll: true } => Loc["EndOfGameArtefactRowLabel"],
        { Kind: ExplorationOutcomeKind.Item, EquipmentItemName: { } name } =>
            _equipmentItemsByEnglishName.GetValueOrDefault(name)?.Name ?? name,
        { Kind: ExplorationOutcomeKind.Gold, SubRollMin: null } => Loc["EndOfGameAutoGoldRowLabel"],
        { Kind: ExplorationOutcomeKind.Gold } => Loc["EndOfGameThresholdGoldRowLabel"],
        { Kind: ExplorationOutcomeKind.Wyrdstone } => Loc["EndOfGameWyrdstoneRowLabel"],
        _ => string.Empty
    };

    /// <summary>Bloque tant qu'un jet de seuil visible n'est pas rempli, puis (une fois le seuil passé,
    /// ou pour la branche Auto) tant que le jet de montant/quantité/artefact qui en découle ne l'est
    /// pas non plus - même principe que ValidateExplorationResultStep pour la forme à branche unique.</summary>
    private bool ValidateIndependentOutcomesStep()
    {
        var valid = true;
        foreach (var entry in IndependentOutcomeEntries)
        {
            if (!entry.IsAuto)
                valid &= CheckRoll(string.IsNullOrWhiteSpace(entry.CheckRoll), () => entry.CheckRollError = Loc["EndOfGameRollRequired"]);

            if (!entry.ShowResult) continue;

            if (entry.ShowAmountRoll)
                valid &= CheckRoll(string.IsNullOrWhiteSpace(entry.AmountRoll), () => entry.AmountRollError = Loc["EndOfGameRollRequired"]);
            if (entry.ShowItemQuantityRoll)
                valid &= CheckRoll(string.IsNullOrWhiteSpace(entry.ItemQuantity), () => entry.ItemQuantityError = Loc["EndOfGameRollRequired"]);
            if (entry.IsArtefact)
                valid &= CheckRoll(string.IsNullOrWhiteSpace(entry.ArtefactRoll), () => entry.ArtefactRollError = Loc["EndOfGameRollRequired"]);
        }
        return valid;
    }
}
