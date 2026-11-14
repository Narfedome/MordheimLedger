using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Duel avec le meneur" (StepKind.PairDuel, 2026-09-01, retour utilisateur - "quitte à le
/// mettre dans un step supplémentaire pour éviter une page trop longue") - sa propre étape, séparée de la
/// carte "Une Poignée d'Or" qui la déclenche (voir IsPairDuelStep/EndOfGamePageViewModel.
/// PairEngagement.cs), pour ne pas surcharger cette dernière. Affiche le profil comparatif du meneur de
/// bande et du/des adversaire(s) - même principe que la comparaison "Ce guerrier"/"Le Gladiateur" de
/// Vendu aux Fosses (WarriorOutcomeRow.ShowSoldToThePits), généralisé à N participants (voir
/// DuelOpponents) plutôt que 2 fixes. Pas de moteur de combat : le joueur résout l'affrontement lui-même
/// à la table, coche simplement victoire/défaite (WonDuel).
///
/// **Générique depuis le 2026-09-04 (retour utilisateur - "si le personnage a la règle c'est l'heure de
/// payer, on réutilise la mécanique du combat/vol de réserve") : DuelOpponents affiche 1 ou 2 cartes
/// selon PairedWithDramatisPersona, pas 2 fixes (Marquand + Ulli).** Ulli &amp; Marquand restent le seul
/// cas réel aujourd'hui (2 cartes) ; un futur Dramatis Persona solo portant "A Fistful of Crowns" sans
/// partenaire n'afficherait qu'une seule carte, sans rien à changer dans ce fichier ni le XAML.
///
/// **Position dans le wizard : toujours juste après l'étape "Une Poignée d'Or"** (voir
/// EndOfGamePageViewModel.Steps) - un seul point d'ancrage possible depuis que Corruption et Rétention
/// partagent la même étape (2026-09-04, retour utilisateur), plus besoin de deux positions distinctes
/// comme avant ce regroupement (juste après Prisonniers si Corruption, juste après Dramatis Personae si
/// Rétention - une première version l'insérait même à une position FIXE avant tout ça, juste avant
/// Récapitulatif, trompeuse pour la Corruption qui se déclenchait tôt dans le wizard).</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Le meneur de CETTE bande (Warrior.IsLeader) - null si mort/retraité/malade cette partie
    /// (absent de WarriorRows), auquel cas la carte reste simplement vide plutôt qu'une erreur bloquante,
    /// même idiome que BonusStatTestLeader (Exploration, Bâtiment Éventré).</summary>
    public WarriorOutcomeRow? DuelLeaderRow => WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader && !r.IsDeadBeforeDuel);

    /// <summary>Cartes « Une Poignée d'Or » qui finissent en duel (2026-11-14 : un duel par personnage ou duo impayé).</summary>
    public List<FistfulOfCrownsEntry> DuelEntries => ActiveFistfulOfCrownsEntries.Where(e => e.WantsDuel).ToList();

    public bool WantsDuel => DuelEntries.Count > 0;

    /// <summary>Le meneur meurt s'il perd un seul de ses duels.</summary>
    public bool LostAnyDuel => DuelEntries.Any(e => !e.WonDuel);

    /// <summary>Équipement de départ d'un adversaire du duel, résolu en amont (voir
    /// WarbandDetailViewModel.EndOfGame - EquipmentQuantityChip.GroupFrom, même helper que
    /// DetailDialogService.ShowDramatisPersonaDetailDialogAsync) - 2026-09-03, retour utilisateur : "les
    /// armes ne sont pas affichées... dans le combat contre la paire". DramatisPersona.StartingEquipmentIds
    /// lui-même reste une simple liste d'ids catalogue (voir sa doc), jamais résolue sur le modèle - ce
    /// dictionnaire couvre tout le catalogue, pas seulement Marquand/Ulli (peu coûteux, réutilisable si
    /// besoin ailleurs).</summary>
    private IReadOnlyDictionary<int, List<EquipmentQuantityChip>> _dramatisPersonaStartingEquipmentById = new Dictionary<int, List<EquipmentQuantityChip>>();

    private List<EquipmentQuantityChip> ResolveDuelOpponentEquipment(DramatisPersona persona) => _dramatisPersonaStartingEquipmentById.GetValueOrDefault(persona.Id) ?? new();

    [RelayCommand]
    private Task ShowDuelEquipmentDetail(EquipmentQuantityChip chip) => _detailDialogs.ShowEquipmentDetailDialogAsync(chip.Item);

    /// <summary>2026-09-04, retour utilisateur - "dans le duel avec la paire les chips des compétence
    /// n'ouvre pas le detail de la compétence" : la chip Compétence de DuelOpponentDisplay n'avait aucune
    /// Command câblée en XAML (contrairement à Équipement ci-dessus) - même patron que ShowSkillDetail
    /// (Advance.cs)/ShowInjurySkillDetail (Injury.cs).</summary>
    [RelayCommand]
    private Task ShowDuelSkillDetail(Skill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill);

    /// <summary>Règles spéciales de combat d'un adversaire du duel (DramatisPersona.SpecialRules, déjà
    /// résolues côté catalogue) - 2026-09-04, retour utilisateur ("il faut rajouter dans les 2 cas les
    /// règles spéciales du combat").</summary>
    [RelayCommand]
    private Task ShowDuelSpecialRuleDetail(SpecialRule rule) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule);

    /// <summary>Rien à valider (2026-09-03, retour utilisateur - "en cas de défaite, le chef de bande est
    /// forcément mort") : la case WonDuel suffit à elle seule, victoire ou défaite sont chacune une
    /// conséquence directe, sans jet supplémentaire à saisir. Remplace l'ancien sous-jet D66 sur la table
    /// des Blessures Graves (PairDuelRoll, retiré) - pas dans le texte de cette règle-ci, contrairement à
    /// Vendu aux Fosses dont elle s'inspirait par erreur.</summary>
    private bool ValidatePairDuelStep() => true;
}

/// <summary>Un profil de duel (nom/stats/compétences via Persona, équipement déjà résolu à part - voir
/// EndOfGamePageViewModel.DuelOpponents's own doc). Type de haut niveau plutôt qu'imbriqué dans la
/// classe du ViewModel, pour rester référençable depuis x:DataType en XAML sans la syntaxe de type
/// imbriqué (peu fiable côté compilateur XAML source-gen de ce projet).</summary>
public sealed record DuelOpponentDisplay(DramatisPersona Persona, List<EquipmentQuantityChip> Equipment);
