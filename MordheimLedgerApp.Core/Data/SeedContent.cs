using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Version du contenu officiel embarqué (Data/SeedData/*.json). ContentVersion.json porte un numéro
/// incrémenté à la main à chaque modification des JSON ; la base le stocke (ContentMetaEntity) pour savoir
/// si son contenu officiel est à jour. L'empreinte (Fingerprint) sert de garde-fou au générateur de
/// seed.db3 (Tools/DbSeedGenerator) : des JSON modifiés sans incrément du numéro font échouer le build.</summary>
public static class SeedContent
{
    public const string VersionFileName = "ContentVersion.json";
    public const string VersionKey = "ContentVersion";
    public const string FingerprintKey = "ContentFingerprint";

    private const string ResourcePrefix = "MordheimLedgerApp.Core.Data.SeedData.";

    /// <summary>Numéro de ContentVersion.json.</summary>
    public static int Version
    {
        get
        {
            using var stream = Open(ResourcePrefix + VersionFileName);
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.GetProperty("contentVersion").GetInt32();
        }
    }

    /// <summary>SHA-256 de tous les JSON de seed sauf ContentVersion.json, dans l'ordre des noms de fichier.
    /// Fins de ligne normalisées et BOM retiré : le même contenu donne la même empreinte quel que soit le
    /// PC (checkout git en CRLF ou LF).</summary>
    public static string Fingerprint
    {
        get
        {
            var names = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
                .Where(n => n != ResourcePrefix + VersionFileName)
                .OrderBy(n => n, StringComparer.Ordinal);
            var text = new StringBuilder();
            foreach (var name in names)
            {
                using var reader = new StreamReader(Open(name), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                text.Append(name[ResourcePrefix.Length..]).Append('\n')
                    .Append(reader.ReadToEnd().Replace("\r\n", "\n")).Append('\n');
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }

    private static Stream Open(string resourceName) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Missing embedded seed file: {resourceName}");
}
