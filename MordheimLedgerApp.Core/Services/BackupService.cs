using System.IO.Compression;
using System.Text.Json;
using MordheimLedgerApp.Core.Data;

namespace MordheimLedgerApp.Core.Services;

/// <summary>Description d'une sauvegarde (« manifest.json » de l'archive).</summary>
public sealed class BackupManifest
{
    public const string FormatName = "mordheim-ledger-backup";
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Version de l'appli qui a écrit la sauvegarde (AppInfo.VersionString).</summary>
    public string? AppVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public int WarbandCount { get; set; }
    public int PhotoCount { get; set; }

    /// <summary>Réglages restaurés avec les données (langue, thème, palette), par clé de préférence. L'appli les lit
    /// et les applique elle-même : Core ne connaît pas les Preferences de MAUI.</summary>
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.Ordinal);
}

public enum BackupProblem
{
    NotABackup,
    Damaged,
    FromNewerApp,
}

/// <summary>Sauvegarde illisible : pas une sauvegarde Mordheim Ledger, endommagée, ou faite par une version plus
/// récente de l'appli (Problem : à traduire par l'appli, le message n'est qu'un détail technique).</summary>
public sealed class BackupFormatException(BackupProblem problem, string message, Exception? inner = null) : Exception(message, inner)
{
    public BackupProblem Problem { get; } = problem;
}

/// <summary>Sauvegarde complète (v2, lot 1) : archive .zip (« .mordheimbackup ») = manifest.json + copie de la base
/// (database.db3) + photos des guerriers (warrior-photos/). Choix de l'utilisateur : copie brute de la base plutôt que
/// le format d'échange (complète par construction), réglages inclus, et une copie de secours des données remplacées
/// gardée à chaque restauration pour pouvoir l'annuler (une seule, la dernière).</summary>
public sealed class BackupService
{
    public const string FileExtension = ".mordheimbackup";

    private const string ManifestName = "manifest.json";
    private const string DatabaseName = "database.db3";
    private const long MaxManifestBytes = 1024 * 1024;
    private const long MaxPhotoBytes = 20L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly AppDatabase _db;
    private readonly WarriorPhotoStore _photos;
    private readonly string _undoPath;

    /// <param name="undoPath">Fichier de la copie de secours (dans les données de l'appli, exclu de la sauvegarde
    /// Google d'Android).</param>
    public BackupService(AppDatabase db, WarriorPhotoStore photos, string undoPath)
    {
        _db = db;
        _photos = photos;
        _undoPath = undoPath;
    }

    /// <summary>Une restauration peut être annulée (copie de secours présente).</summary>
    public bool CanUndoRestore => File.Exists(_undoPath);

    /// <summary>Date de la copie de secours, null s'il n'y en a pas.</summary>
    public DateTime? UndoCreatedAtUtc => CanUndoRestore ? File.GetLastWriteTimeUtc(_undoPath) : null;

    public async Task<BackupManifest> WriteAsync(Stream output, string? appVersion, IReadOnlyDictionary<string, string> settings)
    {
        var tempDatabase = TempPath(".db3");
        try
        {
            await _db.CopyToAsync(tempDatabase);
            var photos = _photos.EnumerateRelativePaths().ToList();
            var manifest = new BackupManifest
            {
                AppVersion = appVersion,
                CreatedAtUtc = DateTime.UtcNow,
                WarbandCount = await _db.CountWarbandsAsync(),
                PhotoCount = photos.Count,
                Settings = new Dictionary<string, string>(settings, StringComparer.Ordinal),
            };

            using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            await using (var stream = zip.CreateEntry(ManifestName).Open())
                await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions);
            zip.CreateEntryFromFile(tempDatabase, DatabaseName, CompressionLevel.Optimal);
            foreach (var photo in photos)
                if (_photos.GetExistingFullPath(photo) is { } fullPath)
                    zip.CreateEntryFromFile(fullPath, photo, CompressionLevel.NoCompression);
            return manifest;
        }
        finally
        {
            File.Delete(tempDatabase);
        }
    }

    /// <summary>Lit et vérifie la description d'une sauvegarde, sans rien restaurer (dialog de confirmation).</summary>
    public static async Task<BackupManifest> ReadManifestAsync(Stream input, string? currentAppVersion)
    {
        using var zip = OpenArchive(input);
        return await ReadManifestAsync(zip, currentAppVersion);
    }

    /// <summary>Remplace toutes les données (base + photos) par celles de la sauvegarde, après avoir gardé une copie de
    /// secours des données actuelles. Renvoie la description de la sauvegarde, dont les réglages à appliquer.</summary>
    public Task<BackupManifest> RestoreAsync(Stream input, string? currentAppVersion, IReadOnlyDictionary<string, string> currentSettings) =>
        RestoreCoreAsync(input, currentAppVersion, currentSettings);

    /// <summary>Remet les données d'avant la dernière restauration, puis supprime la copie de secours.</summary>
    public async Task<BackupManifest> UndoRestoreAsync(string? currentAppVersion)
    {
        if (!CanUndoRestore) throw new InvalidOperationException("Aucune restauration à annuler.");
        BackupManifest manifest;
        await using (var undo = File.OpenRead(_undoPath))
            manifest = await RestoreCoreAsync(undo, currentAppVersion, currentSettings: null);
        File.Delete(_undoPath);
        return manifest;
    }

    private async Task<BackupManifest> RestoreCoreAsync(Stream input, string? currentAppVersion, IReadOnlyDictionary<string, string>? currentSettings)
    {
        var tempDatabase = TempPath(".db3");
        try
        {
            // 1. Tout vérifier et extraire avant de toucher à quoi que ce soit.
            BackupManifest manifest;
            var photos = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            try
            {
                using var zip = OpenArchive(input);
                manifest = await ReadManifestAsync(zip, currentAppVersion);
                var database = zip.GetEntry(DatabaseName) ?? throw new BackupFormatException(BackupProblem.Damaged, "Sauvegarde incomplète (base absente).");
                database.ExtractToFile(tempDatabase);
                if (!AppDatabase.IsValidDatabaseFile(tempDatabase)) throw new BackupFormatException(BackupProblem.Damaged, "Sauvegarde endommagée.");
                foreach (var entry in zip.Entries.Where(IsPhotoEntry))
                    photos[entry.FullName] = await ReadEntryAsync(entry, MaxPhotoBytes);
            }
            catch (InvalidDataException ex)
            {
                throw new BackupFormatException(BackupProblem.Damaged, "Sauvegarde endommagée.", ex);
            }

            // 2. Copie de secours des données actuelles (sauf pour une annulation, qui la consomme).
            if (currentSettings is not null)
            {
                var pending = _undoPath + ".tmp";
                await using (var undo = File.Create(pending))
                    await WriteAsync(undo, currentAppVersion, currentSettings);
                File.Move(pending, _undoPath, overwrite: true);
            }

            // 3. Base, puis photos.
            await _db.RestoreFromAsync(tempDatabase);
            _photos.DeleteAll();
            foreach (var (path, bytes) in photos)
            {
                using var stream = new MemoryStream(bytes, writable: false);
                await _photos.WriteAsync(path, stream);
            }
            return manifest;
        }
        finally
        {
            File.Delete(tempDatabase);
        }
    }

    private static ZipArchive OpenArchive(Stream input)
    {
        try
        {
            return new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new BackupFormatException(BackupProblem.NotABackup, "Ce n'est pas une sauvegarde Mordheim Ledger.", ex);
        }
    }

    private static async Task<BackupManifest> ReadManifestAsync(ZipArchive zip, string? currentAppVersion)
    {
        var entry = zip.GetEntry(ManifestName) ?? throw new BackupFormatException(BackupProblem.NotABackup, "Ce n'est pas une sauvegarde Mordheim Ledger.");
        BackupManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(await ReadEntryAsync(entry, MaxManifestBytes), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new BackupFormatException(BackupProblem.NotABackup, "Ce n'est pas une sauvegarde Mordheim Ledger.", ex);
        }
        if (manifest is null || manifest.Format != BackupManifest.FormatName)
            throw new BackupFormatException(BackupProblem.NotABackup, "Ce n'est pas une sauvegarde Mordheim Ledger.");
        if (manifest.Version > BackupManifest.CurrentVersion || IsNewer(manifest.AppVersion, currentAppVersion))
            throw new BackupFormatException(BackupProblem.FromNewerApp, $"Sauvegarde faite avec une version plus récente de l'appli ({manifest.AppVersion}) : mettez l'appli à jour.");
        return manifest;
    }

    /// <summary>Une base d'une version plus récente peut porter des données que cette version ne sait pas lire (et la
    /// synchro du contenu officiel retirerait ce qu'elle ne connaît pas) : refusée.</summary>
    internal static bool IsNewer(string? backupVersion, string? currentVersion) =>
        Version.TryParse(backupVersion, out var backup) && Version.TryParse(currentVersion, out var current) && backup > current;

    private static bool IsPhotoEntry(ZipArchiveEntry entry) =>
        entry.FullName.StartsWith(WarriorPhotoStore.FolderName + "/", StringComparison.Ordinal)
        && entry.Name.Length > 0
        && !entry.FullName.Contains("..", StringComparison.Ordinal)
        && entry.FullName.Count(c => c == '/') == 1;

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, long maxBytes)
    {
        if (entry.Length > maxBytes) throw new BackupFormatException(BackupProblem.Damaged, "Sauvegarde endommagée (fichier trop volumineux).");
        await using var stream = entry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        if (buffer.Length > maxBytes) throw new BackupFormatException(BackupProblem.Damaged, "Sauvegarde endommagée (fichier trop volumineux).");
        return buffer.ToArray();
    }

    private static string TempPath(string extension) => Path.Combine(Path.GetTempPath(), $"mordheimledger-backup-{Guid.NewGuid():N}{extension}");
}
