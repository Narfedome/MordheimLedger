using System.Text.Json;
using System.Text.Json.Serialization;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Exchange;

/// <summary>Contenu échangé entre deux appareils (v2, lot 0) : des bandes jouées et/ou des entrées du Codex,
/// avec tout le contenu du catalogue qu'elles référencent. Indépendant des Id auto-incrémentés d'une base : une
/// ligne du catalogue y est désignée par sa clé (<see cref="CatalogEntry.Key"/>), jamais par son Id local.
/// Sérialisé en JSON (voir <see cref="ExchangeArchive"/>) ; les photos voyagent à côté, dans l'archive .zip.</summary>
public sealed class ExchangePackage
{
    public const string FormatName = "mordheim-ledger";

    /// <summary>Version du format. Un paquet d'une version plus récente est refusé (l'appli qui le lit ne sait
    /// pas ce qu'il contient de plus) ; une version plus ancienne reste lisible.</summary>
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Empreinte du contenu officiel de l'appareil qui a exporté (SeedContent.Fingerprint) - indicatif.</summary>
    public string? ContentFingerprint { get; set; }

    /// <summary>Lignes du catalogue : celles choisies pour un partage du Codex (<see cref="SharedEntries"/>) et
    /// tout ce qu'elles ou les bandes référencent, de proche en proche.</summary>
    public List<CatalogEntry> Catalog { get; set; } = [];

    /// <summary>Clés (« Table|Key ») des entrées que le joueur a choisi de partager, par opposition à celles
    /// embarquées parce qu'elles sont référencées. Vide pour un partage de bande.</summary>
    public List<string> SharedEntries { get; set; } = [];

    /// <summary>Bandes jouées (WarbandEntity), avec leurs guerriers, réserve, Historique...</summary>
    public List<PlayedRow> Warbands { get; set; } = [];

    /// <summary>Photos des guerriers, par chemin dans l'archive (« photos/xxx.jpg », valeur de PhotoPath dans le
    /// paquet). Hors JSON : écrites à côté dans l'archive .zip.</summary>
    [JsonIgnore]
    public Dictionary<string, byte[]> Photos { get; } = new(StringComparer.Ordinal);

    internal static string EntryId(string table, string key) => table + "|" + key;
}

/// <summary>Une ligne du catalogue. Les colonnes de référence, de traduction et les jointures dont elle est
/// propriétaire (OfficialContentSchema) sont exprimées en clés et en textes, le reste tel quel.</summary>
public sealed class CatalogEntry
{
    /// <summary>Préfixe de clé d'une ligne du contenu officiel (Officielle ou Modifiée) : « o:equipment.axe ».</summary>
    public const string OfficialPrefix = "o:";

    /// <summary>Préfixe de clé d'une ligne Personnalisée : « s:&lt;ShareId&gt; ».</summary>
    public const string SharedPrefix = "s:";

    /// <summary>Nom de la table (type d'entité, ex. « EquipmentItemEntity »).</summary>
    public string Table { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
    public ContentSource Source { get; set; }

    public SortedDictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Colonne de traduction -> texte par langue.</summary>
    public SortedDictionary<string, SortedDictionary<string, string>> Translations { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Colonne de référence -> clé de la ligne visée (absente si aucune).</summary>
    public SortedDictionary<string, string> References { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Colonne « liste d'id » -> clés des lignes visées.</summary>
    public SortedDictionary<string, List<string>> ListReferences { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Table de jointure possédée -> clés des lignes jointes.</summary>
    public SortedDictionary<string, List<string>> Joins { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Une ligne de partie jouée (bande, guerrier, ligne d'équipement...) et ses lignes enfants (voir
/// PlayedDataSchema). Ni Id ni colonne parente : recréées à l'import.</summary>
public sealed class PlayedRow
{
    /// <summary>Identifiant d'échange de la bande (ShareIdentityEntity) - renseigné pour une bande seulement.</summary>
    public string? ShareId { get; set; }

    public SortedDictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Colonne de référence au catalogue -> clé de la ligne visée (absente si aucune).</summary>
    public SortedDictionary<string, string> References { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Table enfant -> lignes, dans l'ordre de création.</summary>
    public SortedDictionary<string, List<PlayedRow>> Children { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Paquet illisible : pas un fichier de Mordheim Ledger, version plus récente que l'appli, archive
/// endommagée, ou référence vers un contenu introuvable.</summary>
public sealed class ExchangeFormatException(string message, Exception? inner = null) : Exception(message, inner);
