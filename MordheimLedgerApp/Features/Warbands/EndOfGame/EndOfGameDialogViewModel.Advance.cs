using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

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
        // AdvanceRollEntry.MissingRequirementMessage pointe précisément ce qui manque (jet principal,
        // sous-jet, choix de caractéristique, compétence/sort, ou tel champ de la Promotion) plutôt
        // qu'un message générique "Un jet est requis" même quand le jet était déjà fait - voir sa doc.
        var valid = true;
        foreach (var advance in rolls)
            valid &= CheckRoll(advance.MissingRequirementMessage is not null, () => advance.RollError = advance.MissingRequirementMessage);
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

    // Sous-jet 1D6 des résultats Héros 6/8/9 (voir CharacteristicChoiceMode.SubRoll1D6) - seule la
    // table Héros en comporte, HeroAdvanceTable.RollSubDie() convient donc dans tous les cas où ce
    // bouton est visible (AdvanceRollEntry.NeedsSubRoll).
    [RelayCommand]
    private void AutoRollSubRoll(AdvanceRollEntry entry) => entry.ManualSubRoll = HeroAdvanceTable.RollSubDie().ToString();

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
        if (skills.Count == 0) return;

        entry.SelectedSpell = null;
        foreach (var skill in skills)
            entry.SelectedSkills.Add(skill);
    }

    // Alternative au choix de compétence ci-dessus, pour un Héros sorcier uniquement (voir
    // AdvanceRollEntry.ShowSpellOption) : tirage 1D6 via SpellRollDialog sur les écoles de la bande,
    // même mécanisme que WarriorEditDialogViewModel.AddSpell (branche non-_skipCosts) - un sorcier
    // obtient toujours un sort au hasard, jamais un choix libre.
    [RelayCommand]
    private async Task PickAdvanceSpell(AdvanceRollEntry entry)
    {
        var row = WarriorRows.First(r => r.AdvanceRolls.Contains(entry) || r.ExplorationAdvanceRolls.Contains(entry));
        var knownSpellIds = row.Warrior.Spells.Select(s => s.Item.Id).ToList();
        var rolled = await ShowDialogAsync(new SpellRollDialog(new SpellRollDialogViewModel(row.MagicSchools.ToList(), _libraryService, _detailDialogs, knownSpellIds)));
        if (rolled is null) return;

        entry.SelectedSkills.Clear();
        entry.SelectedSpell = rolled;
    }

    [RelayCommand]
    private Task ShowSkillDetail(Skill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill);

    [RelayCommand]
    private Task ShowSpellDetail(Spell spell) => _detailDialogs.ShowSpellDetailDialogAsync(spell);

    // Croix du ChipView (voir XAML) : ChipView ne transmet que l'item tapé (ici la Skill elle-même, pas
    // son AdvanceRollEntry propriétaire) - on retire donc cette Skill de TOUTES les collections
    // SelectedSkills parcourues (y compris les jets imbriqués d'une Promotion), sans risque : chaque
    // Skill choisie via PickAdvanceSkill est une instance propre au picker, jamais partagée entre deux
    // entrées, donc Remove(skill) (égalité par référence, Skill n'a pas d'Equals custom) ne peut agir
    // que sur la collection qui la détient réellement.
    [RelayCommand]
    private void RemoveAdvanceSkill(Skill skill)
    {
        foreach (var entry in AllAdvanceEntries())
            entry.SelectedSkills.Remove(skill);
    }

    // Même idiome pour le sort choisi (SelectedSpell, une seule valeur au lieu d'une collection) - la
    // demande initiale ne portait que sur les compétences, mais laisser le sort non-retirable aurait été
    // une incohérence, pas un choix délibéré.
    [RelayCommand]
    private void RemoveAdvanceSpell(Spell spell)
    {
        foreach (var entry in AllAdvanceEntries().Where(e => e.SelectedSpell == spell))
            entry.SelectedSpell = null;
    }

    /// <summary>Tous les AdvanceRollEntry de ce wizard, y compris les jets imbriqués d'une Promotion
    /// (NestedHeroRoll/NestedHenchmanRoll) - seul point de parcours pour RemoveAdvanceSkill/
    /// RemoveAdvanceSpell, qui n'ont que l'item tapé (pas l'entrée propriétaire) pour retrouver où
    /// agir.</summary>
    private IEnumerable<AdvanceRollEntry> AllAdvanceEntries()
    {
        foreach (var row in WarriorRows)
        {
            foreach (var entry in row.AdvanceRolls.Concat(row.ExplorationAdvanceRolls))
            {
                yield return entry;
                if (entry.NestedHeroRoll is { } heroRoll) yield return heroRoll;
                if (entry.NestedHenchmanRoll is { } henchmanRoll) yield return henchmanRoll;
            }
        }
    }
}
