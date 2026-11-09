using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Point d'entrée des effets de règle : pour un moment donné (EffectMoment), quels effets les
/// guerriers ont-ils à proposer, et leur condition est-elle remplie ? L'app propose, le joueur décide : rien
/// ici n'applique quoi que ce soit, chaque étape du wizard / sélecteur consomme la liste à sa façon.
///
/// Deux sources, présentées de la même manière :
/// - les compétences du guerrier (Skill.Effects) ;
/// - les objets qu'il porte, via leurs colonnes Grants* existantes (EquipmentEffects) - lues telles quelles,
///   non migrées.
///
/// Les prérequis (EffectKind.Prerequisite) ne sont jamais renvoyés : ils portent sur une compétence qu'on
/// envisage de choisir, pas sur celles déjà acquises - voir SkillPrerequisites.</summary>
public static class RuleEffectResolver
{
    public static IReadOnlyList<ActiveEffect> For(EffectMoment moment, IEnumerable<EffectSubject> subjects)
    {
        var result = new List<ActiveEffect>();
        foreach (var subject in subjects)
        {
            foreach (var warriorSkill in subject.Warrior.Skills)
            {
                var skill = warriorSkill.Item;
                for (var i = 0; i < skill.Effects.Count; i++)
                {
                    var effect = skill.Effects[i];
                    if (effect.Moment != moment || effect.Kind == EffectKind.Prerequisite) continue;
                    result.Add(new ActiveEffect(subject.Warrior, EffectSourceKind.Skill, skill.Name, skill.OfficialId,
                        skill.Id, i, effect, IsConditionMet(effect, subject)));
                }
            }

            foreach (var carried in subject.Warrior.Equipment)
            {
                var item = carried.Item;
                var effects = EquipmentEffects(item);
                for (var i = 0; i < effects.Count; i++)
                {
                    if (effects[i].Moment != moment) continue;
                    result.Add(new ActiveEffect(subject.Warrior, EffectSourceKind.Equipment, item.Name, item.OfficialId,
                        item.Id, i, effects[i], IsConditionMet(effects[i], subject)));
                }
            }
        }
        return result;
    }

    public static IReadOnlyList<ActiveEffect> For(EffectMoment moment, EffectSubject subject) => For(moment, [subject]);

    /// <summary>Adaptateur : les capacités d'un objet porté (colonnes Grants* d'EquipmentItem, déjà
    /// appliquées par RareItemSearchBonus/ExplorationDiceBonus/SkillEligibility/CharacteristicIncreaseRules)
    /// exprimées comme des effets, pour que le wizard les affiche comme une compétence.</summary>
    public static IReadOnlyList<SkillEffect> EquipmentEffects(EquipmentItem item)
    {
        var effects = new List<SkillEffect>();
        if (item.GrantsRareItemSearchBonus is { } searchBonus and not 0)
            effects.Add(new SkillEffect { Moment = EffectMoment.RareItemSearch, Kind = EffectKind.Modifier, Value = EffectValues.FormatModifier(searchBonus) });
        // Le porteur doit être resté debout : c'était déjà la règle de l'ancien ExplorationDiceBonus (objets des
        // guerriers non hors de combat), gardée telle quelle sous forme de condition.
        if (item.GrantsBonusExplorationDice is { } extraDice and not 0)
            effects.Add(new SkillEffect { Moment = EffectMoment.ExplorationRoll, Kind = EffectKind.ExtraDie, Value = extraDice.ToString(), Condition = EffectCondition.NotOutOfAction });
        if (item.GrantsSkillCategory is { } category)
            effects.Add(new SkillEffect { Moment = EffectMoment.SkillChoice, Kind = EffectKind.Allow, Targets = [category.ToString()] });
        if (item.GrantsSpecificSkillOfficialId is { } skillOfficialId)
            effects.Add(new SkillEffect { Moment = EffectMoment.SkillChoice, Kind = EffectKind.Allow, Targets = [skillOfficialId] });
        if (item.RaisesCharacteristicMax is { } field)
            effects.Add(new SkillEffect { Moment = EffectMoment.CharacteristicIncrease, Kind = EffectKind.Modifier, Value = "+1", Targets = [field.ToString()] });
        return effects;
    }

    public static bool IsConditionMet(SkillEffect effect, EffectSubject subject) => effect.Condition switch
    {
        EffectCondition.None => true,
        EffectCondition.NotOutOfAction => !subject.IsOutOfAction,
        EffectCondition.NotDead => !subject.IsDead,
        EffectCondition.Leader => subject.Warrior.IsLeader,
        EffectCondition.Spellcaster => subject.IsSpellcaster,
        EffectCondition.CarriesItem => subject.Warrior.Equipment.Any(e =>
            e.Item.OfficialId is { } id && effect.ConditionTargets.Contains(id)),
        EffectCondition.NotInWarbandOrArchetype =>
            (subject.WarbandOfficialId is null || !effect.ConditionTargets.Contains(subject.WarbandOfficialId))
            && (subject.WarriorArchetypeOfficialId is null || !effect.ConditionTargets.Contains(subject.WarriorArchetypeOfficialId)),
        _ => true,
    };
}
