using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Features.Warbands.EndOfGame;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Commande "Fin de partie" - se réduit au garde + à la navigation vers EndOfGamePage depuis le
/// passage de ce wizard en page Shell (2026-09-22, voir CLAUDE.md) : toute la préparation (catalogues,
/// dictionnaires anglais->localisé, snapshot d'inventaire) et le pipeline de persistance (Apply*Async)
/// vivent désormais sur EndOfGamePageViewModel lui-même (InitializeAsync/FinishAsync,
/// EndOfGamePageViewModel.Apply.cs) - re-dérivés depuis warbandId seul plutôt que construits ici, puisque
/// rien de tout cela ne dépend d'une interaction utilisateur avant l'ouverture du wizard. Extrait de
/// WarbandDetailViewModel.cs à l'origine (refactor de découpage, voir CLAUDE.md) - gardé comme fichier à
/// part pour la continuité git-blame, même s'il ne reste plus que cette seule commande.</summary>
public partial class WarbandDetailViewModel
{
    [RelayCommand]
    private async Task EndOfGame()
    {
        if (Warband is null) return;

        // Un guerrier Malade (voir WarriorStatus.Sick) manque LA bataille que ce wizard s'apprête à
        // enregistrer - AllActiveWarriorRows (Heroes/Henchmen/HiredSwords/DramatisPersonae déjà chargés
        // par LoadAsync) suffit pour ce garde léger, sans avoir à refaire tout le chargement de
        // catalogues que EndOfGamePageViewModel.InitializeAsync fera de toute façon une fois la page
        // poussée - pas de travail dupliqué pour le cas le plus fréquent (il y a bien des guerriers
        // actifs).
        var hasActiveWarriors = AllActiveWarriorRows.Any(r => r.Warrior.Status == WarriorStatus.Active);
        if (!hasActiveWarriors)
        {
            await ShowInfoAsync(Loc["EndOfGameTitle"], Loc["EndOfGameNoWarriors"]);
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(EndOfGamePage)}?warbandId={Warband.Id}");
    }
}
