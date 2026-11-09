using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame.Effects;

/// <summary>Textes communs aux effets affichés dans le wizard : d'où vient l'effet, et pourquoi sa condition
/// n'est pas remplie (Core a déjà tranché ActiveEffect.ConditionMet, on ne fait que le dire).</summary>
public static class EffectTexts
{
    public static string SourceOf(ActiveEffect effect, LocalizationService loc) => string.Format(
        loc[effect.SourceKind == EffectSourceKind.Skill ? "EffectSourceSkillOf" : "EffectSourceItemOf"], effect.Warrior.Name);

    public static string? UnmetCondition(ActiveEffect effect, LocalizationService loc) => effect.ConditionMet
        ? null
        : string.Format(loc[$"EffectUnmet{effect.Effect.Condition}"], effect.Warrior.Name);

    /// <summary>Rappel : la note de l'effet, sinon la description de la compétence qui le porte.</summary>
    public static string ReminderText(ActiveEffect effect) =>
        effect.Effect.Note is { Length: > 0 } note
            ? note
            : effect.Warrior.Skills.FirstOrDefault(s => s.Item.Id == effect.SourceId)?.Item.Description ?? string.Empty;

    public static bool IsReminder(ActiveEffect effect) => EffectHandling.Classify(effect.Effect) == EffectHandlingKind.Reminder;
}

/// <summary>Un rappel affiché tel quel (maquette « Jet d'Exploration » : « Rappel · Hans »).</summary>
public sealed record EffectReminder(string Title, string Text)
{
    public static EffectReminder From(ActiveEffect effect, LocalizationService loc) =>
        new(string.Format(loc["EffectReminderTitle"], effect.SourceName, effect.Warrior.Name), EffectTexts.ReminderText(effect));
}
