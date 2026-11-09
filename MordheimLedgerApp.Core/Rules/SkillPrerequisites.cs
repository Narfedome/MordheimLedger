using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Prérequis d'une compétence qu'on envisage de choisir (EffectKind.Prerequisite) que le guerrier
/// ne remplit pas - ex. Sorcellerie pour un non-jeteur de sorts. Purement informatif : le sélecteur affiche
/// un message ambre, la compétence reste choisissable (décision de l'utilisateur, 2026-11-04).</summary>
public static class SkillPrerequisites
{
    public static IReadOnlyList<SkillEffect> Unmet(Skill candidate, EffectSubject subject) =>
        candidate.Effects
            .Where(e => e.Kind == EffectKind.Prerequisite && !RuleEffectResolver.IsConditionMet(e, subject))
            .ToList();
}
