using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape Progression (une carte par guerrier ayant franchi un palier d'XP, voir la doc de
/// classe du fichier principal) - validation + commandes de jet + choix de compétence. Extrait de
/// EndOfGameDialogViewModel.cs (2026-08-18, refactor de découpage, voir CLAUDE.md) : aucun changement
/// de comportement, pur déplacement de membres.</summary>
public partial class EndOfGameDialogViewModel
{
    /// <summary>Prend directement la collection à valider (AdvanceRolls ou ExplorationAdvanceRolls, voir
    /// EndOfGameDialogViewModel.CurrentAdvanceRolls) plutôt qu'un WarriorOutcomeRow - le même guerrier
    /// peut traverser cette étape deux fois (voir WizardStep.IsExplorationAdvance), chaque passage ne
    /// devant valider que SES propres jets.</summary>
    private bool ValidateAdvanceStep(IEnumerable<AdvanceRollEntry> rolls)
    {
        var valid = true;
        foreach (var advance in rolls)
            valid &= CheckRoll(string.IsNullOrWhiteSpace(advance.ResultText), () => advance.RollError = Loc["EndOfGameRollRequired"]);
        return valid;
    }

    // Un guerrier peut franchir plusieurs paliers d'un coup - chaque AdvanceRollEntry (une par palier,
    // voir WarriorOutcomeRow.SyncAdvanceRolls) est un jet 2D6 indépendant sur la table de progression,
    // même résolution automatique que AutoRoll mais purement descriptif : aucune stat n'est modifiée
    // automatiquement (les sous-jets 1D6 des résultats 6/8/9 et le choix CC/CT du 7 restent à résoudre
    // par le joueur, cf. HeroAdvanceTable/HenchmanAdvanceTable).
    [RelayCommand]
    private void AutoRollAdvance(AdvanceRollEntry entry)
    {
        var roll = entry.IsHero ? HeroAdvanceTable.RollDice() : HenchmanAdvanceTable.RollDice();
        entry.ManualRoll = roll.ToString();
    }

    // Résultat "Compétence" (voir HeroAdvanceTable.IsSkill) : le joueur choisit directement une
    // compétence existante de la Bibliothèque, comme le "+" Compétences de la carte guerrier -
    // rattachée au guerrier par WarbandDetailViewModel.EndOfGame à l'enregistrement du wizard, pas
    // tout de suite (même logique différée que les autres résultats de cette étape).
    // SkillEligibility.EffectiveAllowedCategories plutôt que Warrior.AllowedSkillCategories brut :
    // certains objets trouvés élargissent les listes accessibles tant qu'ils sont portés (ex. Carnet de
    // l'Alchimiste -> Érudition, voir EquipmentItem.GrantsSkillCategory).
    [RelayCommand]
    private async Task PickAdvanceSkill(AdvanceRollEntry entry)
    {
        var row = WarriorRows.First(r => r.AdvanceRolls.Contains(entry) || r.ExplorationAdvanceRolls.Contains(entry));
        // EffectiveExtraSkillNames -> ids via _skillIdsByEnglishName : mêmes objets (ex. le symbole de
        // l'Ordre des Libres Marchands) que la Charrette/le Carnet de l'Alchimiste - une compétence
        // précise débloquée hors catégorie plutôt qu'une liste entière, voir EquipmentItem.
        // GrantsSpecificSkillName.
        var extraSkillIds = SkillEligibility.EffectiveExtraSkillNames(row.Warrior)
            .Select(name => _skillIdsByEnglishName.GetValueOrDefault(name))
            .Where(id => id != 0)
            .ToList();
        var skills = await _skillPicker.PickSkillAsync(_warbandArchetypeId, row.Warrior.WarriorArchetypeId,
            SkillEligibility.EffectiveAllowedCategories(row.Warrior), extraSkillIds);
        foreach (var skill in skills)
            entry.SelectedSkills.Add(skill);
    }

    [RelayCommand]
    private Task ShowSkillDetail(Skill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill);
}
