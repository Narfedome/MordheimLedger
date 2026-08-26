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
        // partie, inutile de lui faire passer ce test).
        var oldWoundRolls = Heroes.Concat(Henchmen)
            .Where(r => r.Warrior.Status == WarriorStatus.Active
                && r.Warrior.Injuries.Any(i => InjuryCatalogLookup.RollRangeMatches(i.Item.RollRange, 32)))
            .Select(r => new OldWoundRollEntry(r.Warrior))
            .ToList();

        var dialogViewModel = new StartGameDialogViewModel(unavailableWarriors, oldWoundRolls, Warband.NextGameNote);
        if (await ShowDialogAsync(new StartGameDialog(dialogViewModel)) != true) return;

        await Loading.RunAsync(async () =>
        {
            // Seuls les échecs sont journalisés - un guerrier qui passe le test n'a rien de notable à
            // consigner (même principe que le reste de l'Historique : uniquement les événements qui
            // changent quelque chose pour la bande).
            var sentences = dialogViewModel.OldWoundRolls
                .Where(r => r.CanFight == false)
                .Select(r => string.Format(Loc["HistoryOldWoundFailSentence"], r.Warrior.Name))
                .ToList();
            if (sentences.Count > 0)
                await _warbandService.AddHistoryEntryAsync(Warband.Id, string.Join(" ", sentences));

            Warband.GameInProgress = true;
            await _warbandService.SaveWarbandAsync(Warband);
            await LoadAsync(Warband.Id);
        });
    }
}
