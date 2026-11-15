// Stryker disable all : requêtes sqlite-net (arbres d'expression) - un mutant y empêche la traduction SQL et fausse les tests de mutation (voir MordheimLedgerApp.Tests/stryker-config.json).
using MordheimLedgerApp.Core.Data.Entities;
using SQLite;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Sauvegarde complète (v2, lot 1) : copie cohérente de la base et restauration en place, sans fermer la
/// connexion de l'appli (voir Services/BackupService pour l'archive et les photos).</summary>
public partial class AppDatabase
{
    /// <summary>Copie cohérente de toute la base dans destinationPath (VACUUM INTO : compacte, sans transaction en
    /// cours). Le fichier ne doit pas exister.</summary>
    public async Task CopyToAsync(string destinationPath)
    {
        await Initialization;
        await _db.ExecuteAsync($"VACUUM INTO '{destinationPath.Replace("'", "''")}'");
    }

    /// <summary>Nombre de bandes, pour décrire une sauvegarde.</summary>
    public async Task<int> CountWarbandsAsync()
    {
        await Initialization;
        return await _db.Table<WarbandEntity>().CountAsync();
    }

    /// <summary>Vrai si path est une base Mordheim Ledger lisible et intacte.</summary>
    public static bool IsValidDatabaseFile(string path)
    {
        try
        {
            using var connection = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
            return connection.ExecuteScalar<string>("PRAGMA integrity_check") == "ok"
                && connection.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = ?", nameof(WarbandEntity)) == 1;
        }
        catch (SQLiteException)
        {
            return false;
        }
    }

    /// <summary>Remplace tout le contenu de la base par celui de sourcePath (API de sauvegarde SQLite, page par page,
    /// sous verrou : la connexion de l'appli reste ouverte et voit le nouveau contenu), puis rejoue la préparation du
    /// lancement - tables et colonnes ajoutées depuis (sauvegarde plus ancienne), synchro du contenu officiel si son
    /// empreinte diffère, réparations des parties jouées. Les écrans déjà chargés gardent l'ancien état : l'appli
    /// demande un redémarrage, comme après ResetAsync.</summary>
    public async Task RestoreFromAsync(string sourcePath)
    {
        await Initialization;
        if (!IsValidDatabaseFile(sourcePath)) throw new InvalidDataException("Base de sauvegarde illisible.");

        var livePath = _db.DatabasePath;
        await Task.Run(() =>
        {
            using var source = new SQLiteConnection(sourcePath, SQLiteOpenFlags.ReadOnly);
            source.Backup(livePath);
        });

        InvalidateAllCachedTables();
        ClearSeedLookups();
        StartupSyncReport = null;
        StartupSyncError = null;
        await CreateAllTablesAsync();
        await RunInTransactionAsync(SeedOrIdentifyOfficialContentAsync);
        await SyncOfficialContentOnStartupAsync();
        await RunInTransactionAsync(RepairPlayedDataAsync);
    }
}
