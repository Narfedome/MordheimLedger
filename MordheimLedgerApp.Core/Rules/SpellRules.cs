namespace MordheimLedgerApp.Core.Rules;

/// <summary>Résultat de l'apprentissage d'un sort tiré au dé.</summary>
public enum SpellLearningOutcome
{
    /// <summary>Sort nouveau : il rejoint la liste du guerrier.</summary>
    NewSpell,
    /// <summary>Sort déjà connu : sa difficulté baisse de 1 (livre, section Magie), au lieu d'une relance.</summary>
    DifficultyReduced
}

/// <summary>Livre des règles : "A Wizard starts with one spell, determined randomly - roll 1D6 on the
/// appropriate list." Même tirage pour un nouveau sort pris à la place d'une compétence à la progression, pour le
/// successeur qui apprend la magie et pour le Grimoire. Un sort tiré alors qu'il est déjà connu n'est pas relancé :
/// sa difficulté baisse de 1 (cumulable).</summary>
public static class SpellRules
{
    public static int RollDice() => Random.Shared.Next(1, 7);

    /// <summary>Nouveau sort, ou doublon qui réduit la difficulté du sort déjà connu.</summary>
    public static SpellLearningOutcome Learn(IEnumerable<int> knownSpellIds, int rolledSpellId) =>
        knownSpellIds.Contains(rolledSpellId) ? SpellLearningOutcome.DifficultyReduced : SpellLearningOutcome.NewSpell;

    /// <summary>Difficulté affichée d'un sort connu, après réduction(s) - jamais sous 2 (le plus petit jet de 2D6).
    /// Null pour un sort sans difficulté (prière sans jet).</summary>
    public static int? EffectiveDifficulty(int? baseDifficulty, int reduction) =>
        baseDifficulty is { } difficulty ? Math.Max(2, difficulty - Math.Max(0, reduction)) : null;
}
