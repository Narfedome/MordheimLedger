using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Sérialisation de Skill.Effects dans SkillEntity.EffectsJson. Sortie déterministe (même liste =
/// même texte) : la synchro du contenu officiel compare les lignes par empreinte, colonne comprise.</summary>
public static class SkillEffectJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Texte lisible tel quel ("+2", accents d'une note) : la colonne s'affiche dans l'écran des conflits
        // de synchro. Jamais injecté dans du HTML, l'échappement par défaut n'apporte rien ici.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>null pour une liste vide - une compétence sans effet garde une colonne vide.</summary>
    public static string? Serialize(IReadOnlyCollection<SkillEffect>? effects) =>
        effects is null || effects.Count == 0 ? null : JsonSerializer.Serialize(effects, Options);

    /// <summary>Tolérant : une valeur illisible (édition à la main, version future) donne une liste vide
    /// plutôt que d'empêcher le chargement du catalogue.</summary>
    public static List<SkillEffect> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<SkillEffect>();
        try
        {
            return JsonSerializer.Deserialize<List<SkillEffect>>(json, Options) ?? new List<SkillEffect>();
        }
        catch (JsonException)
        {
            return new List<SkillEffect>();
        }
    }
}
