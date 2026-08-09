using MordheimLedgerApp.Core.Data;

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
if (File.Exists(outputPath)) File.Delete(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

var db = new AppDatabase(outputPath);
await db.Initialization;

Console.WriteLine($"Seeded database written to {outputPath}");
return 0;
