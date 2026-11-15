using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Sauvegarde complète (v2, lot 1) : archive écrite sur un appareil, restaurée sur un autre dont la base reste
/// ouverte, puis annulée.</summary>
public class BackupServiceTests : IDisposable
{
    private sealed class Device : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"mordheimledger-backup-tests-{Guid.NewGuid()}");
        public AppDatabase Db { get; }
        public ILibraryService Library { get; }
        public IWarbandService Warbands { get; }
        public WarriorPhotoStore Photos { get; }
        public BackupService Backup { get; }

        public Device()
        {
            Directory.CreateDirectory(_root);
            Db = new AppDatabase(Path.Combine(_root, "mordheimledger.db3"));
            Library = new LibraryService(Db);
            Photos = new WarriorPhotoStore(_root);
            Warbands = new WarbandService(Db, Library, Photos);
            Backup = new BackupService(Db, Photos, Path.Combine(_root, "restore-undo" + BackupService.FileExtension));
        }

        public void Dispose()
        {
            try { Db.Initialization.GetAwaiter().GetResult(); } catch { /* déjà signalé par le test */ }
            Db.Connection.CloseAsync().GetAwaiter().GetResult();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private readonly Device _phone = new();
    private readonly Device _laptop = new();

    public void Dispose()
    {
        _phone.Dispose();
        _laptop.Dispose();
    }

    private static readonly Dictionary<string, string> PhoneSettings = new() { ["app_lang"] = "fr", ["app_theme"] = "2" };

    private async Task<MemoryStream> PhoneBackupAsync()
    {
        var warband = (await SampleWarband.CreateAsync(_phone.Warbands, _phone.Library, "en", "Les Roses Sanglantes", ["Guerriers", "Tireurs"]))!;
        var captain = (await _phone.Warbands.GetWarriorsAsync(warband.Id, "en")).First(w => w.IsHero);
        var photo = await _phone.Photos.SaveAsync(new MemoryStream([9, 8, 7]), ".jpg");
        await _phone.Db.Connection.ExecuteAsync("UPDATE WarriorEntity SET PhotoPath = ? WHERE Id = ?", photo, captain.Id);

        var file = new MemoryStream();
        var manifest = await _phone.Backup.WriteAsync(file, "1.5.0", PhoneSettings);
        Assert.Equal(1, manifest.WarbandCount);
        Assert.Equal(1, manifest.PhotoCount);
        file.Position = 0;
        return file;
    }

    [Fact]
    public async Task Restore_ReplacesEverything_OnALiveDatabase_AndCanBeUndone()
    {
        using var file = await PhoneBackupAsync();
        var laptopWarband = await _laptop.Warbands.CreateWarbandAsync("Bande du portable",
            (await _laptop.Library.GetWarbandArchetypesAsync("en")).First());
        var versionBefore = _laptop.Db.DataVersion;

        var preview = await BackupService.ReadManifestAsync(file, "1.5.0");
        Assert.Equal(1, preview.WarbandCount);
        file.Position = 0;
        var manifest = await _laptop.Backup.RestoreAsync(file, "1.5.0", new Dictionary<string, string> { ["app_lang"] = "en" });

        Assert.Equal("fr", manifest.Settings["app_lang"]);
        Assert.NotEqual(versionBefore, _laptop.Db.DataVersion);
        var warband = Assert.Single(await _laptop.Warbands.GetWarbandsAsync());
        Assert.Equal("Les Roses Sanglantes", warband.Name);
        var captain = (await _laptop.Warbands.GetWarriorsAsync(warband.Id, "en")).First(w => w.IsHero);
        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(_laptop.Photos.GetExistingFullPath(captain.PhotoPath)!));
        Assert.NotEmpty(await _laptop.Library.GetEquipmentItemsAsync("en"));

        // La base reste utilisable après la restauration (même connexion).
        await _laptop.Warbands.AddHistoryEntryAsync(warband.Id, "Après la restauration.");
        Assert.Contains(await _laptop.Warbands.GetHistoryEntriesAsync(warband.Id), h => h.Text == "Après la restauration.");

        Assert.True(_laptop.Backup.CanUndoRestore);
        var undone = await _laptop.Backup.UndoRestoreAsync("1.5.0");
        Assert.Equal("en", undone.Settings["app_lang"]);
        Assert.False(_laptop.Backup.CanUndoRestore);
        Assert.Equal(laptopWarband.Name, Assert.Single(await _laptop.Warbands.GetWarbandsAsync()).Name);
        Assert.Empty(_laptop.Photos.EnumerateRelativePaths());
    }

    [Fact]
    public async Task Backup_FromANewerApp_OrNotABackup_IsRefused_WithoutTouchingData()
    {
        using var file = await PhoneBackupAsync();
        await Assert.ThrowsAsync<BackupFormatException>(() => _laptop.Backup.RestoreAsync(file, "1.4.2", new Dictionary<string, string>()));
        Assert.False(_laptop.Backup.CanUndoRestore);
        Assert.Empty(await _laptop.Warbands.GetWarbandsAsync());

        await Assert.ThrowsAsync<BackupFormatException>(() => BackupService.ReadManifestAsync(new MemoryStream([1, 2, 3]), "1.5.0"));
    }

    [Theory]
    [InlineData("1.6.0", "1.5.0", true)]
    [InlineData("1.5.0", "1.5.0", false)]
    [InlineData("1.4.9", "1.5.0", false)]
    [InlineData(null, "1.5.0", false)]
    [InlineData("1.6", null, false)]
    public void IsNewer_ComparesAppVersions(string? backup, string? current, bool expected) =>
        Assert.Equal(expected, BackupService.IsNewer(backup, current));
}
