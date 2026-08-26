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
        // partie, inutile de lui faire passer ce test). UN jet PAR instance de l'Injury portée, pas un
        // par guerrier - retour utilisateur (2026-08-26) : "si on a plusieurs oldwound, on doit tirer le
        // nombre de old wound... 2 old wound = 2 jets... ça augmente les chances de ne pas jouer" - un
        // guerrier qui a accumulé 2 Vieilles blessures distinctes (2 résultats Serious Injury différents
        // tombés sur 32 au fil des parties) teste chacune indépendamment, un seul échec suffit à le
        // sortir de la partie.
        var oldWoundRolls = Heroes.Concat(Henchmen)
            .Where(r => r.Warrior.Status == WarriorStatus.Active)
            .SelectMany(r =>
            {
                var count = r.Warrior.Injuries.Count(i => InjuryCatalogLookup.RollRangeMatches(i.Item.RollRange, 32));
                return Enumerable.Range(1, count).Select(n => new OldWoundRollEntry(r.Warrior,
                    count > 1 ? $"{Loc["StartGameOldWoundSubtitle"]} ({n}/{count})" : Loc["StartGameOldWoundSubtitle"]));
            })
            .ToList();

        var dialogViewModel = new StartGameDialogViewModel(unavailableWarriors, oldWoundRolls, Warband.NextGameNote);
        if (await ShowDialogAsync(new StartGameDialog(dialogViewModel)) != true) return;

        await Loading.RunAsync(async () =>
        {
            // Seuls les échecs sont journalisés - un guerrier qui passe le test n'a rien de notable à
            // consigner (même principe que le reste de l'Historique : uniquement les événements qui
            // changent quelque chose pour la bande). Distinct par guerrier (référence) : un guerrier à 2
            // Vieilles blessures qui échoue les deux ne doit apparaître qu'une fois - il ne joue pas
            // cette partie, peu importe combien de jets l'ont décidé.
            var sentences = dialogViewModel.OldWoundRolls
                .Where(r => r.CanFight == false)
                .Select(r => r.Warrior)
                .Distinct()
                .Select(w => string.Format(Loc["HistoryOldWoundFailSentence"], w.Name))
                .ToList();
            if (sentences.Count > 0)
                await _warbandService.AddHistoryEntryAsync(Warband.Id, string.Join(" ", sentences));

            Warband.GameInProgress = true;
            await _warbandService.SaveWarbandAsync(Warband);
            await LoadAsync(Warband.Id);
        });
    }
}
