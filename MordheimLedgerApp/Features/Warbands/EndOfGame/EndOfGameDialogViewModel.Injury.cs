using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape Blessure (une carte par guerrier hors de combat coché, voir la doc de classe du
/// fichier principal) - validation + commandes de jet. Extrait de EndOfGameDialogViewModel.cs
/// (2026-08-18, refactor de découpage, voir CLAUDE.md) : aucun changement de comportement, pur
/// déplacement de membres.</summary>
public partial class EndOfGameDialogViewModel
{
    private bool ValidateInjuryStep(WarriorOutcomeRow row)
    {
        var valid = true;

        if (row.Warrior.IsHero)
        {
            valid &= CheckRoll(string.IsNullOrWhiteSpace(row.InjuryResultText), () => row.RollError = Loc["EndOfGameRollRequired"]);

            if (row.ShowMultipleInjuriesSection)
            {
                valid &= CheckRoll(row.MultipleInjuryRolls.Count == 0, () => row.MultipleInjuryCountError = Loc["EndOfGameRollRequired"]);
                foreach (var sub in row.MultipleInjuryRolls)
                    valid &= CheckRoll(string.IsNullOrWhiteSpace(sub.InjuryResultText), () => sub.RollError = Loc["EndOfGameRollRequired"]);
            }
        }
        else
        {
            foreach (var figure in row.FigureInjuryRolls)
                valid &= CheckRoll(string.IsNullOrWhiteSpace(figure.InjuryResultText), () => figure.RollError = Loc["EndOfGameRollRequired"]);
        }

        return valid;
    }

    // Lance les dés à la place du joueur (D66 pour un Héros, D6 pour un Homme de main - deux tables
    // totalement différentes, voir SeriousInjuryTable/HenchmanInjuryTable) - le champ ManualRoll reste
    // modifiable ensuite si le joueur préfère un jet physique. Dans les deux cas (dé ou saisie
    // manuelle), la résolution texte + ApplyInjuryRoll se fait automatiquement dès que ManualRoll
    // contient un jet complet et valide (voir WarriorOutcomeRow.OnManualRollChanged) et s'affiche tout
    // de suite sous le champ - plus de popup de confirmation après un clic sur le dé (décision
    // explicite du 2026-08-17, devenue redondante avec cet affichage automatique).
    [RelayCommand]
    private void AutoRoll(WarriorOutcomeRow row)
    {
        var roll = row.Warrior.IsHero ? SeriousInjuryTable.RollDice() : HenchmanInjuryTable.RollDice();
        row.ManualRoll = roll.ToString();
    }

    // Résultat "Blessures multiples" (16/21, Héros uniquement) : le joueur lance 1D6 pour savoir
    // combien de sous-jets faire sur cette même table (règle du livre : "Roll D6 times on this
    // table" = un nombre déterminé par 1D6, pas un compte fixe). Comme AutoRoll ci-dessus, une saisie
    // manuelle valide (1 à 6) peuple MultipleInjuryRolls toute seule (WarriorOutcomeRow.
    // OnMultipleInjuryCountRollChanged) - ce bouton ne fait que tirer le 1D6 à la place du joueur.
    [RelayCommand]
    private void AutoRollMultipleInjuryCount(WarriorOutcomeRow row) => row.MultipleInjuryCountRoll = Random.Shared.Next(1, 7).ToString();

    // Sert deux cas distincts qui partagent la même forme (voir la doc d'InjurySubRollEntry) : les
    // sous-jets "Blessures multiples" d'un Héros (D66, entry.IsHero true) et les jets par figurine d'un
    // groupe d'Hommes de main hors de combat (D6, entry.IsHero false). Même résolution automatique que
    // le jet principal (InjurySubRollEntry.OnManualRollChanged), y compris pour un résultat qui devrait
    // en théorie être relancé (Mort/Capturé/Blessures multiples, cf. livre, Héros uniquement) -
    // décision explicite : l'appli n'impose ni ne relance rien elle-même, le résultat du joueur est
    // accepté tel quel comme n'importe quel autre jet de cette table.
    [RelayCommand]
    private void AutoRollSubInjury(InjurySubRollEntry entry)
    {
        var roll = entry.IsHero ? SeriousInjuryTable.RollDice() : HenchmanInjuryTable.RollDice();
        entry.ManualRoll = roll.ToString();
    }
}
