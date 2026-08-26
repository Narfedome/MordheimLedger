using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
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
                {
                    valid &= CheckRoll(string.IsNullOrWhiteSpace(sub.InjuryResultText), () => sub.RollError = Loc["EndOfGameRollRequired"]);
                    if (sub.ShowDeepWoundSubRoll)
                        valid &= CheckRoll(!sub.HasValidDeepWoundSubRoll, () => sub.DeepWoundRollError = Loc["EndOfGameRollRequired"]);
                    if (sub.ShowCapturedChoice)
                        valid &= CheckRoll(sub.SelectedCapturedOutcome is null, () => sub.CapturedChoiceError = Loc["EndOfGameRollRequired"]);
                }
            }

            if (row.ShowHatredSection)
            {
                if (row.HatredScope is null)
                    valid &= CheckRoll(true, () => row.HatredRollError = Loc["EndOfGameRollRequired"]);
                else
                    valid &= CheckRoll(!row.HasHatredTarget, () => row.HatredRollError = Loc["EndOfGameHatredTargetRequired"]);
            }

            if (row.ShowInjuryBranchSubRoll)
                valid &= CheckRoll(!row.HasValidInjuryBranchSubRoll, () => row.InjuryBranchRollError = Loc["EndOfGameRollRequired"]);

            if (row.ShowDeepWoundSubRoll)
                valid &= CheckRoll(!row.HasValidDeepWoundSubRoll, () => row.DeepWoundRollError = Loc["EndOfGameRollRequired"]);

            if (row.ShowCapturedChoice)
                valid &= CheckRoll(row.SelectedCapturedOutcome is null, () => row.CapturedChoiceError = Loc["EndOfGameRollRequired"]);
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

    // Sous-jet 1D6 de "Rancune" (56, Héros uniquement) déterminant la portée de la Haine (voir
    // Core.Rules.HatredTargetTable) - le champ HatredSubRoll reste modifiable ensuite si le joueur
    // préfère un jet physique, même convention que les autres jets de cette étape.
    [RelayCommand]
    private void AutoRollHatred(WarriorOutcomeRow row) => row.HatredSubRoll = HatredTargetTable.RollDice().ToString();

    // Sous-jet 1D6 de branche (Blessure au bras/Jambe écrasée, 23/25) déterminant laquelle des deux
    // s'applique (voir Core.Rules.SeriousInjuryEffectTable.RequiresBranchSubRoll) - même convention que
    // AutoRollHatred, le champ InjuryBranchSubRoll reste modifiable ensuite pour un jet physique.
    [RelayCommand]
    private void AutoRollInjuryBranch(WarriorOutcomeRow row) => row.InjuryBranchSubRoll = SeriousInjuryEffectTable.RollSubDie().ToString();

    // Sous-jet 1D3 de Blessure profonde (35) déterminant le nombre de parties manquées (voir
    // Core.Rules.SeriousInjuryEffectTable.RollD3) - jusqu'ici tiré silencieusement par l'appli sans que
    // le joueur ne le voie, retour utilisateur 2026-08-26. Même convention que les autres jets de cette
    // étape, modifiable ensuite pour un jet physique.
    [RelayCommand]
    private void AutoRollDeepWound(WarriorOutcomeRow row) => row.DeepWoundSubRoll = SeriousInjuryEffectTable.RollD3().ToString();

    // Même sous-jet, pour un sous-jet "Blessures multiples" qui tombe lui-même sur 35.
    [RelayCommand]
    private void AutoRollSubDeepWound(InjurySubRollEntry entry) => entry.DeepWoundSubRoll = SeriousInjuryEffectTable.RollD3().ToString();

    // Portée "toutes les bandes de ce type" (6) uniquement - la seule portée référençant un vrai
    // WarbandArchetype du catalogue (les 3 autres sont résolues à la frappe dans un simple champ texte,
    // voir WarriorOutcomeRow.OnHatredTargetFreeTextInputChanged - l'appli ne suit pas les bandes/
    // guerriers adverses comme données structurées, retour utilisateur explicite). Nécessite un dialog
    // (ActionSheet), impossible à déclencher depuis un simple setter de propriété - même patron que
    // PickAdvanceSkill/PickAdvanceSpell (EndOfGameDialogViewModel.Advance.cs).
    [RelayCommand]
    private async Task PickHatredWarbandArchetype(WarriorOutcomeRow row)
    {
        var archetypes = await _libraryService.GetWarbandArchetypesAsync(Loc.Language);
        var index = await ShowActionSheetIndexAsync(Loc["EndOfGameHatredPickWarbandArchetype"], archetypes.Select(a => a.Name).ToArray());
        if (index < 0) return;

        row.SetHatredTarget(archetypes[index]);
    }

    // Confirmation de la portée 6 en chip (ChipView), même langage tap-to-detail/croix-pour-retirer que
    // le reste de l'app plutôt qu'un label brut.
    [RelayCommand]
    private Task ShowHatredArchetypeDetail(WarbandArchetype archetype) => _detailDialogs.ShowWarbandArchetypeDetailDialogAsync(archetype);

    // ChipView ne transmet que l'item tapé (l'archétype lui-même, pas son WarriorOutcomeRow propriétaire)
    // - même patron que RemoveAdvanceSkill/RemoveAdvanceSpell (EndOfGameDialogViewModel.Advance.cs).
    [RelayCommand]
    private void RemoveHatredArchetype(WarbandArchetype archetype)
    {
        foreach (var row in WarriorRows.Where(r => r.HatredTargetWarbandArchetype == archetype))
            row.ClearHatredTargetWarbandArchetype();
    }

    // Rappel de règle pour une branche qui accorde une SpecialRule permanente (Folie 24 -> Stupidité/
    // Frénésie, voir WarriorOutcomeRow.InjuryBranchSpecialRules) - même popup que la puce Règles
    // spéciales de la fiche guerrier (WarbandDetailViewModel.ShowSpecialRuleDetail), affichée dès le
    // wizard plutôt que d'attendre l'enregistrement ("il faut mettre le chip plutôt que du texte",
    // retour utilisateur 2026-08-25).
    [RelayCommand]
    private Task ShowInjuryBranchSpecialRuleDetail(SpecialRule rule) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule);
}
