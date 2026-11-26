using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Écritures liées au chef de la bande : succession après sa mort, retour d'un chef racheté (le Vampire), et
/// archivage d'une bande (effondrée ou abandonnée). Comme les autres services de la Fin de partie, il applique des
/// décisions déjà prises (le choix du Héros est fait dans le wizard, voir Core.Rules.LeaderSuccessionRules) et n'écrit
/// aucune phrase d'Historique.</summary>
public sealed class LeaderSuccessionService
{
    private readonly IWarbandService _warbands;

    public LeaderSuccessionService(IWarbandService warbands) => _warbands = warbands;

    /// <summary>newLeader prend la tête de la bande : il gagne la règle Chef (Warrior.IsLeader), tout autre guerrier la
    /// perd. Dans une bande « Le successeur apprend la magie » (successorLearnsMagic), un nouveau chef qui ne lançait
    /// pas encore de sorts (alreadySpellcaster faux) les apprend, avec un sort au choix à sa prochaine progression.</summary>
    public async Task ApplySuccessionAsync(IReadOnlyList<Warrior> roster, Warrior newLeader, bool successorLearnsMagic, bool alreadySpellcaster)
    {
        foreach (var warrior in roster.Where(w => w.Id != newLeader.Id && w.IsLeader))
        {
            warrior.IsLeader = false;
            await _warbands.SaveWarriorAsync(warrior);
        }

        newLeader.IsLeader = true;
        if (successorLearnsMagic && !alreadySpellcaster && !newLeader.CastsAsSuccessor)
        {
            newLeader.CastsAsSuccessor = true;
            newLeader.HasPendingSuccessorSpell = true;
        }
        await _warbands.SaveWarriorAsync(newLeader);
    }

    /// <summary>Un chef racheté (type « Rachetable après sa mort », ex. le Vampire) reprend la tête : le successeur en
    /// place perd la règle Chef (errata p. 117 : « whether he wants to or not »). Ne touche pas à la magie du
    /// successeur : la règle ne la lui retire pas.</summary>
    public async Task ApplyRehiredLeaderAsync(IReadOnlyList<Warrior> roster, Warrior rehiredLeader)
    {
        foreach (var warrior in roster.Where(w => w.Id != rehiredLeader.Id && w.IsLeader))
        {
            warrior.IsLeader = false;
            await _warbands.SaveWarriorAsync(warrior);
        }

        rehiredLeader.IsLeader = true;
        await _warbands.SaveWarriorAsync(rehiredLeader);
    }

    /// <summary>La première progression d'un successeur qui apprend la magie a été faite (sort choisi ou jet
    /// normal) : l'option du sort au choix disparaît.</summary>
    public async Task ConsumePendingSuccessorSpellAsync(Warrior warrior)
    {
        if (!warrior.HasPendingSuccessorSpell) return;
        warrior.HasPendingSuccessorSpell = false;
        await _warbands.SaveWarriorAsync(warrior);
    }

    /// <summary>Archive la bande : hors de la liste active, consultable en lecture seule. Une partie en cours est
    /// abandonnée.</summary>
    public async Task ArchiveAsync(Warband warband, DateTime archivedAt)
    {
        warband.IsArchived = true;
        warband.ArchivedAt = archivedAt;
        warband.GameInProgress = false;
        await _warbands.SaveWarbandAsync(warband);
    }

    public async Task UnarchiveAsync(Warband warband)
    {
        warband.IsArchived = false;
        warband.ArchivedAt = null;
        await _warbands.SaveWarbandAsync(warband);
    }
}
