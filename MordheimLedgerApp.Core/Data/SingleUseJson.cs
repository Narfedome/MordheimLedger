using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Sérialisation d'EquipmentItem.SingleUse dans EquipmentItemEntity.SingleUseJson - même contrat que
/// SkillEffectJson (sortie déterministe pour l'empreinte de synchro, lecture tolérante).</summary>
public static class SingleUseJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? Serialize(SingleUseProfile? profile) =>
        profile is null ? null : JsonSerializer.Serialize(profile, Options);

    /// <summary>Valeur illisible = objet ordinaire plutôt qu'un catalogue qui ne se charge plus.</summary>
    public static SingleUseProfile? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<SingleUseProfile>(json, Options); }
        catch (JsonException) { return null; }
    }
}
