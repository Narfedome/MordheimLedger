using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Services;

/// <summary>Textes d'un objet à usage unique, partagés par la fiche récap du Codex, l'écran des conflits de synchro et la
/// Fin de partie : « I +1D3, M +1, F +1 », « 2-3 : dépendant », « Perdu sur 1-3 si le chef est mis hors de combat ».</summary>
public static class SingleUseText
{
    private static LocalizationService Loc => LocalizationService.Instance;

    public static string Stat(CharacteristicField field) => Loc[$"Stat{field}Abbr"];

    /// <summary>« I +1D3 », « M +1 », « E −1 +1D6 ».</summary>
    public static string Modifier(GameStatModifier m)
    {
        var bits = new List<string>();
        if (m.Value != 0 || m.DiceSides is not > 0) bits.Add($"{m.Value:+0;-0;+0}");
        if (m.DiceSides is > 0) bits.Add($"+1D{m.DiceSides}");
        return $"{Stat(m.Field)} {string.Join(" ", bits)}";
    }

    public static string Bonuses(SingleUseProfile profile) => string.Join(", ", profile.StatModifiers.Select(Modifier));

    /// <summary>Effet d'une ligne de la table : « dépendant », « I +1 définitivement », « gagne Stupidité ».</summary>
    public static string Outcome(AfterBattleOutcome outcome, Func<AfterBattleOutcome, string?> ruleName) => outcome.Kind switch
    {
        AfterBattleOutcomeKind.Addiction => Loc["SingleUseOutcomeShortAddiction"],
        AfterBattleOutcomeKind.CharacteristicChange when outcome.Field is { } field && outcome.Delta is { } delta =>
            string.Format(Loc["SingleUseOutcomeShortCharacteristic"], Stat(field), $"{delta:+0;-0;+0}"),
        AfterBattleOutcomeKind.GainSpecialRule => string.Format(Loc["SingleUseOutcomeShortRule"], ruleName(outcome) ?? "?"),
        _ => Loc["SingleUseOutcomeNone"],
    };

    public static string Range(AfterBattleOutcome o) => o.Min == o.Max ? o.Min.ToString() : $"{o.Min}-{o.Max}";

    /// <summary>« Après la bataille (2D6) » puis une ligne par résultat, vide sans table.</summary>
    public static string? TableHeading(SingleUseProfile profile) =>
        SingleUseRules.AfterBattleDice(profile) is { } dice ? string.Format(Loc["SingleUseAfterBattleDiceFormat"], dice.Count, dice.Sides) : null;

    public static List<string> TableLines(SingleUseProfile profile, Func<AfterBattleOutcome, string?> ruleName) =>
        SingleUseRules.AfterBattleDice(profile) is null
            ? new List<string>()
            : profile.AfterBattle.Select(o => $"{Range(o)} : {Outcome(o, ruleName)}").ToList();

    public static string Target(SingleUseTarget target) => Loc[$"SingleUseTarget{target}"];

    /// <summary>« Perdu sur 1-3 si le chef est mis hors de combat », null si l'objet ne se perd pas ainsi.</summary>
    public static string? Loss(EquipmentItem item) => item.LostAfterOutOfActionOn is > 0 and var upTo
        ? string.Format(Loc[item.LostAfterOutOfActionLeaderOnly ? "SingleUseLossLeaderFormat" : "SingleUseLossFormat"], upTo)
        : null;

    /// <summary>Résumé d'une ligne (écran des conflits) : « Pris avant la bataille · Guerrier · I +1D3, M +1 · 2D6 : 2-3 dépendant ».</summary>
    public static string Summary(SingleUseProfile profile, Func<AfterBattleOutcome, string?> ruleName)
    {
        var parts = new List<string> { Loc[$"SingleUse{profile.Timing}"], Target(profile.Target) };
        if (profile.StatModifiers.Count > 0) parts.Add(Bonuses(profile));
        if (SingleUseRules.AfterBattleDice(profile) is { } dice)
            parts.Add($"{dice.Count}D{dice.Sides} : " + string.Join(", ", profile.AfterBattle.Select(o => $"{Range(o)} {Outcome(o, ruleName)}")));
        return string.Join(" · ", parts);
    }
}
