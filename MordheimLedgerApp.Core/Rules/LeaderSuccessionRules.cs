using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Un Héros qui peut prendre la tête de la bande (voir LeaderSuccessionRules.IsEligible).</summary>
/// <param name="IsDesignatedSuccessor">Son type porte « Successeur imposé » (WarriorArchetype.IsDesignatedSuccessor,
/// ex. le Nécromancien).</param>
public sealed record SuccessionCandidate(int WarriorId, int Leadership, int Experience, bool IsDesignatedSuccessor = false);

public enum SuccessionKind
{
    /// <summary>Un seul Héros a le meilleur Commandement (puis le plus d'XP) : il est proposé.</summary>
    ByLeadership,
    /// <summary>Plusieurs Héros à égalité de Commandement et d'XP : un D6 les départage (TiedWarriorIds).</summary>
    Tie,
    /// <summary>Le type de bande impose un successeur et la bande en a un : il est proposé.</summary>
    DesignatedSuccessor,
    /// <summary>Le type de bande impose un successeur et la bande n'en a aucun : le livre dit qu'elle s'effondre.</summary>
    NoDesignatedSuccessor,
    /// <summary>Aucun Héros pour prendre la tête (le livre ne prévoit pas ce cas).</summary>
    NoCandidate
}

public enum LeaderRecruitmentNotice
{
    None,
    /// <summary>Ambre : le livre interdit de recruter un nouveau chef.</summary>
    NewLeaderNotAllowed,
    /// <summary>Ambre : rachetable seulement après la prochaine partie (le Vampire).</summary>
    RehireOnlyAfterNextGame,
    /// <summary>Info : le chef racheté reprend la tête, le successeur perd la règle Chef.</summary>
    ReplacesActingLeader
}

/// <summary>Proposition de l'appli ; le joueur peut toujours désigner un autre Héros (message ambre, jamais
/// bloquant).</summary>
public sealed record SuccessionProposal(SuccessionKind Kind, int? ProposedWarriorId, IReadOnlyList<int> TiedWarriorIds);

/// <summary>Mort du chef (livre p. 117 + errata) : le Héros au meilleur Commandement prend la tête de la bande ; à
/// égalité, celui qui a le plus d'XP ; encore à égalité, un D6. Le type de bande peut imposer un successeur (Morts-
/// Vivants : le Nécromancien ; sans lui la bande s'effondre). Réglages portés par le Codex
/// (WarriorArchetype.IsDesignatedSuccessor), jamais par le nom d'une bande, pour qu'une bande custom puisse les
/// reprendre.</summary>
public static class LeaderSuccessionRules
{
    /// <summary>Peut prendre la tête : un Héros de la bande encore vivant (Franc-Tireur et Dramatis Persona exclus, ils
    /// ne font pas partie de la bande), y compris un Homme de main promu Héros pendant la même Fin de partie.</summary>
    public static bool IsEligible(Warrior warrior) =>
        warrior.IsHero && !warrior.IsHiredSword && !warrior.IsDramatisPersona && !warrior.IsLeader
        && warrior.Status != WarriorStatus.Dead && warrior.Status != WarriorStatus.Retired;

    /// <param name="candidates">Héros éligibles (voir IsEligible).</param>
    /// <param name="requiresDesignatedSuccessor">Au moins un type de guerrier de la bande porte « Successeur imposé ».</param>
    public static SuccessionProposal Propose(IReadOnlyList<SuccessionCandidate> candidates, bool requiresDesignatedSuccessor)
    {
        if (requiresDesignatedSuccessor)
        {
            var designated = Rank(candidates.Where(c => c.IsDesignatedSuccessor)).FirstOrDefault();
            return designated is null
                ? new SuccessionProposal(SuccessionKind.NoDesignatedSuccessor, null, Array.Empty<int>())
                : new SuccessionProposal(SuccessionKind.DesignatedSuccessor, designated.WarriorId, Array.Empty<int>());
        }

        if (candidates.Count == 0)
            return new SuccessionProposal(SuccessionKind.NoCandidate, null, Array.Empty<int>());

        var ranked = Rank(candidates).ToList();
        var best = ranked[0];
        var tied = ranked.TakeWhile(c => c.Leadership == best.Leadership && c.Experience == best.Experience)
            .Select(c => c.WarriorId).ToList();
        return tied.Count > 1
            ? new SuccessionProposal(SuccessionKind.Tie, null, tied)
            : new SuccessionProposal(SuccessionKind.ByLeadership, best.WarriorId, Array.Empty<int>());
    }

    /// <summary>Départage au D6 : le plus haut jet l'emporte ; null si les meilleurs jets sont encore à égalité (on
    /// relance). rolls = WarriorId → jet, un par Héros à égalité.</summary>
    public static int? ResolveTie(IReadOnlyDictionary<int, int> rolls)
    {
        if (rolls.Count == 0) return null;
        var top = rolls.Values.Max();
        var winners = rolls.Where(r => r.Value == top).Select(r => r.Key).ToList();
        return winners.Count == 1 ? winners[0] : null;
    }

    /// <summary>Message à afficher quand on recrute un guerrier d'un type Chef (jamais un blocage) : le livre interdit
    /// de recruter un nouveau chef, sauf un type « Rachetable après sa mort » (le Vampire) à partir de la partie qui
    /// suit sa mort - il reprend alors la tête de la bande à son successeur.</summary>
    /// <param name="leaderDiedThisGame">Le chef est mort pendant la partie que la Fin de partie en cours enregistre.</param>
    public static LeaderRecruitmentNotice RecruitmentNotice(bool archetypeIsLeader, bool canBeRehiredAfterDeath, bool leaderDiedThisGame)
    {
        if (!archetypeIsLeader) return LeaderRecruitmentNotice.None;
        if (!canBeRehiredAfterDeath) return LeaderRecruitmentNotice.NewLeaderNotAllowed;
        return leaderDiedThisGame ? LeaderRecruitmentNotice.RehireOnlyAfterNextGame : LeaderRecruitmentNotice.ReplacesActingLeader;
    }

    private static IEnumerable<SuccessionCandidate> Rank(IEnumerable<SuccessionCandidate> candidates) =>
        candidates.OrderByDescending(c => c.Leadership).ThenByDescending(c => c.Experience);
}
