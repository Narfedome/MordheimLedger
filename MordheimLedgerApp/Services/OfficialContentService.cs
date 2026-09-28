using MordheimLedgerApp.Core.Data;

namespace MordheimLedgerApp.Services;

/// <summary>Côté appli de la synchro du contenu officiel (AppDatabase.SyncOfficialContentAsync) : fournit la
/// seed.db3 embarquée et formule le bilan pour le joueur - au lancement (une seule fois, sur la liste des
/// bandes) et depuis les Paramètres.</summary>
public class OfficialContentService
{
    private readonly AppDatabase _db;
    private bool _startupMessageTaken;

    public OfficialContentService(AppDatabase db) => _db = db;

    public static Task<Stream> OpenEmbeddedSeedAsync() => FileSystem.OpenAppPackageFileAsync("seed.db3");

    public Task<ContentSyncReport> SyncNowAsync() => _db.SyncOfficialContentAsync(OpenEmbeddedSeedAsync);

    /// <summary>Message à montrer une fois après une synchro de démarrage qui a changé quelque chose (ou
    /// échoué) - null sinon, et null à tout appel suivant.</summary>
    public async Task<string?> TakeStartupMessageAsync()
    {
        if (_startupMessageTaken) return null;
        _startupMessageTaken = true;
        await _db.Initialization;

        var loc = LocalizationService.Instance;
        if (_db.StartupSyncError is { } error)
            return string.Format(loc["OfficialContentSyncFailed"], error.Message);
        if (_db.StartupSyncReport is not { HasChanges: true } report) return null;
        var message = loc["OfficialContentUpdated"] + "\n\n" + Describe(report);
        return report.Conflicts > 0 ? message + "\n\n" + loc["OfficialContentReviewHint"] : message;
    }

    /// <summary>Bilan en toutes lettres, une ligne par type de changement non nul.</summary>
    public static string Describe(ContentSyncReport report)
    {
        var loc = LocalizationService.Instance;
        if (!report.HasChanges) return loc["OfficialContentNoChange"];
        var lines = new List<string>();
        if (report.Updated > 0) lines.Add(string.Format(loc["OfficialContentUpdatedCount"], report.Updated));
        if (report.Inserted > 0) lines.Add(string.Format(loc["OfficialContentInsertedCount"], report.Inserted));
        if (report.Retired > 0) lines.Add(string.Format(loc["OfficialContentRetiredCount"], report.Retired));
        if (report.Conflicts > 0) lines.Add(string.Format(loc["OfficialContentConflictCount"], report.Conflicts));
        return string.Join("\n", lines);
    }
}
