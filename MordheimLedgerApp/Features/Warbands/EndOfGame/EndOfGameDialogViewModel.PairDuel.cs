using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Duel avec le meneur" (StepKind.PairDuel, 2026-09-01, retour utilisateur - "quitte à le
/// mettre dans un step supplémentaire pour éviter une page trop longue") - sa propre étape, séparée de la
/// carte Où est l'Argent qui la déclenche (Prisonniers ou Dramatis Personae, voir IsPairDuelStep dans
/// EndOfGameDialogViewModel.Steps), pour ne pas surcharger cette dernière. Affiche le profil comparatif du
/// meneur de bande et de la paire au complet (Marquand + Ulli, jamais un seul des deux - "Une Poignée
/// d'Or" les corrompt/les garde toujours ensemble) - même principe que la comparaison "Ce guerrier"/"Le
/// Gladiateur" de Vendu aux Fosses (WarriorOutcomeRow.ShowSoldToThePits), adaptée à 3 participants au lieu
/// de 2. Pas de moteur de combat : le joueur résout l'affrontement lui-même à la table, coche simplement
/// victoire/défaite (WonDuel) - déjà vrai avant ce découpage, inchangé.
///
/// WonDuel/PairDuelRoll vivaient dans EndOfGameDialogViewModel.Captives.cs avant ce découpage (même
/// fichier que le reste du sous-système "Où est l'Argent", qui reste lui-même partagé/déclaré là-bas -
/// WantsEquipmentSeizure/WantsDuel (déterminés automatiquement depuis le 2026-09-02, plus de choix manuel
/// du joueur) n'ont pas bougé, seule LA RÉSOLUTION du duel une fois déclenché a sa propre étape
/// désormais).</summary>
public partial class EndOfGameDialogViewModel
{
    /// <summary>Le meneur de CETTE bande (Warrior.IsLeader) - null si mort/retraité/malade cette partie
    /// (absent de WarriorRows), auquel cas la carte reste simplement vide plutôt qu'une erreur bloquante,
    /// même idiome que BonusStatTestLeader (Exploration, Bâtiment Éventré).</summary>
    public WarriorOutcomeRow? DuelLeaderRow => WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);

    /// <summary>Marquand Volker - toujours le représentant "visible" du duo dans _dramatisPersonaCatalog
    /// (IsHiddenFromSearchPicker exclut Ulli du picker de recherche, pas de ce catalogue complet). Résolu
    /// indépendamment de qui déclenche cette étape (bande propriétaire ou non) - le catalogue complet
    /// contient toujours les deux fiches.</summary>
    public DramatisPersona? DuelMarquand => _dramatisPersonaCatalog.FirstOrDefault(p => p.FeeKind == DramatisPersonaHireFeeKind.Pair && !p.IsHiddenFromSearchPicker);

    /// <summary>Ulli Leitpold - l'autre moitié, voir DuelMarquand's own doc.</summary>
    public DramatisPersona? DuelUlli => _dramatisPersonaCatalog.FirstOrDefault(p => p.FeeKind == DramatisPersonaHireFeeKind.Pair && p.IsHiddenFromSearchPicker);

    private bool ValidatePairDuelStep()
    {
        var valid = true;
        if (!WonDuel)
        {
            foreach (var sub in PairDuelRoll)
            {
                valid &= CheckRoll(string.IsNullOrWhiteSpace(sub.InjuryResultText), () => sub.RollError = Loc["EndOfGameRollRequired"]);
                if (sub.ShowDeepWoundSubRoll)
                    valid &= CheckRoll(!sub.HasValidDeepWoundSubRoll, () => sub.DeepWoundRollError = Loc["EndOfGameRollRequired"]);
                if (sub.ShowCapturedChoice && sub.IsRansomed)
                    valid &= CheckRoll(!sub.HasValidRansomAmount, () => sub.CapturedChoiceError = Loc["EndOfGameRollRequired"]);
            }
        }
        return valid;
    }

    /// <summary>Même principe que WonPitFight (Vendu aux Fosses, EndOfGameDialogViewModel.Injury.cs) mais
    /// appliqué au chef de bande de CETTE session plutôt qu'à un guerrier hors de combat - décoché par
    /// défaut (défaite), coché = rien de plus à faire ("Ulli et Marquand" échouent à se venger).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPairDuelRoll))]
    private bool wonDuel;

    partial void OnWonDuelChanged(bool value)
    {
        if (value)
        {
            PairDuelRoll.Clear();
            OnPropertyChanged(nameof(HasPairDuelRoll));
        }
        else if (WantsDuel && PairDuelRoll.Count == 0)
        {
            PopulatePairDuelRoll();
        }
    }

    /// <summary>Défaite du duel : un unique sous-jet D66 sur la table des Blessures Graves Héros (le
    /// meneur est toujours un Héros) - réutilise InjurySubRollEntry tel quel, même patron que
    /// WarriorOutcomeRow.SoldToPitsRerollRoll. Collection à un seul élément pour réutiliser le même
    /// DataTemplate/les mêmes commandes AutoRoll que les autres sous-jets du wizard.</summary>
    public ObservableCollection<InjurySubRollEntry> PairDuelRoll { get; } = new();
    public bool HasPairDuelRoll => PairDuelRoll.Count > 0;

    private void PopulatePairDuelRoll()
    {
        var entry = new InjurySubRollEntry(1, 1, isHero: true, labelKey: "EndOfGamePairDuelRollLabel");
        entry.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasPairDuelRoll));
        PairDuelRoll.Add(entry);
        OnPropertyChanged(nameof(HasPairDuelRoll));
    }
}
