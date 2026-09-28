using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data;

/// <summary>Bilan d'une synchro du contenu officiel.</summary>
public sealed record ContentSyncReport(int Updated, int Inserted, int Retired, int Conflicts)
{
    public bool HasChanges => Updated + Inserted + Retired + Conflicts > 0;
}

/// <summary>Un champ où la version officielle et celle du joueur diffèrent. Field = nom de colonne (ex.
/// "Cost", "DescriptionKey") ou de table de jointure (ex. "EquipmentItemSpecialRuleEntity") - l'appli en
/// fait un libellé. Valeurs déjà en texte (traductions, noms des lignes visées), vides si absentes.</summary>
public sealed record ContentFieldDifference(string Field, string Official, string Mine);

/// <summary>Entrée Modifiée en attente de choix (voir AppDatabase.GetContentConflictsAsync).</summary>
public sealed record ContentConflict(string OfficialId, string TableName, string Name, IReadOnlyList<ContentFieldDifference> Differences);

/// <summary>Synchro du contenu officiel (2026-09-28) : met à jour le catalogue d'une base déjà installée
/// depuis une seed.db3 plus récente, en faisant correspondre les lignes par OfficialId. Les parties jouées
/// (Warband/Warrior...) ne sont jamais touchées et restent valides : une ligne mise à jour garde son Id local.
///
/// Par ligne du catalogue (voir OfficialContentSchema) :
/// - Officielle : réécrite depuis la seed (valeurs, traductions, jointures dont elle est propriétaire) si
///   elle en diffère ;
/// - absente en local : insérée ;
/// - Modifiée par le joueur : jamais écrasée - si la version officielle a changé depuis la dernière synchro
///   (OfficialContentHashEntity), elle est notée en attente de choix (ContentConflictEntity) ;
/// - retirée de la seed, ou Officielle/Modifiée sans OfficialId (doublon hérité d'un ancien Backfill) :
///   passe en Personnalisée ;
/// - Personnalisée : jamais touchée.
///
/// Comparaisons par empreinte (ComputeHash) exprimée en OfficialId plutôt qu'en Id - deux bases n'ont pas
/// les mêmes Id auto-incrémentés, ni les mêmes clés de traduction.</summary>
public partial class AppDatabase
{
    private readonly Func<Task<Stream>>? _officialSeed;

    /// <summary>Bilan de la synchro faite pendant l'initialisation (null si aucune n'a eu lieu).</summary>
    public ContentSyncReport? StartupSyncReport { get; private set; }

    /// <summary>Erreur de la synchro de démarrage : annulée (transaction), l'appli démarre quand même sur
    /// l'ancien contenu et retentera au prochain lancement.</summary>
    public Exception? StartupSyncError { get; private set; }

    private async Task SyncOfficialContentOnStartupAsync()
    {
        if (_officialSeed is null || (await ReadContentMetaAsync()).Version >= SeedContent.Version) return;
        try
        {
            StartupSyncReport = await SyncFromStreamAsync(_officialSeed);
        }
        catch (Exception ex)
        {
            StartupSyncError = ex;
        }
    }

    /// <summary>Synchro à la demande (bouton des Paramètres) depuis la seed.db3 embarquée, même si la
    /// version est déjà à jour - répare aussi une base dont le catalogue aurait dérivé.</summary>
    public async Task<ContentSyncReport> SyncOfficialContentAsync(Func<Task<Stream>> officialSeed)
    {
        await Initialization;
        return await SyncFromStreamAsync(officialSeed);
    }

    public async Task<ContentSyncReport> SyncOfficialContentAsync(string seedDatabasePath)
    {
        await Initialization;
        return await SyncFromFileAsync(seedDatabasePath);
    }

    /// <summary>Entrées Modifiées dont la version officielle a changé, en attente du choix du joueur.</summary>
    public async Task<int> GetPendingContentConflictCountAsync()
    {
        await Initialization;
        return await _db.Table<ContentConflictEntity>().CountAsync();
    }

    /// <summary>Entrées en attente de choix, avec pour chacune les champs où la version officielle et celle du
    /// joueur diffèrent (textes dans languageCode). Une entrée en attente devenue sans objet (plus dans la
    /// seed, ou plus en local) est retirée au passage.</summary>
    public async Task<List<ContentConflict>> GetContentConflictsAsync(Func<Task<Stream>> officialSeed, string languageCode)
    {
        await Initialization;
        var pending = await _db.Table<ContentConflictEntity>().ToListAsync();
        if (pending.Count == 0) return [];

        return await WithSeedFileAsync(officialSeed, async seedPath =>
        {
            var seed = await LoadSnapshotAsync(seedPath);
            var local = await ContentSnapshot.LoadAsync(_db);
            var conflicts = new List<ContentConflict>();
            foreach (var entry in pending)
            {
                if (FindRows(seed, local, entry) is not var (table, seedRow, localRow))
                {
                    await _db.DeleteAsync(entry);
                    continue;
                }
                var mapping = local.Mappings[table.Type];
                var differences = new List<ContentFieldDifference>();
                foreach (var column in mapping.Columns.Where(c => !c.IsPK && c.Name is not ("Source" or "OfficialId")))
                {
                    var official = seed.Display(table, seedRow, column.Name, languageCode);
                    var mine = local.Display(table, localRow, column.Name, languageCode);
                    if (official != mine) differences.Add(new ContentFieldDifference(column.Name, official, mine));
                }
                foreach (var join in OfficialContentSchema.Joins.Where(j => j.Owner == table.Type))
                {
                    var official = seed.DisplayJoin(join, (int)mapping.PK.GetValue(seedRow), languageCode);
                    var mine = local.DisplayJoin(join, (int)mapping.PK.GetValue(localRow), languageCode);
                    if (official != mine) differences.Add(new ContentFieldDifference(join.Type.Name, official, mine));
                }
                conflicts.Add(new ContentConflict(entry.OfficialId, entry.TableName,
                    local.Text((string?)mapping.FindColumn("NameKey").GetValue(localRow), languageCode), differences));
            }
            return conflicts;
        });
    }

    /// <summary>Choix du joueur sur une entrée en attente : garder sa version (rien ne change, la version
    /// officielle actuelle est déjà mémorisée - il ne sera plus sollicité tant qu'elle ne rebouge pas) ou
    /// reprendre l'officielle (réécrite comme à la synchro, repasse en Officielle).</summary>
    public async Task ResolveContentConflictAsync(string officialId, bool takeOfficial, Func<Task<Stream>> officialSeed)
    {
        await Initialization;
        var entry = await _db.FindAsync<ContentConflictEntity>(officialId);
        if (entry is null) return;
        if (!takeOfficial)
        {
            await _db.DeleteAsync(entry);
            return;
        }

        await WithSeedFileAsync(officialSeed, async seedPath =>
        {
            var seed = await LoadSnapshotAsync(seedPath);
            await RunInTransactionAsync(async () =>
            {
                var local = await ContentSnapshot.LoadAsync(_db);
                if (FindRows(seed, local, entry) is var (table, seedRow, localRow))
                {
                    // Toutes les lignes officielles déjà présentes des deux côtés, pour traduire les références.
                    var localIdBySeedId = OfficialContentSchema.Tables.ToDictionary(t => t.Type, t => seed.IdByOfficialId[t.Type]
                        .Where(s => local.IdByOfficialId[t.Type].ContainsKey(s.Key))
                        .ToDictionary(s => s.Value, s => local.IdByOfficialId[t.Type][s.Key]));
                    await ApplySeedRowAsync(seed, table, seedRow, localRow, localIdBySeedId);
                }
                await _db.DeleteAsync(entry);
            });
            return true;
        });
    }

    private static (CatalogTable Table, object SeedRow, object LocalRow)? FindRows(ContentSnapshot seed, ContentSnapshot local, ContentConflictEntity entry)
    {
        var table = OfficialContentSchema.Tables.FirstOrDefault(t => local.Mappings[t.Type].TableName == entry.TableName);
        if (table is null
            || !seed.IdByOfficialId[table.Type].TryGetValue(entry.OfficialId, out var seedId)
            || !local.IdByOfficialId[table.Type].TryGetValue(entry.OfficialId, out var localId))
            return null;
        return (table, seed.Rows[table.Type][seedId], local.Rows[table.Type][localId]);
    }

    private Task<ContentSyncReport> SyncFromStreamAsync(Func<Task<Stream>> officialSeed) =>
        WithSeedFileAsync(officialSeed, SyncFromFileAsync);

    /// <summary>SQLite ne lit qu'un fichier : l'asset embarqué est d'abord copié dans un fichier temporaire,
    /// supprimé après usage.</summary>
    private static async Task<T> WithSeedFileAsync<T>(Func<Task<Stream>> officialSeed, Func<string, Task<T>> use)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-official-{Guid.NewGuid():N}.db3");
        try
        {
            await using (var source = await officialSeed())
            await using (var destination = File.Create(tempPath))
                await source.CopyToAsync(destination);
            return await use(tempPath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static async Task<ContentSnapshot> LoadSnapshotAsync(string seedDatabasePath)
    {
        var connection = new SQLiteAsyncConnection(seedDatabasePath, SQLiteOpenFlags.ReadOnly);
        try
        {
            return await ContentSnapshot.LoadAsync(connection);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private async Task<ContentSyncReport> SyncFromFileAsync(string seedDatabasePath)
    {
        var seedConnection = new SQLiteAsyncConnection(seedDatabasePath, SQLiteOpenFlags.ReadOnly);
        try
        {
            var seed = await ContentSnapshot.LoadAsync(seedConnection);
            var seedMeta = (await seedConnection.Table<ContentMetaEntity>().ToListAsync()).ToDictionary(m => m.Key, m => m.Value);

            ContentSyncReport report = null!;
            await RunInTransactionAsync(async () =>
            {
                report = await ApplySeedSnapshotAsync(seed);
                foreach (var (key, value) in seedMeta)
                    await _db.InsertOrReplaceAsync(new ContentMetaEntity { Key = key, Value = value });
            });
            return report;
        }
        finally
        {
            await seedConnection.CloseAsync();
        }
    }

    /// <summary>Clé "table|OfficialId" -> empreinte de chaque ligne officielle de la base, pour
    /// OfficialContentHashEntity. Appelée en fin de seed : une base neuve (copiée depuis seed.db3) connaît
    /// déjà la version officielle de chaque entrée.</summary>
    private async Task StoreOfficialContentHashesAsync()
    {
        var snapshot = await ContentSnapshot.LoadAsync(_db);
        await _db.ExecuteAsync("DELETE FROM OfficialContentHashEntity");
        foreach (var table in OfficialContentSchema.Tables)
            foreach (var row in snapshot.Rows[table.Type].Values)
                if (snapshot.OfficialIdOf(table.Type, row) is { } officialId)
                    await _db.InsertAsync(new OfficialContentHashEntity { OfficialId = officialId, Hash = snapshot.ComputeHash(table, row) });
    }

    private async Task<ContentSyncReport> ApplySeedSnapshotAsync(ContentSnapshot seed)
    {
        var local = await ContentSnapshot.LoadAsync(_db);
        var storedHashes = (await _db.Table<OfficialContentHashEntity>().ToListAsync()).ToDictionary(h => h.OfficialId, h => h.Hash);
        var existingConflicts = (await _db.Table<ContentConflictEntity>().ToListAsync()).Select(c => c.OfficialId).ToHashSet();
        var localIdBySeedId = OfficialContentSchema.Tables.ToDictionary(t => t.Type, _ => new Dictionary<int, int>());
        var toApply = new List<(CatalogTable Table, object SeedRow, object LocalRow)>();
        int updated = 0, inserted = 0, retired = 0, conflicts = 0;

        // 1. Correspondance par OfficialId ; les lignes absentes sont insérées d'abord à vide (références à
        //    0/null) pour que toutes aient un Id local avant de traduire la moindre référence.
        foreach (var table in OfficialContentSchema.Tables)
        {
            var mapping = local.Mappings[table.Type];
            foreach (var (seedId, seedRow) in seed.Rows[table.Type])
            {
                if (seed.OfficialIdOf(table.Type, seedRow) is not { } officialId) continue;
                var seedHash = seed.ComputeHash(table, seedRow);

                if (local.IdByOfficialId[table.Type].TryGetValue(officialId, out var localId))
                {
                    localIdBySeedId[table.Type][seedId] = localId;
                    var localRow = local.Rows[table.Type][localId];
                    var source = (ContentSource)mapping.FindColumn("Source").GetValue(localRow);
                    if (source == ContentSource.Official && local.ComputeHash(table, localRow) != seedHash)
                    {
                        toApply.Add((table, seedRow, localRow));
                        updated++;
                    }
                    else if (source == ContentSource.Modified
                             && (!storedHashes.TryGetValue(officialId, out var known) || known != seedHash)
                             && local.ComputeHash(table, localRow) != seedHash)
                    {
                        await _db.InsertOrReplaceAsync(new ContentConflictEntity { OfficialId = officialId, TableName = mapping.TableName });
                        if (existingConflicts.Add(officialId)) conflicts++;
                    }
                    continue;
                }

                var newRow = Activator.CreateInstance(table.Type)!;
                foreach (var column in mapping.Columns.Where(c => !c.IsPK))
                    column.SetValue(newRow, column.GetValue(seedRow));
                foreach (var (column, _) in table.ForeignKeys)
                    ClearReference(mapping.FindColumn(column), newRow);
                foreach (var (column, _) in table.CsvForeignKeys)
                    mapping.FindColumn(column).SetValue(newRow, null);
                foreach (var column in table.TranslationColumns)
                    mapping.FindColumn(column).SetValue(newRow, null);
                mapping.FindColumn("NameKey").SetValue(newRow, string.Empty);
                await _db.InsertAsync(newRow);
                localIdBySeedId[table.Type][seedId] = (int)mapping.PK.GetValue(newRow);
                toApply.Add((table, seedRow, newRow));
                inserted++;
            }

            // 2. Ce que la seed ne connaît plus (ou n'a jamais connu) cesse d'être officiel.
            var seedOfficialIds = seed.IdByOfficialId[table.Type];
            foreach (var localRow in local.Rows[table.Type].Values)
            {
                var sourceColumn = mapping.FindColumn("Source");
                if ((ContentSource)sourceColumn.GetValue(localRow) == ContentSource.Custom) continue;
                var officialId = local.OfficialIdOf(table.Type, localRow);
                if (officialId is not null && seedOfficialIds.ContainsKey(officialId)) continue;
                sourceColumn.SetValue(localRow, ContentSource.Custom);
                mapping.FindColumn("OfficialId").SetValue(localRow, null);
                await _db.UpdateAsync(localRow);
                if (officialId is not null) await _db.ExecuteAsync("DELETE FROM ContentConflictEntity WHERE OfficialId = ?", officialId);
                retired++;
            }
        }

        // 3. Recopie complète des lignes Officielles changées et des nouvelles.
        foreach (var (table, seedRow, localRow) in toApply)
            await ApplySeedRowAsync(seed, table, seedRow, localRow, localIdBySeedId);

        await _db.ExecuteAsync("DELETE FROM OfficialContentHashEntity");
        foreach (var table in OfficialContentSchema.Tables)
            foreach (var seedRow in seed.Rows[table.Type].Values)
                if (seed.OfficialIdOf(table.Type, seedRow) is { } officialId)
                    await _db.InsertOrReplaceAsync(new OfficialContentHashEntity { OfficialId = officialId, Hash = seed.ComputeHash(table, seedRow) });

        return new ContentSyncReport(updated, inserted, retired, conflicts);
    }

    /// <summary>Réécrit localRow à l'identique de seedRow (Id local conservé, références traduites vers les Id
    /// locaux, traductions recopiées sous les clés locales) et remplace les jointures dont elle est
    /// propriétaire. Sert aussi, plus tard, au choix "reprendre la version officielle" d'un conflit.</summary>
    private async Task ApplySeedRowAsync(ContentSnapshot seed, CatalogTable table, object seedRow, object localRow,
        Dictionary<Type, Dictionary<int, int>> localIdBySeedId)
    {
        var mapping = seed.Mappings[table.Type];
        var localId = (int)mapping.PK.GetValue(localRow);
        int MapId(Type target, int seedId) => localIdBySeedId[target].TryGetValue(seedId, out var id)
            ? id
            : throw new InvalidOperationException($"{table.Type.Name} -> {target.Name}#{seedId} introuvable dans la seed");

        foreach (var column in mapping.Columns.Where(c => !c.IsPK))
        {
            var value = column.GetValue(seedRow);
            if (table.TranslationColumns.Contains(column.Name))
                value = await CopyTranslationAsync(seed, (string?)value, (string?)column.GetValue(localRow));
            else if (table.ForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } target } && value is int seedRef && seedRef != 0)
                value = MapId(target, seedRef);
            else if (table.CsvForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } csvTarget } && value is string csv)
                value = string.Join(',', csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(id => MapId(csvTarget, int.Parse(id))));
            column.SetValue(localRow, value);
        }
        mapping.FindColumn("Source").SetValue(localRow, ContentSource.Official);
        await _db.UpdateAsync(localRow);

        var seedId = (int)mapping.PK.GetValue(seedRow);
        foreach (var join in OfficialContentSchema.Joins.Where(j => j.Owner == table.Type))
        {
            var joinMapping = seed.Mappings[join.Type];
            await _db.ExecuteAsync($"DELETE FROM \"{joinMapping.TableName}\" WHERE \"{join.OwnerColumn}\" = ?", localId);
            foreach (var other in seed.JoinedIds(join, seedId))
            {
                var joinRow = Activator.CreateInstance(join.Type)!;
                joinMapping.FindColumn(join.OwnerColumn).SetValue(joinRow, localId);
                joinMapping.FindColumn(join.OtherColumn).SetValue(joinRow, MapId(join.Other, other));
                await _db.InsertAsync(joinRow);
            }
        }
    }

    /// <summary>Recopie les traductions de seedKey (toutes langues) sous localKey - créée si la ligne n'en a
    /// pas encore. Renvoie la clé à stocker (null si la seed n'a pas de texte).</summary>
    private async Task<string?> CopyTranslationAsync(ContentSnapshot seed, string? seedKey, string? localKey)
    {
        if (!string.IsNullOrEmpty(localKey))
            await _db.ExecuteAsync("DELETE FROM TranslationEntity WHERE Key = ?", localKey);
        if (string.IsNullOrEmpty(seedKey)) return seedKey;

        var key = string.IsNullOrEmpty(localKey) ? Guid.NewGuid().ToString("N") : localKey;
        foreach (var (languageCode, value) in seed.TranslationsOf(seedKey))
            await _db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = languageCode, Value = value });
        return key;
    }

    private static void ClearReference(TableMapping.Column column, object row) =>
        column.SetValue(row, Nullable.GetUnderlyingType(row.GetType().GetProperty(column.PropertyName)!.PropertyType) is null ? 0 : null);

    /// <summary>Tout le catalogue d'une base, en mémoire (quelques milliers de lignes) : lignes par table,
    /// traductions, jointures.</summary>
    private sealed class ContentSnapshot
    {
        public Dictionary<Type, TableMapping> Mappings { get; } = new();
        public Dictionary<Type, Dictionary<int, object>> Rows { get; } = new();
        public Dictionary<Type, Dictionary<string, int>> IdByOfficialId { get; } = new();
        private readonly Dictionary<string, SortedDictionary<string, string>> _translations = new();
        private readonly Dictionary<Type, ILookup<int, int>> _joins = new();

        public static async Task<ContentSnapshot> LoadAsync(SQLiteAsyncConnection connection)
        {
            var snapshot = new ContentSnapshot();
            foreach (var table in OfficialContentSchema.Tables)
            {
                var mapping = await connection.GetMappingAsync(table.Type);
                snapshot.Mappings[table.Type] = mapping;
                var rows = await connection.QueryAsync(mapping, $"SELECT * FROM \"{mapping.TableName}\"");
                snapshot.Rows[table.Type] = rows.ToDictionary(r => (int)mapping.PK.GetValue(r));
                // Premier par Id si un ancien doublon partage un id (ne devrait plus arriver, cf. pont).
                snapshot.IdByOfficialId[table.Type] = snapshot.Rows[table.Type]
                    .Where(r => snapshot.OfficialIdOf(table.Type, r.Value) is not null)
                    .GroupBy(r => snapshot.OfficialIdOf(table.Type, r.Value)!)
                    .ToDictionary(g => g.Key, g => g.Min(r => r.Key));
            }
            foreach (var join in OfficialContentSchema.Joins)
            {
                var mapping = await connection.GetMappingAsync(join.Type);
                snapshot.Mappings[join.Type] = mapping;
                var owner = mapping.FindColumn(join.OwnerColumn);
                var other = mapping.FindColumn(join.OtherColumn);
                snapshot._joins[join.Type] = (await connection.QueryAsync(mapping, $"SELECT * FROM \"{mapping.TableName}\""))
                    .ToLookup(r => (int)owner.GetValue(r), r => (int)other.GetValue(r));
            }
            foreach (var t in await connection.Table<TranslationEntity>().ToListAsync())
            {
                if (!snapshot._translations.TryGetValue(t.Key, out var byLanguage))
                    snapshot._translations[t.Key] = byLanguage = new SortedDictionary<string, string>(StringComparer.Ordinal);
                byLanguage[t.LanguageCode] = t.Value;
            }
            return snapshot;
        }

        public string? OfficialIdOf(Type table, object row) => (string?)Mappings[table].FindColumn("OfficialId").GetValue(row);

        public IEnumerable<int> JoinedIds(OwnedJoin join, int ownerId) => _joins[join.Type][ownerId];

        /// <summary>Texte d'une clé de traduction dans languageCode, repli sur l'anglais.</summary>
        public string Text(string? key, string languageCode)
        {
            var byLanguage = TranslationsOf(key ?? string.Empty);
            return byLanguage.TryGetValue(languageCode, out var text) || byLanguage.TryGetValue("en", out text) ? text : string.Empty;
        }

        /// <summary>Valeur d'une colonne telle qu'un joueur la lit : texte traduit, nom de la ligne visée,
        /// liste de noms, ou valeur brute.</summary>
        public string Display(CatalogTable table, object row, string column, string languageCode)
        {
            var value = Mappings[table.Type].FindColumn(column).GetValue(row);
            if (table.TranslationColumns.Contains(column)) return Text((string?)value, languageCode);
            if (table.ForeignKeys.FirstOrDefault(f => f.Column == column) is { Target: { } target })
                return NameOf(target, value as int?, languageCode);
            if (table.CsvForeignKeys.FirstOrDefault(f => f.Column == column) is { Target: { } csvTarget })
                return string.Join(", ", ((string?)value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => NameOf(csvTarget, int.Parse(id), languageCode)).Order(StringComparer.CurrentCulture));
            return value switch
            {
                null => string.Empty,
                bool flag => flag ? "✓" : "✗",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }

        public string DisplayJoin(OwnedJoin join, int ownerId, string languageCode) =>
            string.Join(", ", JoinedIds(join, ownerId).Select(id => NameOf(join.Other, id, languageCode)).Order(StringComparer.CurrentCulture));

        private string NameOf(Type table, int? id, string languageCode) =>
            id is { } value && value != 0 && Rows[table].TryGetValue(value, out var row)
                ? Text((string?)Mappings[table].FindColumn("NameKey").GetValue(row), languageCode)
                : string.Empty;

        public IReadOnlyDictionary<string, string> TranslationsOf(string key) =>
            _translations.TryGetValue(key, out var byLanguage) ? byLanguage : new SortedDictionary<string, string>();

        /// <summary>Empreinte d'une ligne indépendante de la base : Id et clés de traduction remplacés par ce
        /// qu'ils désignent (OfficialId, textes), jointures possédées incluses. Source/OfficialId exclus.</summary>
        public string ComputeHash(CatalogTable table, object row)
        {
            var mapping = Mappings[table.Type];
            var text = new StringBuilder();
            foreach (var column in mapping.Columns.Where(c => !c.IsPK && c.Name is not ("Source" or "OfficialId")).OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                var value = column.GetValue(row);
                text.Append(column.Name).Append('=');
                if (table.TranslationColumns.Contains(column.Name))
                    foreach (var (language, translation) in TranslationsOf((string?)value ?? string.Empty))
                        text.Append(language).Append(':').Append(translation).Append('\u001e');
                else if (table.ForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } target })
                    text.Append(Reference(target, value as int?));
                else if (table.CsvForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } csvTarget })
                    text.AppendJoin(',', ((string?)value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => Reference(csvTarget, int.Parse(id))).Order(StringComparer.Ordinal));
                else
                    text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                text.Append('\u001f');
            }
            var id = (int)mapping.PK.GetValue(row);
            foreach (var join in OfficialContentSchema.Joins.Where(j => j.Owner == table.Type))
                text.Append(join.Type.Name).Append('=')
                    .AppendJoin(',', JoinedIds(join, id).Select(other => Reference(join.Other, other)).Order(StringComparer.Ordinal))
                    .Append('\u001f');
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }

        /// <summary>OfficialId de la ligne visée ; "#id" pour une ligne personnalisée (jamais égale à une
        /// référence de la seed, donc toujours vue comme une différence).</summary>
        private string Reference(Type target, int? id) => id is null or 0
            ? string.Empty
            : Rows[target].TryGetValue(id.Value, out var row) ? OfficialIdOf(target, row) ?? $"#{id}" : $"#missing{id}";
    }
}
