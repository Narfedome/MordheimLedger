using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Ce que l'Exploration de cette Fin de partie rapporte, déjà décidé par le wizard (jets faits, choix
/// du joueur faits). Les objets trouvés n'y figurent pas : ils entrent dans la réserve avec les autres gains
/// de la partie (voir le flux de réserve du wizard).</summary>
public sealed record ExplorationRewards
{
    /// <summary>Pierres magiques : barème des dés + résultat Pierre magique + lignes Trésor Caché/Bande Massacrée.</summary>
    public int WyrdstoneShards { get; init; }

    /// <summary>Or : résultat Or + lignes Trésor Caché/Bande Massacrée (peut être négatif en théorie).</summary>
    public int Gold { get; init; }

    /// <summary>Traînard, autres bandes : un dé de plus à la prochaine Exploration.</summary>
    public bool GrantsNextExplorationBonusDie { get; init; }

    /// <summary>Cimetière : pense-bête pour la prochaine partie (remplace celui de la partie précédente).</summary>
    public string? NextGameNote { get; init; }

    /// <summary>Entrée des Catacombes : relance permanente (acquise une seule fois).</summary>
    public bool GrantsCatacombReroll { get; init; }

    /// <summary>XP accordée au chef (Traînard, Possédés) - null si le chef n'est pas disponible.</summary>
    public (Warrior Leader, int Experience)? LeaderExperience { get; init; }

    /// <summary>XP répartie par le joueur entre ses Héros (Prisonniers, Possédés).</summary>
    public IReadOnlyList<(Warrior Hero, int Experience)> DistributedExperience { get; init; } = [];

    /// <summary>Hommes de main offerts (Zombies) : rejoignent un groupe existant du même type, sinon un nouveau groupe.</summary>
    public (WarriorArchetype Archetype, int Count)? FreeHenchmen { get; init; }

    /// <summary>Prisonnier équipé qui rejoint un groupe : +1 modèle, seul le coût de son équipement est payé.</summary>
    public (Warrior Group, int EquipmentCost)? EquippedHenchman { get; init; }

    /// <summary>Sanctuaire : bénédiction posée sur une arme déjà portée.</summary>
    public (int WarriorEquipmentId, int BlessingRuleId)? WeaponBlessing { get; init; }

    /// <summary>Une Faveur Rendue : Franc-Tireur offert, solde de la prochaine partie déjà réglée.</summary>
    public (HiredSword HiredSword, IReadOnlyList<EquipmentItem> StartingEquipment)? FreeHiredSword { get; init; }
}

/// <summary>Application en base des récompenses de l'Exploration (sortie du wizard le 2026-11-11 pour être
/// testée, même principe qu'AdvancementService). Aucune phrase d'Historique : le wizard les écrit.</summary>
public sealed class ExplorationRewardService
{
    private readonly IWarbandService _warbands;

    public ExplorationRewardService(IWarbandService warbands) => _warbands = warbands;

    /// <param name="roster">Guerriers de la bande, pour fusionner des Hommes de main offerts dans un groupe
    /// existant du même type.</param>
    public async Task ApplyAsync(Warband warband, IEnumerable<Warrior> roster, ExplorationRewards rewards)
    {
        // Rappels de la partie précédente : consommés qu'ils aient servi ou non, puis éventuellement reposés.
        warband.PendingExplorationBonusDie = rewards.GrantsNextExplorationBonusDie;
        warband.NextGameNote = rewards.NextGameNote;
        warband.WyrdstoneShards += rewards.WyrdstoneShards;
        warband.Treasury += rewards.Gold;
        if (rewards.GrantsCatacombReroll) warband.HasCatacombReroll = true;
        if (rewards.EquippedHenchman is { EquipmentCost: > 0 } paid) warband.Treasury -= paid.EquipmentCost;
        await _warbands.SaveWarbandAsync(warband);

        if (rewards.LeaderExperience is { } leader)
        {
            leader.Leader.Experience += leader.Experience;
            await _warbands.SaveWarriorAsync(leader.Leader);
        }

        foreach (var (hero, experience) in rewards.DistributedExperience.Where(d => d.Experience > 0))
        {
            hero.Experience += experience;
            await _warbands.SaveWarriorAsync(hero);
        }

        if (rewards.FreeHenchmen is { } free && free.Count > 0)
        {
            var existingGroup = roster.FirstOrDefault(w => !w.IsHero && w.WarriorArchetypeId == free.Archetype.Id);
            if (existingGroup is not null)
            {
                existingGroup.HeadCount += free.Count;
                await _warbands.SaveWarriorAsync(existingGroup);
            }
            else
            {
                await _warbands.RecruitWarriorAsync(warband.Id, free.Archetype, free.Archetype.Name, headCount: free.Count);
            }
        }

        if (rewards.EquippedHenchman is { } joined)
        {
            joined.Group.HeadCount += 1;
            await _warbands.SaveWarriorAsync(joined.Group);
        }

        if (rewards.WeaponBlessing is { } blessing)
            await _warbands.SetWarriorEquipmentBlessingRuleAsync(blessing.WarriorEquipmentId, blessing.BlessingRuleId);

        if (rewards.FreeHiredSword is { } hiredSword)
        {
            var recruited = await _warbands.RecruitHiredSwordAsync(warband.Id, hiredSword.HiredSword, hiredSword.HiredSword.Name, hiredSword.StartingEquipment);
            recruited.HiredSwordUpkeepPrepaid = true;
            await _warbands.SaveWarriorAsync(recruited);
        }
    }
}
