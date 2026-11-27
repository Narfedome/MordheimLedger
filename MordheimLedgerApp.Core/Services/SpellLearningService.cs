using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Écrit un sort tiré au dé sur la fiche d'un guerrier : nouveau sort, ou doublon qui réduit de 1 la difficulté
/// du sort déjà connu (Core.Rules.SpellRules). Sert à la progression, au successeur qui apprend la magie et au
/// Grimoire. Le tirage lui-même est fait par l'appli.</summary>
public sealed class SpellLearningService
{
    private readonly IWarbandService _warbands;

    public SpellLearningService(IWarbandService warbands) => _warbands = warbands;

    /// <summary>target doit exister en base. target.Spells est tenu à jour : deux tirages du même sort dans la même Fin
    /// de partie donnent bien un sort puis une réduction.</summary>
    public async Task<SpellLearningOutcome> LearnAsync(Warrior target, Spell spell)
    {
        if (SpellRules.Learn(target.Spells.Select(s => s.Item.Id), spell.Id) == SpellLearningOutcome.DifficultyReduced)
        {
            var known = target.Spells.First(s => s.Item.Id == spell.Id);
            known.DifficultyReduction++;
            await _warbands.SaveWarriorSpellAsync(known);
            return SpellLearningOutcome.DifficultyReduced;
        }

        var learned = await _warbands.AddWarriorSpellAsync(target.Id, spell);
        target.Spells.Add(learned);
        return SpellLearningOutcome.NewSpell;
    }

    /// <summary>Grimoire + Savoir Arcanique : le guerrier apprend l'école (la Magie Mineure) et son premier sort.</summary>
    public async Task<SpellLearningOutcome> LearnSchoolAsync(Warrior target, MagicSchool school, Spell firstSpell)
    {
        target.ExtraMagicSchoolId = school.Id;
        await _warbands.SaveWarriorAsync(target);
        return await LearnAsync(target, firstSpell);
    }
}
