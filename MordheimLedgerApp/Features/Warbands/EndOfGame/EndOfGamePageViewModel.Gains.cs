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

    /// <summary>Or rapporté par cette partie : Exploration (y compris chaque ligne d'un Trésor Caché/Bande
    /// Massacrée), Récompenses du scénario, vente de pierres magiques, prisonniers ennemis rançonnés ou
    /// vendus.</summary>
    public int GainedGoldThisGame =>
        (ResolvedExplorationOutcome?.Kind == ExplorationOutcomeKind.Gold && int.TryParse(ExplorationGoldAmount, out var gold) ? gold : 0)
        + IndependentOutcomeGold
        + ScenarioGold
        + WyrdstoneSaleValue
        + CapturedEnemiesGold;

    /// <summary>Or des lignes Trésor Caché/Bande Massacrée passées - mêmes conditions qu'
    /// ApplyExplorationOutcomeAsync. Absent du solde jusqu'au 2026-11-02 (retour utilisateur - la
    /// trésorerie écrite à Terminer ne correspondait pas à celle affichée).</summary>
    private int IndependentOutcomeGold => IndependentOutcomeEntries
        .Where(e => e.ShowResult && e.IsGold)
        .Sum(e => int.TryParse(e.AmountRoll, out var amount) ? amount : 0);

    /// <summary>Pierres magiques des lignes Trésor Caché/Bande Massacrée passées - même principe
    /// qu'IndependentOutcomeGold, rejoint FoundThisGameWyrdstoneShards (vendables dans la même séance).</summary>
    private int IndependentOutcomeWyrdstoneShards => IndependentOutcomeEntries
        .Where(e => e.ShowResult && e.IsWyrdstone)
        .Sum(e => int.TryParse(e.AmountRoll, out var amount) ? amount : 0);

    /// <summary>Prisonniers ennemis rançonnés/vendus aux esclavagistes - mêmes conditions
    /// qu'ApplyCapturedEnemiesAsync.</summary>
    private int CapturedEnemiesGold => !HasCapturedEnemies ? 0 : CapturedEnemies
        .Where(e => e.SelectedFate is CapturedEnemyFate.Ransomed or CapturedEnemyFate.SoldToSlavers)
        .Sum(e => int.TryParse(e.GoldAmount, out var amount) ? amount : 0);

    /// <summary>Trésorerie avant cette Fin de Partie + ses gains + l'or des Blessures (rançons payées,
    /// combats de gladiateur - WarriorOutcomeRow.TreasuryDelta) - base de EndOfGameTreasuryRemaining, qui
    /// retranche ensuite toutes les dépenses décidées dans le wizard. Pur aperçu, rien n'est écrit avant
    /// Terminer.</summary>
    private int TreasuryWithGains => _currentTreasury + GainedGoldThisGame + WarriorRows.Sum(r => r.TreasuryDelta);
}
