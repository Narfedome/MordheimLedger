using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using SQLite;

// Outil de build (voir MordheimLedgerApp.csproj/GenerateSeedDatabase) : produit une base SQLite déjà
// entièrement seedée depuis Data/SeedData/*.json, embarquée ensuite comme asset (Resources/Raw/seed.db3)
// et copiée telle quelle au premier lancement (MauiProgram.EnsureDatabaseFileExists) - évite de rejouer
// les ~22 passes de seed JSON->SQLite à froid sur l'appareil de l'utilisateur. Réutilise AppDatabase tel
// quel plutôt que de dupliquer sa logique de seed : sur un fichier neuf/inexistant, son garde-fou "table
// vide -> seed" (InitializeAsync) se déclenche déjà tout seul.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: DbSeedGenerator <output-db-path>");
    return 1;
}

var outputPath = args[0];

// Garde-fou de version (voir SeedContent) : les bases installées ne se mettent à jour que si
// ContentVersion.json a augmenté, donc des JSON modifiés sans incrément ne partiraient jamais chez
// l'utilisateur. Comparé à la seed.db3 précédente (commitée) - absente ou sans empreinte : pas de contrôle.
if (File.Exists(outputPath) && ReadPreviousMeta(outputPath) is { } previous
    && previous.Fingerprint != SeedContent.Fingerprint && SeedContent.Version <= previous.Version)
{
    Console.Error.WriteLine($"error DBSEED01: les JSON de Data/SeedData ont changé mais {SeedContent.VersionFileName} " +
        $"est toujours à la version {SeedContent.Version} - l'incrémenter (> {previous.Version}) pour que les bases " +
        "installées reçoivent la mise à jour.");
    return 1;
}

if (File.Exists(outputPath)) File.Delete(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

var db = new AppDatabase(outputPath);
await db.Initialization;
await db.Connection.CloseAsync();

Console.WriteLine($"Seeded database (content version {SeedContent.Version}) written to {outputPath}");
return 0;

static (int Version, string Fingerprint)? ReadPreviousMeta(string path)
{
    using var connection = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
    if (connection.GetTableInfo(nameof(ContentMetaEntity)).Count == 0) return null;
    var meta = connection.Table<ContentMetaEntity>().ToDictionary(m => m.Key, m => m.Value);
    return meta.TryGetValue(SeedContent.VersionKey, out var version) && meta.TryGetValue(SeedContent.FingerprintKey, out var fingerprint)
        ? (int.Parse(version), fingerprint)
        : null;
}
