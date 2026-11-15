using System.Text.RegularExpressions;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Features.Library.Skills.Effects;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Settings.ContentConflicts;

/// <summary>Textes d'une différence entre deux versions d'une entrée du Codex (ContentFieldDifference) : nom du type,
/// libellé du champ, valeur lisible. Partagé par « Versions à départager » (contenu officiel) et l'import d'un fichier
/// reçu (Features/Exchange).</summary>
public static partial class ContentFieldText
{
    private static LocalizationService Loc => LocalizationService.Instance;

    public static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    /// <summary>Valeur lisible d'un champ : les effets d'une compétence sont stockés en JSON (SkillEntity.EffectsJson)
    /// et s'affichent comme dans le Codex, un effet par ligne (« Achat · Réduction 2D6 CO (1 fois par après-bataille) »).</summary>
    public static string ValueText(string field, string value)
    {
        if (field != "EffectsJson" || string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            return string.Join(Environment.NewLine, SkillEffectJson.Deserialize(value)
                .Select(e => new SkillEffectRow(e, SkillEffectNames.Empty))
                .Select(r => r.HasDetails ? $"{r.Title} ({r.Details})" : r.Title));
        }
        catch (System.Text.Json.JsonException)
        {
            return value;
        }
    }

    /// <summary>Type d'une entrée (nom de table, ex. « EquipmentItemEntity »).</summary>
    public static string TypeLabel(string tableName) => LocOrNull("ContentType" + tableName) ?? Humanize(tableName.Replace("Entity", ""));

    /// <summary>Libellé lisible d'un champ (colonne ou table de jointure, voir ContentFieldDifference) - réutilise
    /// les libellés du Codex quand ils existent, sinon le nom de colonne découpé.</summary>
    public static string FieldLabel(string field)
    {
        if (field.EndsWith("SpecialRuleEntity")) return Loc["LibSpecialRulesPh"];
        if (field == "WarbandArchetypeMagicSchoolEntity") return Loc["LibMagicSchoolsPh"];
        if (field.StartsWith("WarbandArchetype") && field.EndsWith("Entity")) return Loc["LibRestrictedToWarbandsPh"];
        if (field.StartsWith("WarriorArchetype") && field.EndsWith("Entity")) return Loc["LibRestrictedToWarriorsPh"];
        if (field.EndsWith("EquipmentEntity") || field == "EquipmentListItemEntity") return Loc["WarriorsEquipmentHeading"];
        if (field.EndsWith("SkillEntity")) return Loc["WarriorsSkillsHeading"];
        return field switch
        {
            "NameKey" => Loc["ContentFieldName"],
            "DescriptionKey" => Loc["LibDescriptionPh"],
            "ImagePath" => Loc["ContentFieldImage"],
            "Cost" => Loc["EquipmentItemCostPh"],
            "Rarity" => Loc["EquipmentItemRarityPh"],
            "Category" => Loc["EquipmentItemCategoryPh"],
            "HireCost" => Loc["HiredSwordHireCostPh"],
            "RollValue" => Loc["LibRollAbbr"],
            "Difficulty" => Loc["LibDifficultyAbbr"],
            // Effets d'une compétence, stockés en JSON (SkillEntity.EffectsJson) - « Effects json » sinon.
            "EffectsJson" => Loc["EffectsHeading"],
            "Movement" or "WeaponSkill" or "BallisticSkill" or "Strength" or "Toughness" or "Wounds" or "Initiative" or "Attacks" or "Leadership"
                => Loc[$"Stat{field}Abbr"],
            "RaceId" or "RacialProfileId" or "WarbandArchetypeId" or "EquipmentListId" or "MagicSchoolId"
                => Loc[$"ContentType{field[..^2]}Entity"],
            _ => Humanize(field.EndsWith("Key") ? field[..^3] : field.EndsWith("Id") ? field[..^2] : field),
        };
    }

    private static string? LocOrNull(string key) => Loc[key] is var value && value != key ? value : null;

    /// <summary>"MaxWarriors" -> "Max warriors".</summary>
    private static string Humanize(string pascalCase)
    {
        var words = WordBoundary().Replace(pascalCase, " ").ToLowerInvariant();
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}
