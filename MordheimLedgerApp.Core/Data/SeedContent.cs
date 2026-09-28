using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Identité du contenu officiel embarqué (Data/SeedData/*.json) : une empreinte de tous les JSON,
/// sans numéro de version à tenir à la main. La base stocke l'empreinte du contenu qu'elle a reçu
/// (ContentMetaEntity) ; au lancement, une empreinte différente de celle de l'appli déclenche la synchro
/// (AppDatabase.SyncOfficialContentAsync) - toute modification d'un JSON arrive donc d'elle-même sur les bases
/// installées.</summary>
public static class SeedContent
{
    public const string FingerprintKey = "ContentFingerprint";

    private const string ResourcePrefix = "MordheimLedgerApp.Core.Data.SeedData.";

    private static readonly Lazy<string> _fingerprint = new(ComputeFingerprint);

    /// <summary>SHA-256 de tous les JSON de seed, dans l'ordre des noms de fichier. Fins de ligne normalisées
    /// et BOM retiré : le même contenu donne la même empreinte quel que soit le PC (checkout git en CRLF ou
    /// LF). Calculée une fois par exécution.</summary>
    public static string Fingerprint => _fingerprint.Value;

    private static string ComputeFingerprint()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);
        var text = new StringBuilder();
        foreach (var name in names)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            text.Append(name[ResourcePrefix.Length..]).Append('\n')
                .Append(reader.ReadToEnd().Replace("\r\n", "\n")).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
