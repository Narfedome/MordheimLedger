using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Features.Warbands.EndOfGame;
using MordheimLedgerApp.Features.Warbands.StartGame;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Commande "Lancer la partie" - remplace "Fin de partie" sur WarbandDetailPage tant qu'aucune
/// partie n'est en cours (voir Warband.GameInProgress/HasGameInProgress), demande explicite de
/// l'utilisateur (2026-08-26) : "Start a game" -> "End of game", un bouton à la fois. Wizard informatif
/// à un seul écran (StartGameDialog) - PAS de verrouillage roster/inventaire (décision explicite, rien
/// n'est bloqué pendant qu'une partie est "en cours"), seulement des rappels + le jet répétitif de
/// Vieille blessure (32, le seul résultat de la table des Blessures Graves qui se rejoue à chaque
/// partie plutôt qu'une fois à la Fin de Partie précédente).</summary>
public partial class WarbandDetailViewModel
{
    [RelayCommand]
    private async Task StartGame()
    {
        if (Warband is null) return;

        var unavailableWarriors = new List<UnavailableWarriorRow>();
        foreach (var row in Heroes.Concat(Henchmen).Where(r => r.Warrior.Status == WarriorStatus.Sick))
            unavailableWarriors.Add(new UnavailableWarriorRow(row, Loc["WarriorStatusSick"]));
        foreach (var row in RetiredWarriors)
            unavailableWarriors.Add(new UnavailableWarriorRow(row, Loc["WarriorStatusRetired"]));
        foreach (var row in DeadWarriors)
            unavailableWarriors.Add(new UnavailableWarriorRow(row, Loc["WarriorStatusDead"]));

        // Vieille blessure (32) : seul résultat mécanisé qui exige un jet avant CHAQUE bataille plutôt
        // qu'une fois à la Fin de Partie où il a été obtenu - voir SERIOUS_INJURIES_STATUS.md. Restreint
        // aux guerriers Actifs (un guerrier Malade/Mort/Retraité ne combat de toute façon pas cette
        // partie, inutile de lui faire passer ce test). UNE carte PAR guerrier, avec UN jet PAR instance
        // de l'Injury qu'il porte à l'intérieur - retour utilisateur (2026-08-26) : "si on a plusieurs
        // oldwound, on doit tirer le nombre de old wound... 2 old wound = 2 jets" pour la mécanique, "au
        // lieu d'avoir 2 card, c'est d'avoir une card avec 2 roll à l'intérieur" pour l'affichage - un
        // guerrier qui a accumulé 2 Vieilles blessures distinctes (2 résultats Serious Injury différents
        // tombés sur 32 au fil des parties) teste chacune indépendamment, un seul échec parmi ses jets
        // suffit à le sortir de la partie (OldWoundWarriorEntry.HasFailure).
        var oldWoundEntries = Heroes.Concat(Henchmen)
            .Where(r => r.Warrior.Status == WarriorStatus.Active)
            .Select(r => (r.Warrior, Count: r.Warrior.Injuries.Count(i => InjuryCatalogLookup.RollRangeMatches(i.Item.RollRange, 32))))
            .Where(x => x.Count > 0)
            .Select(x => new OldWoundWarriorEntry(x.Warrior,
                Enumerable.Range(1, x.Count).Select(n => new OldWoundRollEntry(x.Count > 1 ? $"{Loc["StartGameOldWoundRollLabel"]} {n}" : string.Empty)).ToList()))
            .ToList();

        var dialogViewModel = new StartGameDialogViewModel(unavailableWarriors, oldWoundEntries, Warband.NextGameNote);
        if (await ShowDialogAsync(new StartGameDialog(dialogViewModel)) != true) return;

        await Loading.RunAsync(async () =>
        {
            // Un guerrier qui rate un de ses jets (une seule Vieille blessure suffit) ne joue pas cette
            // partie - marqué Malade avec 1 partie restante (même mécanisme que le Puits de
            // l'Exploration) : ça l'exclut correctement d'`activeWarriorRows` si "Fin de partie" est
            // ouvert avant qu'il ait rejoué, contrairement à avant ce correctif où l'échec n'était que
            // journalisé, sans conséquence sur son statut - le guerrier restait Actif et se retrouvait
            // à tort dans le wizard Fin de Partie alors qu'il n'avait pas participé (retour utilisateur
            // 2026-08-26). Seuls les échecs sont journalisés dans l'Historique - un guerrier qui passe
            // le test n'a rien de notable à consigner.
            var sentences = new List<string>();
            foreach (var entry in dialogViewModel.OldWoundEntries.Where(e => e.HasFailure))
            {
                entry.Warrior.Status = WarriorStatus.Sick;
                entry.Warrior.SickGamesRemaining += 1;
                await _warbandService.SaveWarriorAsync(entry.Warrior);
                sentences.Add(string.Format(Loc["HistoryOldWoundFailSentence"], entry.Warrior.Name));
            }
            if (sentences.Count > 0)
                await _warbandService.AddHistoryEntryAsync(Warband.Id, string.Join(" ", sentences));

            Warband.GameInProgress = true;
            await _warbandService.SaveWarbandAsync(Warband);
            await LoadAsync(Warband.Id);
        });
    }
}
