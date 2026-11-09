using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Un guerrier vu par les effets de règle, avec ce que Warrior ne porte pas lui-même : son état
/// pendant la partie (hors de combat, tué) et les OfficialId de sa bande et de son type, résolus par l'appelant
/// (Core.Rules ne lit jamais la base).</summary>
public sealed record EffectSubject(
    Warrior Warrior,
    bool IsSpellcaster = false,
    bool IsOutOfAction = false,
    bool IsDead = false,
    string? WarbandOfficialId = null,
    string? WarriorArchetypeOfficialId = null);

/// <summary>D'où vient un effet actif : une compétence du guerrier, ou un objet qu'il porte (colonnes
/// Grants* d'EquipmentItem, présentées comme des effets - voir RuleEffectResolver.EquipmentEffects).</summary>
public enum EffectSourceKind { Skill, Equipment }

/// <summary>Un effet que l'app propose au moment demandé. ConditionMet = false : l'effet est quand même
/// proposé, avec un message ambre - jamais caché ni bloqué.</summary>
public sealed record ActiveEffect(
    Warrior Warrior,
    EffectSourceKind SourceKind,
    string SourceName,
    string? SourceOfficialId,
    int SourceId,
    int EffectIndex,
    SkillEffect Effect,
    bool ConditionMet)
{
    /// <summary>Identifie cet effet de ce guerrier de façon stable pendant tout le wizard - clé de
    /// OncePerPostBattleLedger.</summary>
    public string Key => $"{Warrior.Id}:{SourceKind}:{SourceId}:{EffectIndex}";
}
