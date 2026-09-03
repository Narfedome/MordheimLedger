using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Duel avec le meneur" (StepKind.PairDuel, 2026-09-01, retour utilisateur - "quitte à le
/// mettre dans un step supplémentaire pour éviter une page trop longue") - sa propre étape, séparée de la
/// carte "Une Poignée d'Or" qui la déclenche (voir IsPairDuelStep/EndOfGameDialogViewModel.
/// PairEngagement.cs), pour ne pas surcharger cette dernière. Affiche le profil comparatif du meneur de
/// bande et du/des adversaire(s) - même principe que la comparaison "Ce guerrier"/"Le Gladiateur" de
/// Vendu aux Fosses (WarriorOutcomeRow.ShowSoldToThePits), généralisé à N participants (voir
/// DuelOpponents) plutôt que 2 fixes. Pas de moteur de combat : le joueur résout l'affrontement lui-même
/// à la table, coche simplement victoire/défaite (WonDuel).
///
/// **Générique depuis le 2026-09-04 (retour utilisateur - "si le personnage a la règle c'est l'heure de
/// payer, on réutilise la mécanique du combat/vol de magot") : DuelOpponents affiche 1 ou 2 cartes
/// selon PairedWithDramatisPersona, pas 2 fixes (Marquand + Ulli).** Ulli &amp; Marquand restent le seul
/// cas réel aujourd'hui (2 cartes) ; un futur Dramatis Persona solo portant "A Fistful of Crowns" sans
/// partenaire n'afficherait qu'une seule carte, sans rien à changer dans ce fichier ni le XAML.
///
/// **Position dans le wizard : toujours juste après l'étape "Une Poignée d'Or"** (voir
/// EndOfGameDialogViewModel.Steps) - un seul point d'ancrage possible depuis que Corruption et Rétention
/// partagent la même étape (2026-09-04, retour utilisateur), plus besoin de deux positions distinctes
/// comme avant ce regroupement (juste après Prisonniers si Corruption, juste après Dramatis Personae si
/// Rétention - une première version l'insérait même à une position FIXE avant tout ça, juste avant
/// Récapitulatif, trompeuse pour la Corruption qui se déclenchait tôt dans le wizard).</summary>
public partial class EndOfGameDialogViewModel
{
    /// <summary>Le meneur de CETTE bande (Warrior.IsLeader) - null si mort/retraité/malade cette partie
    /// (absent de WarriorRows), auquel cas la carte reste simplement vide plutôt qu'une erreur bloquante,
    /// même idiome que BonusStatTestLeader (Exploration, Bâtiment Éventré).</summary>
    public WarriorOutcomeRow? DuelLeaderRow => WarriorRows.FirstOrDefault(r => r.Warrior.IsLeader);

    /// <summary>Le Dramatis Persona "A Fistful of Crowns" concerné par CETTE bande, quel que soit le sens
    /// (Corruption via _pairPersona si non possédé, Rétention via _ownedPairPersona si déjà possédé -
    /// mutuellement exclusifs par bande, voir EndOfGameDialogViewModel.PairEngagement.cs) - toujours le
    /// représentant "visible" du groupe (IsHiddenFromSearchPicker exclut un éventuel partenaire caché du
    /// picker de recherche, ex. Ulli).</summary>
    private DramatisPersona? DuelPrimaryPersona => _ownedPairPersona ?? _pairPersona;

    /// <summary>1 ou 2 cartes selon que DuelPrimaryPersona a un partenaire (PairedWithDramatisPersona) ou
    /// non - Marquand + Ulli aujourd'hui (2 cartes, "Une Poignée d'Or" les corrompt/les garde toujours
    /// ensemble), mais généralise sans changement à un futur Dramatis Persona solo portant la même
    /// SpecialRule (1 carte). Vide si DuelPrimaryPersona est null (ex. carte encore vide avant résolution
    /// du catalogue - ne devrait pas arriver en pratique, cette étape n'existe que si ShowPairCorruptionOption/
    /// ShowPairRetentionOption est vrai).</summary>
    public List<DuelOpponentDisplay> DuelOpponents
    {
        get
        {
            if (DuelPrimaryPersona is not { } primary) return new List<DuelOpponentDisplay>();
            var list = new List<DuelOpponentDisplay> { new(primary, ResolveDuelOpponentEquipment(primary)) };
            if (primary.PairedWithDramatisPersona is { } partner) list.Add(new(partner, ResolveDuelOpponentEquipment(partner)));
            return list;
        }
    }

    /// <summary>Équipement de départ d'un adversaire du duel, résolu en amont (voir
    /// WarbandDetailViewModel.EndOfGame - EquipmentQuantityChip.GroupFrom, même helper que
    /// DetailDialogService.ShowDramatisPersonaDetailDialogAsync) - 2026-09-03, retour utilisateur : "les
    /// armes ne sont pas affichées... dans le combat contre la paire". DramatisPersona.StartingEquipmentIds
    /// lui-même reste une simple liste d'ids catalogue (voir sa doc), jamais résolue sur le modèle - ce
    /// dictionnaire couvre tout le catalogue, pas seulement Marquand/Ulli (peu coûteux, réutilisable si
    /// besoin ailleurs).</summary>
    private readonly IReadOnlyDictionary<int, List<EquipmentQuantityChip>> _dramatisPersonaStartingEquipmentById;

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

    /// <summary>Même principe que WonPitFight (Vendu aux Fosses, EndOfGameDialogViewModel.Injury.cs) mais
    /// appliqué au chef de bande de CETTE session plutôt qu'à un guerrier hors de combat - décoché par
    /// défaut (défaite). Défaite = mort automatique du meneur (2026-09-03, retour utilisateur), pas de
    /// jet à résoudre - voir WarbandDetailViewModel.EndOfGame.ApplyPairDuelIfNeededAsync.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDuelLost))]
    private bool wonDuel;

    /// <summary>Simple négation de WonDuel, exposée à part pour piloter IsVisible côté XAML (pas de
    /// convertisseur "inverse un bool" dans cette appli - même idiome que le reste du code, calculer en
    /// C# plutôt qu'ajouter un converter dédié pour un seul usage).</summary>
    public bool IsDuelLost => !WonDuel;
}

/// <summary>Un profil de duel (nom/stats/compétences via Persona, équipement déjà résolu à part - voir
/// EndOfGameDialogViewModel.DuelOpponents's own doc). Type de haut niveau plutôt qu'imbriqué dans la
/// classe du ViewModel, pour rester référençable depuis x:DataType en XAML sans la syntaxe de type
/// imbriqué (peu fiable côté compilateur XAML source-gen de ce projet).</summary>
public sealed record DuelOpponentDisplay(DramatisPersona Persona, List<EquipmentQuantityChip> Equipment);
