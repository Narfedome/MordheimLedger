using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Roster d'après-bataille : LA source de vérité sur qui est encore dans la bande, une fois les
/// Blessures résolues (retour utilisateur 2026-11-04 - "on manipule un seul et unique objet warband tout
/// du long jusqu'à la sauvegarde finale... si on a un mort au jet des blessures graves, derrière on ne
/// puisse pas le rééquiper").
///
/// Le wizard ne touche jamais la base avant Terminer : chaque guerrier reste le même objet Warrior en
/// mémoire (WarriorOutcomeRow.Warrior, partagé avec _allActiveWarriorRows), et sa mort éventuelle est
/// DÉDUITE en direct des saisies de l'étape Blessure (WarriorOutcomeRow.IsDead/SurvivingHeadCount) - un
/// retour en arrière qui change un jet ressuscite donc le guerrier partout. Toute étape ou application
/// postérieure aux Blessures (renfort d'un groupe, solde d'un Franc-Tireur, chef de bande, groupe rejoint
/// par une recrue gratuite...) passe par les membres ci-dessous plutôt que par WarriorRows/
/// _allActiveWarriorRows bruts, qui contiennent aussi les morts de cette bataille.
///
/// Les listes construites une seule fois à l'ouverture (HiredSwordUpkeepEntries,
/// DramatisPersonaUpkeepEntries, ExistingHenchmanTopUps) gardent toutes leurs entrées - un mort peut
/// redevenir vivant si le joueur revient changer son jet - mais ne sont plus lues qu'à travers leur vue
/// Living* ci-dessous, à l'écran comme dans le solde et à Terminer.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Mort à l'issue de CETTE bataille selon l'étape Blessure (voir WarriorOutcomeRow.IsDead).
    /// False pour un guerrier absent de WarriorRows (Malade cette partie, donc pas engagé) - il n'a pas pu
    /// mourir pendant une bataille qu'il n'a pas jouée.</summary>
    private bool IsDeadThisBattle(Warrior warrior) =>
        WarriorRows.FirstOrDefault(r => ReferenceEquals(r.Warrior, warrior))?.IsDead == true;

    /// <summary>Guerriers engagés dans cette bataille et toujours en vie après les Blessures.</summary>
    private IEnumerable<WarriorOutcomeRow> LivingWarriorRows => WarriorRows.Where(r => !r.IsDead);

    /// <summary>Le chef de bande s'il a survécu à cette bataille (engagé ou non) - null s'il vient d'y
    /// mourir : aucun bonus de chef (XP de Traînard/Prisonniers sacrifié, test de Bâtiment Éventré, duel
    /// d'Une Poignée d'Or) ne doit plus lui revenir.</summary>
    private WarriorRow? SurvivingLeader =>
        _allActiveWarriorRows.FirstOrDefault(r => r.Warrior.IsLeader && !IsDeadThisBattle(r.Warrior));

    /// <summary>Reporte sur les lignes du roster les deux morts décidées APRÈS l'étape Blessure, pour que
    /// toutes les étapes suivantes (renvoi, vente, recrutement, réallocation...) et Terminer les voient
    /// comme n'importe quelle autre mort (WarriorOutcomeRow.IsDead) : le Héros dévoré dans la Fosse
    /// (Exploration) et le chef vaincu en duel par Ulli &amp; Marquand (WonDuel décoché = défaite, comme à
    /// Terminer). Idempotente - appelée à chaque changement de PitDevouredHero/WantsDuel/WonDuel et à
    /// chaque changement d'étape ; un retour en arrière qui annule la cause annule la mort.</summary>
    private void SyncPostInjuryDeaths()
    {
        if (WarriorRows.Count == 0) return;

        var devoured = PitDevouredHero;
        var sick = StatTestSickHero;
        var duelLoser = WantsDuel && !WonDuel ? DuelLeaderRow : null;
        // Héros promus compris : désignables pour la Fosse et le Puits (voir EndOfGamePageViewModel.Promotion).
        foreach (var row in AllOutcomeRows)
        {
            row.IsKilledByExploration = ReferenceEquals(row, devoured);
            row.IsSickenedByExploration = ReferenceEquals(row, sick);
            row.IsKilledInDuel = ReferenceEquals(row, duelLoser);
        }
    }

    /// <summary>Francs-Tireurs toujours en vie : un Franc-Tireur mort cette bataille n'a plus de solde à
    /// payer (ni à refuser - le refus le supprimerait de la base au lieu de le laisser Mort).</summary>
    public IEnumerable<HiredSwordUpkeepEntry> LivingHiredSwordUpkeepEntries =>
        HiredSwordUpkeepEntries.Where(e => !IsDeadThisBattle(e.Warrior));

    /// <summary>Même principe que LivingHiredSwordUpkeepEntries pour les Dramatis Personae.</summary>
    public IEnumerable<DramatisPersonaUpkeepEntry> LivingDramatisPersonaUpkeepEntries =>
        DramatisPersonaUpkeepEntries.Where(e => !IsDeadThisBattle(e.Warrior));

    /// <summary>Groupes d'Hommes de main existants qu'on peut encore renforcer : un groupe dont toutes les
    /// figurines sont mortes cette bataille disparaît de la bande (supprimé à Terminer), on ne peut plus
    /// lui ajouter de recrues ni les équiper sur son modèle.</summary>
    public IEnumerable<ExistingHenchmanTopUp> LivingHenchmanTopUps => ExistingHenchmanTopUps.Where(t => !t.Row.IsDead);

    /// <summary>Appelée à chaque changement de WarriorOutcomeRow.IsDead (voir WireUpChangeNotifications) :
    /// annule ce qui avait été décidé pour ce guerrier dans une étape postérieure (renfort de son groupe)
    /// et rafraîchit tout ce qui dépend du roster vivant. Ne touche jamais aux saisies de l'étape Blessure
    /// elle-même, seule à décider de la mort.</summary>
    private void OnWarriorDeathStateChanged(WarriorOutcomeRow row)
    {
        if (row.IsDead)
        {
            foreach (var topUp in ExistingHenchmanTopUps.Where(t => t.Row == row && t.AddCount > 0))
                topUp.AddCount = 0;
        }

        RefreshHenchmanTopUpBreakdowns();
        UpdateRecruitRowsEligibility();
        NotifyRosterChanged();
        NotifyTreasuryChanged();
    }

    /// <summary>Toutes les vues "roster vivant" - aussi rappelée à chaque changement d'étape (les vues
    /// d'étape sont mises en cache, voir OnStepIndexChanged).</summary>
    private void NotifyRosterChanged()
    {
        OnPropertyChanged(nameof(LivingHiredSwordUpkeepEntries));
        OnPropertyChanged(nameof(LivingDramatisPersonaUpkeepEntries));
        OnPropertyChanged(nameof(LivingHenchmanTopUps));
        OnPropertyChanged(nameof(HasHiredSwordUpkeep));
        OnPropertyChanged(nameof(HasExistingHenchmanGroups));
        OnPropertyChanged(nameof(DuelLeaderRow));
        OnPropertyChanged(nameof(ShowPairCorruptionOption));
        OnPropertyChanged(nameof(ShowPairRetentionOption));
        OnPropertyChanged(nameof(BonusStatTestLeader));
        OnPropertyChanged(nameof(ReallocationCarriers));
        OnPropertyChanged(nameof(DismissibleWarriorRows));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(IsLastStep));
    }
}
