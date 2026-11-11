using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Sort d'un Héros ennemi capturé par la bande.</summary>
public enum CaptiveFate
{
    Ransomed,
    SoldToSlavers,
    KilledForZombie,
    SacrificedForXp,
}

/// <summary>Décision prise pour un captif ; Gold sert à la rançon et à la vente aux esclavagistes.</summary>
public sealed record CaptiveOutcome(CaptiveFate Fate, int Gold = 0);

/// <summary>Ce que la bande décide pour la solde d'un Franc-Tireur ou d'un personnage spécial.</summary>
public enum UpkeepChoice
{
    /// <summary>Solde déjà couverte (« Une Faveur Rendue ») : consommée, rien n'est payé.</summary>
    Prepaid,
    PayGold,
    PayWyrdstone,
    /// <summary>Solde refusée ou impayée : il quitte la bande, fiche supprimée.</summary>
    Refuse,
}

public sealed record UpkeepDecision(Warrior Warrior, UpkeepChoice Choice, int Cost);

/// <summary>Personnage spécial vagabond qui repart à la fin de la partie ; CooldownPersonaId posé si sa
/// recherche doit attendre une bataille (Aenur, Ulli et Marquand).</summary>
public sealed record WandererDeparture(Warrior Warrior, int? CooldownPersonaId);

/// <summary>Écritures des étapes de la séquence d'après-bataille qui ne relèvent ni des guerriers
/// (InjuryService, AdvancementService), ni de l'Exploration (ExplorationRewardService), ni du commerce
/// (PostBattleTradingService) ni des changements d'effectif (RosterChangeService) : captifs, vente de pierre
/// magique... Comme les autres services de la Fin de partie, reçoit des décisions déjà prises - aucun jet,
/// aucune phrase d'Historique (le wizard garde l'UI, les jets et la localisation).</summary>
public sealed class PostBattleSequenceService
{
    /// <summary>Expérience que le chef gagne par captif sacrifié (Culte des Possédés).</summary>
    public const int SacrificeExperience = 1;

    private readonly IWarbandService _warbands;

    public PostBattleSequenceService(IWarbandService warbands) => _warbands = warbands;

    /// <summary>Captifs : rançon et vente aux esclavagistes rapportent leur montant ; un captif tué devient un
    /// Zombie qui rejoint le groupe de Zombies existant (ou en crée un) - seulement si zombieArchetype est connu ;
    /// un captif sacrifié donne +1 XP au chef s'il est encore là. Renvoie le nombre de sacrifices appliqués (0
    /// sans chef), pour l'Historique.</summary>
    public async Task<int> ApplyCaptivesAsync(Warband warband, IReadOnlyList<CaptiveOutcome> captives, IEnumerable<Warrior> roster,
        WarriorArchetype? zombieArchetype, Warrior? leader)
    {
        var sacrifices = 0;
        foreach (var captive in captives)
        {
            switch (captive.Fate)
            {
                case CaptiveFate.Ransomed or CaptiveFate.SoldToSlavers:
                    warband.Treasury += Math.Max(0, captive.Gold);
                    break;

                case CaptiveFate.KilledForZombie when zombieArchetype is not null:
                    var group = roster.FirstOrDefault(w => !w.IsHero && w.HeadCount > 0 && w.WarriorArchetypeId == zombieArchetype.Id);
                    if (group is not null)
                    {
                        group.HeadCount += 1;
                        await _warbands.SaveWarriorAsync(group);
                    }
                    else
                    {
                        var recruited = await _warbands.RecruitWarriorAsync(warband.Id, zombieArchetype, zombieArchetype.Name, headCount: 1);
                        roster = roster.Append(recruited).ToList(); // un second Zombie de la même partie le rejoint
                    }
                    break;

                case CaptiveFate.SacrificedForXp when leader is not null:
                    leader.Experience += SacrificeExperience;
                    sacrifices++;
                    break;
            }
        }
        if (sacrifices > 0) await _warbands.SaveWarriorAsync(leader!);
        await _warbands.SaveWarbandAsync(warband);
        return sacrifices;
    }

    /// <summary>Vente de pierre magique : les éclats vendus (jamais plus que le stock) sortent du stock, l'or
    /// (calculé par le wizard depuis la table de vente) entre dans la trésorerie. Renvoie les éclats réellement
    /// vendus.</summary>
    public async Task<int> SellWyrdstoneAsync(Warband warband, int shards, int gold)
    {
        var sold = Math.Clamp(shards, 0, warband.WyrdstoneShards);
        if (sold == 0) return 0;
        warband.WyrdstoneShards -= sold;
        warband.Treasury += Math.Max(0, gold);
        await _warbands.SaveWarbandAsync(warband);
        return sold;
    }

    /// <summary>Récompenses du scénario : or (peut être négatif, une perte prévue par le scénario) et éclats. Les
    /// objets passent par la réserve (PostBattleTradingService.ApplyReserveAsync).</summary>
    public async Task ApplyScenarioRewardsAsync(Warband warband, int gold, int shards)
    {
        if (gold == 0 && shards <= 0) return;
        warband.Treasury += gold;
        warband.WyrdstoneShards += Math.Max(0, shards);
        await _warbands.SaveWarbandAsync(warband);
    }

    /// <summary>Soldes des Francs-Tireurs et personnages spéciaux : payée en or ou en pierre magique, déjà
    /// couverte (le prépaiement est consommé), ou refusée (il quitte la bande).</summary>
    public async Task ApplyUpkeepAsync(Warband warband, IReadOnlyList<UpkeepDecision> decisions)
    {
        var warbandChanged = false;
        foreach (var decision in decisions)
        {
            switch (decision.Choice)
            {
                case UpkeepChoice.Prepaid:
                    decision.Warrior.HiredSwordUpkeepPrepaid = false;
                    await _warbands.SaveWarriorAsync(decision.Warrior);
                    break;
                case UpkeepChoice.PayGold:
                    warband.Treasury -= Math.Max(0, decision.Cost);
                    warbandChanged = true;
                    break;
                case UpkeepChoice.PayWyrdstone:
                    warband.WyrdstoneShards -= Math.Max(0, decision.Cost);
                    warbandChanged = true;
                    break;
                case UpkeepChoice.Refuse:
                    await _warbands.DeleteWarriorAsync(decision.Warrior.Id);
                    break;
            }
        }
        if (warbandChanged) await _warbands.SaveWarbandAsync(warband);
    }

    /// <summary>Nouveau Franc-Tireur embauché après la bataille : recruté avec son équipement de départ, prix
    /// d'embauche débité.</summary>
    public async Task<Warrior> HireAsync(Warband warband, HiredSword hiredSword, string name, IReadOnlyList<EquipmentItem> startingEquipment)
    {
        var recruited = await _warbands.RecruitHiredSwordAsync(warband.Id, hiredSword, name, startingEquipment.ToList());
        warband.Treasury -= hiredSword.HireCost;
        await _warbands.SaveWarbandAsync(warband);
        return recruited;
    }

    /// <summary>Vagabonds qui repartent : fiche supprimée, et délai de re-recherche posé si besoin.</summary>
    public async Task DepartWanderersAsync(int warbandId, IEnumerable<WandererDeparture> departures)
    {
        foreach (var departure in departures)
        {
            await _warbands.DeleteWarriorAsync(departure.Warrior.Id);
            if (departure.CooldownPersonaId is { } personaId)
                await _warbands.AddDramatisPersonaCooldownAsync(warbandId, personaId);
        }
    }

    /// <summary>« Une Poignée d'Or » : la bande paie pour gagner ou garder Ulli et Marquand (rien si le montant
    /// est nul).</summary>
    public async Task PayPairAsync(Warband warband, int amount)
    {
        if (amount <= 0) return;
        warband.Treasury -= amount;
        await _warbands.SaveWarbandAsync(warband);
    }

    /// <summary>Duel perdu contre Ulli et Marquand : le meneur meurt, sans jet de Blessure.</summary>
    public async Task LoseDuelAsync(Warrior leader)
    {
        leader.Status = WarriorStatus.Dead;
        await _warbands.SaveWarriorAsync(leader);
    }

    /// <summary>Statuts posés en tout dernier (après les issues des guerriers, qui écraseraient sinon le statut) :
    /// les guerriers Indisponibles qui ont manqué cette partie décomptent une partie et redeviennent Actifs à
    /// 0 (leurs blessures temporaires tombent alors) ; le Héros qui a raté le Puits devient Indisponible pour la
    /// partie suivante ; celui dévoré dans la Fosse meurt.</summary>
    public async Task ApplySicknessAsync(IEnumerable<Warrior> previouslySick, Warrior? newlySick, Warrior? devoured)
    {
        foreach (var warrior in previouslySick)
        {
            warrior.SickGamesRemaining = Math.Max(0, warrior.SickGamesRemaining - 1);
            if (warrior.SickGamesRemaining == 0)
            {
                warrior.Status = WarriorStatus.Active;
                await _warbands.RemoveTemporaryInjuriesAsync(warrior.Id);
            }
            await _warbands.SaveWarriorAsync(warrior);
        }

        if (newlySick is not null)
        {
            newlySick.Status = WarriorStatus.Sick;
            newlySick.SickGamesRemaining = 1;
            await _warbands.SaveWarriorAsync(newlySick);
        }

        if (devoured is not null)
        {
            devoured.Status = WarriorStatus.Dead;
            await _warbands.SaveWarriorAsync(devoured);
        }
    }
}
