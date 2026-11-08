using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Ce que l'app fait d'un effet, tel qu'affiché dans le Codex (puce « Appliqué » / « Info » /
/// « Rappel ») : la légende « Appliqué : l'app calcule ou propose. Info et Rappel : l'app affiche, le joueur
/// décide » se lit d'ici, pas d'un ViewModel.</summary>
public enum EffectHandlingKind
{
    /// <summary>L'app calcule ou propose quelque chose au moment concerné (bonus, relance, réduction...).</summary>
    Applied,
    /// <summary>Prérequis : signalé en ambre au choix de la compétence, jamais bloquant.</summary>
    Info,
    /// <summary>Simple rappel affiché, rien de calculé (EffectKind.Reminder, ou tout effet « en partie »).</summary>
    Reminder,
}

/// <summary>Lecture d'un SkillEffect pour son affichage et son édition : quelle prise en charge, et quels
/// champs ont un sens pour quel type d'effet (le popup d'édition n'affiche que ceux-là).</summary>
public static class EffectHandling
{
    public static EffectHandlingKind Classify(SkillEffect effect) => effect switch
    {
        { Kind: EffectKind.Prerequisite } => EffectHandlingKind.Info,
        { Kind: EffectKind.Reminder } or { Moment: EffectMoment.InGame } => EffectHandlingKind.Reminder,
        _ => EffectHandlingKind.Applied,
    };

    /// <summary>Value n'a de sens que pour un bonus, une relance, un ajustement, un dé de plus ou une
    /// réduction (voir SkillEffect.Value).</summary>
    public static bool UsesValue(EffectKind kind) => kind is EffectKind.Modifier or EffectKind.Reroll
        or EffectKind.AdjustDie or EffectKind.ExtraDie or EffectKind.Discount;

    /// <summary>Valeur saisie au compteur −/+ plutôt qu'en texte libre : un nombre de dés ou un bonus. La
    /// réduction garde une saisie libre (formule de dés, « 2D6 »).</summary>
    public static bool UsesNumericValue(EffectKind kind) => kind is EffectKind.Modifier or EffectKind.Reroll
        or EffectKind.AdjustDie or EffectKind.ExtraDie;

    /// <summary>Bornes du compteur : un bonus va de -6 à +6, un nombre de dés de 1 à 6 (au-delà, aucune règle
    /// du livre n'en donne).</summary>
    public static (int Min, int Max) NumericRange(EffectKind kind) => kind == EffectKind.Modifier ? (-6, 6) : (1, 6);

    /// <summary>Valeur après un appui sur − (delta -1) ou + (delta +1), bornée par NumericRange ; delta 0
    /// remet simplement une valeur lisible dans les bornes (changement de type, valeur vide ou « 2D6 » restée
    /// d'une réduction). Un bonus est écrit signé (« +2 »), un nombre de dés tel quel (« 1 »).</summary>
    public static string StepNumericValue(EffectKind kind, string? value, int delta)
    {
        var (min, max) = NumericRange(kind);
        var current = kind == EffectKind.Modifier ? EffectValues.ParseAmount(value) : EffectValues.ParseCount(value);
        if (kind == EffectKind.Modifier && string.IsNullOrWhiteSpace(value) && delta == 0) current = 1;
        var next = Math.Clamp(current + delta, min, max);
        return kind == EffectKind.Modifier ? EffectValues.FormatModifier(next) : next.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Prix plancher : seulement pour une réduction (Marchandage : 1 CO).</summary>
    public static bool UsesMinimumPrice(EffectKind kind) => kind == EffectKind.Discount;

    /// <summary>Cibles : seulement pour « ouvre un sélecteur » (catégories ou OfficialId ouverts).</summary>
    public static bool UsesTargets(EffectKind kind) => kind == EffectKind.Allow;

    /// <summary>Cibles de condition : seulement pour les conditions qui en portent une liste.</summary>
    public static bool UsesConditionTargets(EffectCondition condition) =>
        condition is EffectCondition.CarriesItem or EffectCondition.NotInWarbandOrArchetype;

    /// <summary>Retire ce qui n'a plus de sens après un changement de type ou de condition dans le popup
    /// d'édition (une Valeur restée d'un ancien type « Bonus » sur un « Prérequis », par ex.) - pour que le
    /// JSON enregistré ne garde que des champs lus par les règles.</summary>
    public static void ClearUnusedFields(SkillEffect effect)
    {
        if (!UsesValue(effect.Kind)) effect.Value = null;
        if (!UsesMinimumPrice(effect.Kind)) effect.MinimumPrice = null;
        if (!UsesTargets(effect.Kind)) effect.Targets.Clear();
        if (!UsesConditionTargets(effect.Condition)) effect.ConditionTargets.Clear();
        if (string.IsNullOrWhiteSpace(effect.Note)) effect.Note = null;
        if (string.IsNullOrWhiteSpace(effect.Value)) effect.Value = null;
    }
}
