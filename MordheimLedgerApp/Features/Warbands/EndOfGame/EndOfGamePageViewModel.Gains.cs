using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Point central de ce que CETTE partie rapporte à la bande, toutes étapes confondues (retour
/// utilisateur 2026-09-25 - "on mutualise et on centralise nos mécaniques d'objet, d'XP et d'or") :
/// chaque nouvelle source de gain se branche ICI une seule fois plutôt que chez chaque lecteur.
///
/// - **Objets** : ReserveGains - Exploration, Récompenses du scénario, objets rares achetés, chacun avec
///   son origine. Versés dans la réserve par BuildReserve (EndOfGamePageViewModel.Reserve.cs), la seule
///   source de vérité de la réserve, créés en base une seule fois à Terminer (ApplyReserveAsync).
/// - **Or** : GainedGoldThisGame, base du solde unique du wizard (TreasuryWithGains ->
///   EndOfGameTreasuryRemaining).
/// - **Pierres magiques** : FoundThisGameWyrdstoneShards (EndOfGamePageViewModel.PostBattle.cs), déjà
///   central - Exploration + scénario.
/// - **XP** : une seule saisie par guerrier, WarriorOutcomeRow.ExperienceGained (étape Expérience -
///   survie, victoire, scénario...), à part l'XP propre à l'Exploration et aux blessures qui a ses règles.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Objets gagnés pendant cette partie, dans l'ordre des étapes, avec leur origine.</summary>
    private IEnumerable<(ReserveInflow Inflow, ReserveLineOrigin Origin)> ReserveGains()
    {
        foreach (var found in PendingExplorationItems())
            yield return (found, ReserveLineOrigin.Exploration);

        foreach (var pick in ScenarioRewardItems)
            yield return (new ReserveInflow(pick.Item, pick.MaterialRule, 1), ReserveLineOrigin.Scenario);

        // Retour utilisateur 2026-09-25 - "les objets rares n'apparaissent pas dans la réserve".
        foreach (var entry in RareItemSearchEntries.Where(e => e.IsPurchased))
            yield return (new ReserveInflow(entry.SelectedItem!, entry.SelectedMaterial, 1), ReserveLineOrigin.RarePurchase);
    }

    /// <summary>Or rapporté par cette partie : Exploration, Récompenses du scénario, vente de pierres
    /// magiques.</summary>
    public int GainedGoldThisGame =>
        (ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold && int.TryParse(ExplorationGoldAmount, out var gold) ? gold : 0)
        + ScenarioGold
        + WyrdstoneSaleValue;

    /// <summary>Trésorerie avant cette Fin de Partie + ses gains - base de EndOfGameTreasuryRemaining, qui
    /// retranche ensuite toutes les dépenses décidées dans le wizard. Pur aperçu, rien n'est écrit avant
    /// Terminer.</summary>
    private int TreasuryWithGains => _currentTreasury + GainedGoldThisGame;
}
