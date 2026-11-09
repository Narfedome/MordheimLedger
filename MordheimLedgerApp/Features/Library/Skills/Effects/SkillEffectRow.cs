using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Skills.Effects;

/// <summary>Une ligne d'effet dans le Codex (édition et lecture seule) : titre « Moment · type », ligne de
/// détail (condition, fréquence, prix plancher, rappel) et puce de prise en charge. Pur affichage : la
/// prise en charge vient de Core.Rules.EffectHandling, le texte de la localisation.</summary>
public sealed class SkillEffectRow
{
    public SkillEffect Effect { get; }
    public string Title { get; }
    public string Details { get; }
    public bool HasDetails => Details.Length > 0;
    public EffectHandlingKind Handling { get; }
    public string HandlingLabel { get; }
    public bool IsApplied => Handling == EffectHandlingKind.Applied;
    public bool IsInfo => Handling == EffectHandlingKind.Info;
    public bool IsReminder => Handling == EffectHandlingKind.Reminder;

    public SkillEffectRow(SkillEffect effect, SkillEffectNames names)
    {
        Effect = effect;
        Handling = EffectHandling.Classify(effect);
        HandlingLabel = Loc[$"EffectHandling{Handling}"];
        Title = $"{Loc[$"EffectMoment{effect.Moment}"]} · {Summary(effect, names)}";
        var details = string.Join(" · ", DetailParts(effect, names));
        Details = details.Length == 0 ? details : char.ToUpper(details[0], System.Globalization.CultureInfo.CurrentCulture) + details[1..];
    }

    private static LocalizationService Loc => LocalizationService.Instance;

    private static string Summary(SkillEffect effect, SkillEffectNames names)
    {
        var count = EffectValues.ParseCount(effect.Value);
        return effect.Kind switch
        {
            EffectKind.Modifier => string.Format(Loc["EffectSummaryModifier"], EffectValues.FormatModifier(EffectValues.ParseAmount(effect.Value))),
            EffectKind.Reroll => count > 1 ? string.Format(Loc["EffectSummaryRerollMany"], count) : Loc["EffectSummaryReroll"],
            EffectKind.AdjustDie => string.Format(Loc["EffectSummaryAdjustDie"], count),
            EffectKind.ExtraDie => string.Format(Loc["EffectSummaryExtraDie"], count),
            EffectKind.Discount => string.Format(Loc["EffectSummaryDiscount"], effect.Value?.Trim() ?? "?"),
            EffectKind.Allow => effect.Targets.Count == 0
                ? Loc["EffectKindAllow"]
                : string.Format(Loc["EffectSummaryAllow"], string.Join(", ", effect.Targets.Select(t => names.NameOf(t, effect.Moment)))),
            EffectKind.Prerequisite => Loc["EffectSummaryPrerequisite"],
            _ => Loc["EffectSummaryReminder"],
        };
    }

    private static IEnumerable<string> DetailParts(SkillEffect effect, SkillEffectNames names)
    {
        if (effect.Condition != EffectCondition.None)
        {
            var targets = string.Join(", ", effect.ConditionTargets.Select(t => names.NameOf(t)));
            yield return effect.Condition switch
            {
                EffectCondition.CarriesItem when targets.Length > 0 => string.Format(Loc["EffectConditionCarriesItemFormat"], targets),
                EffectCondition.NotInWarbandOrArchetype when targets.Length > 0 => string.Format(Loc["EffectConditionExceptFormat"], targets),
                _ => Loc[$"EffectCondition{effect.Condition}"],
            };
        }
        if (effect.Frequency == EffectFrequency.OncePerPostBattle)
            yield return Loc["EffectFrequencyOnceDetail"];
        if (effect.MinimumPrice is { } floor)
            yield return string.Format(Loc["EffectMinimumPriceDetail"], floor);
        if (!string.IsNullOrWhiteSpace(effect.Note))
            yield return $"« {effect.Note.Trim()} »";
    }
}

/// <summary>Une cible affichée en puce dans le popup d'édition (ChipListView lie « Name »).</summary>
public sealed record EffectTargetChip(string Id, string Name);
