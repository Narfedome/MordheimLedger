using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Expérience pré-remplie et nom du scénario (1.6). L'appli propose la part de l'XP qu'elle connaît (survie,
/// chef vainqueur, outsider - Core.Rules.ExperiencePrefill) et dit d'où elle vient dans une ligne d'astuce ; le
/// total reste une saisie libre, les ennemis mis hors de combat et le bonus du scénario sont ajoutés par les joueurs.
/// L'outsider n'est pré-rempli que si les valeurs adverses ont été saisies au début de partie (Warband.OpponentRatings).</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Nom du scénario joué, saisie libre à l'étape Résultat, repris dans la phrase d'Historique.</summary>
    [ObservableProperty]
    private string scenarioName = string.Empty;

    /// <summary>Valeur de la bande avant la partie (même calcul que la fiche de bande), figée au chargement.</summary>
    private int _ratingBeforeGame;

    private IReadOnlyList<int> OpponentRatings => Warband?.OpponentRatings ?? (IReadOnlyList<int>)Array.Empty<int>();

    private int UnderdogBonus => UnderdogExperience.BonusFor(_ratingBeforeGame, OpponentRatings.ToList());

    /// <summary>Ligne sous l'intro de l'étape Expérience : outsider calculé, ou rappel qu'il n'a pas pu l'être.</summary>
    public string ExperienceUnderdogHint
    {
        get
        {
            if (OpponentRatings.Count == 0) return Loc["EndOfGameExperienceNoOpponentRating"];
            return UnderdogExperience.GapFor(_ratingBeforeGame, OpponentRatings.ToList()) is { } gap && UnderdogBonus > 0
                ? string.Format(Loc["EndOfGameExperienceUnderdogFormat"], gap, UnderdogBonus)
                : Loc["EndOfGameExperienceNotUnderdog"];
        }
    }

    private void CaptureRatingBeforeGame() => _ratingBeforeGame = WarbandRatingRules.Total(_allActiveWarriorRows.Select(r => r.Warrior));

    /// <summary>Pose le pré-remplissage de chaque guerrier de l'étape Expérience. Appelé à chaque arrivée sur l'étape :
    /// un champ que le joueur a modifié n'est jamais écrasé (voir WarriorOutcomeRow.AppliedExperiencePrefillText).</summary>
    private void ApplyExperiencePrefill()
    {
        var underdog = UnderdogBonus;
        foreach (var row in WarriorRows.Where(r => r.ShowsInExperienceStep))
        {
            var prefill = ExperiencePrefill.For(row.GainsExperience, survived: !row.IsDeadFromInjuries,
                wasLeaderDuringBattle: row.Warrior.IsLeader, victory: WonLastGame, underdogBonus: underdog);

            var parts = new List<string>();
            if (prefill.Survival > 0) parts.Add(string.Format(Loc["EndOfGameXpPartSurvival"], prefill.Survival));
            if (prefill.WinningLeader > 0) parts.Add(string.Format(Loc["EndOfGameXpPartWinningLeader"], prefill.WinningLeader));
            if (prefill.Underdog > 0) parts.Add(string.Format(Loc["EndOfGameXpPartUnderdog"], prefill.Underdog));
            row.ExperiencePrefillTip = parts.Count == 0 ? string.Empty
                : string.Format(Loc["EndOfGameXpPrefillTipFormat"], string.Join(", ", parts));

            var text = prefill.Total > 0 ? prefill.Total.ToString() : string.Empty;
            if (ExperiencePrefill.IsUntouched(row.ExperienceGainedText, row.AppliedExperiencePrefillText))
            {
                row.ExperienceGainedText = text;
                row.AppliedExperiencePrefillText = text;
            }
        }
        OnPropertyChanged(nameof(ExperienceUnderdogHint));
    }

    /// <summary>Phrase de résultat de l'Historique, avec le scénario s'il a été saisi.</summary>
    private string ResultHistorySentence() => string.IsNullOrWhiteSpace(ScenarioName)
        ? string.Format(Loc["HistoryResultSentence"], SelectedResult)
        : string.Format(Loc["HistoryResultWithScenarioSentence"], SelectedResult, ScenarioName.Trim());
}
