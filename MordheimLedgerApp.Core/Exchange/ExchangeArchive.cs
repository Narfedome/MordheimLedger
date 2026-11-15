using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MordheimLedgerApp.Core.Exchange;

/// <summary>Mise en forme d'un ExchangePackage pour le transport :
/// - archive .zip (fichier partagé, lobby) : « package.json » + photos sous « photos/ » ;
/// - texte compact (QR code, ou son équivalent en fichier sous Windows) : JSON compressé en base64url, sans photos ;
/// - JSON brut.</summary>
public static class ExchangeArchive
{
    /// <summary>Extension du fichier partagé.</summary>
    public const string FileExtension = ".mordheim";

    private const string ManifestName = "package.json";
    private const string PhotoFolder = "photos/";

    /// <summary>Préfixe du texte compact (format + version du codage), pour reconnaître un QR qui n'est pas à nous.</summary>
    internal const string CompactPrefix = "MLX1:";

    /// <summary>Plafonds de lecture : une archive reçue d'ailleurs ne doit pas pouvoir remplir la mémoire.</summary>
    private const long MaxManifestBytes = 32L * 1024 * 1024;
    private const long MaxPhotoBytes = 20L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = ExchangeService.ValueOptions.Encoder,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
    };

    public static string ToJson(ExchangePackage package) => JsonSerializer.Serialize(package, JsonOptions);

    public static ExchangePackage FromJson(string json)
    {
        ExchangePackage? package;
        try
        {
            package = JsonSerializer.Deserialize<ExchangePackage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ExchangeFormatException("Ce n'est pas un fichier Mordheim Ledger.", ex);
        }
        Validate(package);
        return package!;
    }

    internal static void Validate(ExchangePackage? package)
    {
        if (package is null || package.Format != ExchangePackage.FormatName)
            throw new ExchangeFormatException("Ce n'est pas un fichier Mordheim Ledger.");
        if (package.Version > ExchangePackage.CurrentVersion)
            throw new ExchangeFormatException($"Fichier créé par une version plus récente de l'appli (format {package.Version}).");
    }

    public static async Task WriteAsync(ExchangePackage package, Stream output)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var manifest = zip.CreateEntry(ManifestName, CompressionLevel.Optimal);
        await using (var stream = manifest.Open())
            await JsonSerializer.SerializeAsync(stream, package, JsonOptions);
        foreach (var (path, bytes) in package.Photos)
        {
            // Déjà compressées (JPEG) : stockées telles quelles.
            var entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
            await using var stream = entry.Open();
            await stream.WriteAsync(bytes);
        }
    }

    public static async Task<ExchangePackage> ReadAsync(Stream input)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new ExchangeFormatException("Ce n'est pas un fichier Mordheim Ledger.", ex);
        }
        using (zip)
        {
            var manifest = zip.GetEntry(ManifestName) ?? throw new ExchangeFormatException("Ce n'est pas un fichier Mordheim Ledger.");
            var package = FromJson(Encoding.UTF8.GetString(await ReadEntryAsync(manifest, MaxManifestBytes)));
            foreach (var path in PhotoPaths(package))
                if (zip.GetEntry(path) is { } entry)
                    package.Photos[path] = await ReadEntryAsync(entry, MaxPhotoBytes);
            return package;
        }
    }

    /// <summary>Texte pour un QR code (photos jamais incluses).</summary>
    public static string ToCompactText(ExchangePackage package)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(Encoding.UTF8.GetBytes(ToJson(package)));
        return CompactPrefix + Convert.ToBase64String(buffer.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static ExchangePackage FromCompactText(string text)
    {
        text = text.Trim();
        if (!text.StartsWith(CompactPrefix, StringComparison.Ordinal))
            throw new ExchangeFormatException("Ce n'est pas un code Mordheim Ledger.");
        try
        {
            var base64 = text[CompactPrefix.Length..].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            using var zlib = new ZLibStream(new MemoryStream(Convert.FromBase64String(base64)), CompressionMode.Decompress);
            using var json = new MemoryStream();
            CopyLimited(zlib, json, MaxManifestBytes);
            return FromJson(Encoding.UTF8.GetString(json.ToArray()));
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException)
        {
            throw new ExchangeFormatException("Code Mordheim Ledger endommagé.", ex);
        }
    }

    /// <summary>Chemins de photo cités par les guerriers du paquet, seuls lus dans l'archive (et seulement sous
    /// « photos/ », jamais un chemin qui remonterait ailleurs).</summary>
    private static IEnumerable<string> PhotoPaths(ExchangePackage package)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        void Visit(PlayedRow row)
        {
            if (row.Values.TryGetValue("PhotoPath", out var value) && value.ValueKind == JsonValueKind.String
                && value.GetString() is { } path && path.StartsWith(PhotoFolder, StringComparison.Ordinal) && !path.Contains(".."))
                paths.Add(path);
            foreach (var children in row.Children.Values)
                foreach (var child in children) Visit(child);
        }
        foreach (var warband in package.Warbands) Visit(warband);
        return paths;
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, long maxBytes)
    {
        if (entry.Length > maxBytes) throw new ExchangeFormatException("Fichier trop volumineux.");
        await using var stream = entry.Open();
        using var buffer = new MemoryStream();
        CopyLimited(stream, buffer, maxBytes);
        return buffer.ToArray();
    }

    private static void CopyLimited(Stream source, Stream destination, long maxBytes)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new ExchangeFormatException("Fichier trop volumineux.");
            destination.Write(chunk, 0, read);
        }
    }
}
