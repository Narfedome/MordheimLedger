using System.Text.Encodings.Web;
using System.Text.Json;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using SQLite;

namespace MordheimLedgerApp.Core.Exchange;

/// <param name="EmbedOfficialContent">Recopier aussi en entier le contenu officiel référencé (secours si le
/// destinataire a une version plus ancienne de l'appli). Faux pour un QR code : l'officiel n'y voyage que par
/// son OfficialId.</param>
/// <param name="IncludePhotos">Joindre les photos des guerriers (archive .zip seulement).</param>
public sealed record ExchangeExportOptions(bool EmbedOfficialContent = true, bool IncludePhotos = true);

/// <summary>Une ligne du catalogue à partager : type d'entité (ex. typeof(EquipmentItemEntity)) et Id local.</summary>
public sealed record CatalogItemRef(Type EntityType, int Id);

public enum CatalogImportStatus
{
    /// <summary>Absente de cet appareil : sera créée.</summary>
    New,

    /// <summary>Contenu officiel déjà présent : la version de cet appareil sert telle quelle.</summary>
    Existing,

    /// <summary>Déjà présente, identique.</summary>
    Identical,

    /// <summary>Déjà présente (même entrée Modifiée ou Personnalisée), mais différente : au joueur de choisir.</summary>
    Conflict,
}

/// <param name="Differences">Pour un conflit : les champs qui diffèrent, déjà en texte (comme ContentConflict) -
/// Official = la version reçue, Mine = celle de cet appareil. Vide sinon.</param>
public sealed record CatalogImportItem(string Table, string Key, string Name, ContentSource Source, CatalogImportStatus Status, bool IsShared,
    IReadOnlyList<ContentFieldDifference> Differences)
{
    public string EntryId => ExchangePackage.EntryId(Table, Key);
}

/// <param name="ArchetypeName">Type de bande, dans la langue demandée.</param>
/// <param name="WarriorCount">Figurines (effectif des groupes compris), hors morts et retraités.</param>
/// <param name="ExistingWarbandId">Bande de cet appareil qui porte le même identifiant d'échange (déjà importée,
/// ou exportée d'ici), sinon null.</param>
public sealed record WarbandImportItem(string? ShareId, string Name, string ArchetypeName, int WarriorCount,
    int? ExistingWarbandId, string? ExistingName);

/// <param name="MissingReferences">Références (« Table|Key ») introuvables, ni dans le paquet ni sur cet
/// appareil : l'import est impossible (contenu officiel d'une version plus récente, partagé sans secours).</param>
public sealed record ImportPreview(IReadOnlyList<CatalogImportItem> Catalog, IReadOnlyList<WarbandImportItem> Warbands,
    IReadOnlyList<string> MissingReferences)
{
    public bool CanImport => MissingReferences.Count == 0;
}

public enum WarbandImportMode
{
    /// <summary>Ajouter une nouvelle bande (nouvel identifiant d'échange si la bande existe déjà ici).</summary>
    NewCopy,

    /// <summary>Remplacer la bande existante (même identifiant d'échange) par celle reçue.</summary>
    Replace,
}

/// <summary>Choix du joueur avant l'import. Par défaut : garder sa version de chaque entrée en conflit, et
/// ajouter une copie d'une bande déjà présente plutôt que l'écraser.</summary>
public sealed class ImportDecisions
{
    /// <summary>Entrées en conflit (CatalogImportItem.EntryId) dont la version reçue remplace la sienne.</summary>
    public HashSet<string> TakeIncoming { get; } = new(StringComparer.Ordinal);

    /// <summary>Par identifiant d'échange de bande.</summary>
    public Dictionary<string, WarbandImportMode> Warbands { get; } = new(StringComparer.Ordinal);
}

public sealed record ImportResult(int CatalogCreated, int CatalogReplaced, IReadOnlyList<int> WarbandIds);

/// <summary>Export et import de bandes jouées et d'entrées du Codex entre appareils (v2, lot 0 - socle du partage
/// par fichier, du QR code et du lobby). Pas d'UI ni de jet ici : l'appli montre l'aperçu (PreviewAsync), recueille
/// les choix (ImportDecisions) puis importe.
///
/// Identité d'une ligne du catalogue d'un appareil à l'autre : son OfficialId (contenu officiel, Officiel ou
/// Modifié), sinon son identifiant d'échange (contenu Personnalisé, GUID attribué au premier export puis gardé par
/// qui l'importe - ShareIdentityEntity). Une bande est reconnue par son propre identifiant d'échange. Le catalogue est parcouru par OfficialContentSchema,
/// les parties jouées par PlayedDataSchema : une nouvelle table ou colonne de référence s'y déclare, rien ici.</summary>
public sealed class ExchangeService
{
    /// <summary>Colonnes du catalogue jamais transportées : identité (portée par la clé), image locale.</summary>
    private static readonly string[] NotTransported = ["Source", "OfficialId", "ImagePath"];

    internal static readonly JsonSerializerOptions ValueOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly AppDatabase _db;
    private readonly WarriorPhotoStore? _photos;

    public ExchangeService(AppDatabase db, WarriorPhotoStore? photos = null)
    {
        _db = db;
        _photos = photos;
    }

    private SQLiteAsyncConnection Connection => _db.Connection;

    // --- Export ---------------------------------------------------------------------------------------------

    public async Task<ExchangePackage> ExportWarbandsAsync(IEnumerable<int> warbandIds, ExchangeExportOptions? options = null)
    {
        await _db.Initialization;
        options ??= new ExchangeExportOptions();
        var package = new ExchangePackage { ContentFingerprint = SeedContent.Fingerprint };
        // Transaction : les identifiants d'échange attribués au passage s'écrivent tous ou pas du tout.
        await _db.RunInTransactionAsync(async () =>
        {
            var identities = await ShareIdentities.LoadAsync(Connection);
            var exporter = new CatalogExporter(await AppDatabase.ContentSnapshot.LoadAsync(Connection), identities, options);
            foreach (var id in warbandIds)
            {
                var warband = await Connection.FindAsync<WarbandEntity>(id) ?? throw new ArgumentException($"Bande {id} introuvable", nameof(warbandIds));
                var row = await ExportPlayedAsync(PlayedDataSchema.Warband, warband, exporter, options, package);
                row.ShareId = identities.Of(typeof(WarbandEntity), id) ?? identities.Add(typeof(WarbandEntity), id, NewShareId());
                package.Warbands.Add(row);
            }
            exporter.Finish(package);
            await identities.SaveAsync(Connection);
        });
        return package;
    }

    /// <summary>Entrées du Codex choisies par le joueur. Un type de bande emmène ses types de guerrier et ses
    /// listes d'équipement (sans eux, il ne sert à rien chez le destinataire).</summary>
    public async Task<ExchangePackage> ExportCatalogAsync(IEnumerable<CatalogItemRef> items, ExchangeExportOptions? options = null)
    {
        await _db.Initialization;
        options ??= new ExchangeExportOptions(IncludePhotos: false);
        var package = new ExchangePackage { ContentFingerprint = SeedContent.Fingerprint };
        await _db.RunInTransactionAsync(async () =>
        {
            var snapshot = await AppDatabase.ContentSnapshot.LoadAsync(Connection);
            var identities = await ShareIdentities.LoadAsync(Connection);
            var exporter = new CatalogExporter(snapshot, identities, options);
            foreach (var item in items)
            {
                var table = CatalogTableOf(item.EntityType);
                var key = exporter.Share(table.Type, item.Id);
                package.SharedEntries.Add(ExchangePackage.EntryId(table.Type.Name, key));
                if (table.Type != typeof(WarbandArchetypeEntity)) continue;
                foreach (var childType in new[] { typeof(WarriorArchetypeEntity), typeof(EquipmentListEntity) })
                {
                    var parentColumn = snapshot.Mappings[childType].FindColumn("WarbandArchetypeId");
                    foreach (var (childId, child) in snapshot.Rows[childType])
                        if (parentColumn.GetValue(child) is int parent && parent == item.Id)
                            exporter.Reference(childType, childId);
                }
            }
            exporter.Finish(package);
            await identities.SaveAsync(Connection);
        });
        return package;
    }

    private async Task<PlayedRow> ExportPlayedAsync(PlayedTable table, object row, CatalogExporter exporter,
        ExchangeExportOptions options, ExchangePackage package)
    {
        var mapping = await Connection.GetMappingAsync(table.Type);
        var result = new PlayedRow();
        foreach (var column in mapping.Columns)
        {
            if (column.IsPK || column.Name == table.ParentColumn || table.Excluded.Contains(column.Name)) continue;
            var value = column.GetValue(row);
            if (table.References.FirstOrDefault(r => r.Column == column.Name) is { Target: { } target })
            {
                if (value is int id && id != 0) result.References[column.Name] = exporter.Reference(target, id);
                continue;
            }
            if (table.Type == typeof(WarriorEntity) && column.Name == nameof(WarriorEntity.PhotoPath))
                value = await ExportPhotoAsync((string?)value, options, package);
            result.Values[column.Name] = Serialize(value, PropertyType(table.Type, column));
        }

        var parentId = (int)mapping.PK.GetValue(row);
        foreach (var child in PlayedDataSchema.ChildrenOf(table.Type))
        {
            var childMapping = await Connection.GetMappingAsync(child.Type);
            var rows = await Connection.QueryAsync(childMapping,
                $"SELECT * FROM \"{childMapping.TableName}\" WHERE \"{child.ParentColumn}\" = ? ORDER BY Id", parentId);
            if (rows.Count == 0) continue;
            var exported = new List<PlayedRow>();
            foreach (var childRow in rows)
                exported.Add(await ExportPlayedAsync(child, childRow, exporter, options, package));
            result.Children[child.Type.Name] = exported;
        }
        return result;
    }

    private async Task<string?> ExportPhotoAsync(string? relativePath, ExchangeExportOptions options, ExchangePackage package)
    {
        if (!options.IncludePhotos || _photos?.GetExistingFullPath(relativePath) is not { } fullPath) return null;
        var name = $"photos/{Guid.NewGuid():N}{Path.GetExtension(fullPath).ToLowerInvariant()}";
        package.Photos[name] = await File.ReadAllBytesAsync(fullPath);
        return name;
    }

    /// <summary>Parcourt le catalogue de proche en proche depuis ce que les bandes ou le joueur désignent.</summary>
    private sealed class CatalogExporter(AppDatabase.ContentSnapshot snapshot, ShareIdentities identities, ExchangeExportOptions options)
    {
        private readonly Queue<(Type Type, int Id)> _pending = new();
        private readonly HashSet<string> _queued = new(StringComparer.Ordinal);

        /// <summary>Clé de la ligne, embarquée dans le paquet si elle doit l'être.</summary>
        public string Reference(Type type, int id) => Include(type, id, force: false);

        /// <summary>Ligne choisie par le joueur : toujours embarquée, même officielle.</summary>
        public string Share(Type type, int id) => Include(type, id, force: true);

        private string Include(Type type, int id, bool force)
        {
            if (!snapshot.Rows[type].TryGetValue(id, out var row))
                throw new InvalidOperationException($"{type.Name}#{id} introuvable");
            var key = KeyOf(type, id, row);
            var source = (ContentSource)snapshot.Mappings[type].FindColumn("Source").GetValue(row)!;
            if ((force || options.EmbedOfficialContent || source != ContentSource.Official)
                && _queued.Add(ExchangePackage.EntryId(type.Name, key)))
                _pending.Enqueue((type, id));
            return key;
        }

        private string KeyOf(Type type, int id, object row) =>
            LocalKey(snapshot, identities, type, id, row) ?? CatalogEntry.SharedPrefix + identities.Add(type, id, NewShareId());

        public void Finish(ExchangePackage package)
        {
            while (_pending.TryDequeue(out var next))
            {
                var table = CatalogTableOf(next.Type);
                var row = snapshot.Rows[next.Type][next.Id];
                package.Catalog.Add(BuildEntry(snapshot, table, row, KeyOf(next.Type, next.Id, row), Reference));
            }
        }
    }

    /// <summary>Clé d'une ligne du catalogue sur cet appareil, null si elle n'en a pas encore (Personnalisée
    /// jamais exportée).</summary>
    private static string? LocalKey(AppDatabase.ContentSnapshot snapshot, ShareIdentities identities, Type type, int id, object row)
    {
        if (snapshot.OfficialIdOf(type, row) is { } officialId) return CatalogEntry.OfficialPrefix + officialId;
        return identities.Of(type, id) is { } shareId ? CatalogEntry.SharedPrefix + shareId : null;
    }

    /// <summary>Identifiants d'échange (ShareIdentityEntity) en mémoire ; les nouveaux s'écrivent à SaveAsync.</summary>
    private sealed class ShareIdentities
    {
        private readonly Dictionary<(string Table, int LocalId), string> _byRow = new();
        private readonly Dictionary<(string Table, string ShareId), int> _byShareId = new();
        private readonly List<ShareIdentityEntity> _added = [];

        public static async Task<ShareIdentities> LoadAsync(SQLiteAsyncConnection connection)
        {
            var identities = new ShareIdentities();
            foreach (var row in await connection.Table<ShareIdentityEntity>().ToListAsync())
                identities.Remember(row.TableName, row.LocalId, row.ShareId);
            return identities;
        }

        private void Remember(string table, int localId, string shareId)
        {
            _byRow[(table, localId)] = shareId;
            _byShareId[(table, shareId)] = localId;
        }

        public string? Of(Type type, int localId) => _byRow.GetValueOrDefault((type.Name, localId));

        /// <summary>Ligne locale qui porte cet identifiant (peut avoir été supprimée depuis : à vérifier).</summary>
        public int? Find(Type type, string shareId) => _byShareId.TryGetValue((type.Name, shareId), out var id) ? id : null;

        public string Add(Type type, int localId, string shareId)
        {
            Remember(type.Name, localId, shareId);
            _added.Add(new ShareIdentityEntity { TableName = type.Name, LocalId = localId, ShareId = shareId });
            return shareId;
        }

        public async Task SaveAsync(SQLiteAsyncConnection connection)
        {
            foreach (var row in _added)
            {
                await connection.ExecuteAsync("DELETE FROM ShareIdentityEntity WHERE TableName = ? AND LocalId = ?", row.TableName, row.LocalId);
                await connection.InsertAsync(row);
            }
            _added.Clear();
        }
    }

    /// <summary>Ligne du catalogue telle que le paquet la transporte. Sert aussi à comparer une entrée reçue à la
    /// version locale (même construction des deux côtés).</summary>
    private static CatalogEntry BuildEntry(AppDatabase.ContentSnapshot snapshot, CatalogTable table, object row, string key,
        Func<Type, int, string> reference)
    {
        var mapping = snapshot.Mappings[table.Type];
        var entry = new CatalogEntry
        {
            Table = table.Type.Name,
            Key = key,
            Source = (ContentSource)mapping.FindColumn("Source").GetValue(row)!,
        };
        foreach (var column in mapping.Columns)
        {
            if (column.IsPK || NotTransported.Contains(column.Name)) continue;
            var value = column.GetValue(row);
            if (table.TranslationColumns.Contains(column.Name))
            {
                var texts = snapshot.TranslationsOf(value as string ?? string.Empty);
                if (texts.Count > 0)
                    entry.Translations[column.Name] = new SortedDictionary<string, string>(texts.ToDictionary(t => t.Key, t => t.Value), StringComparer.Ordinal);
            }
            else if (table.ForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } target })
            {
                if (value is int id && id != 0) entry.References[column.Name] = reference(target, id);
            }
            else if (table.CsvForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } csvTarget })
            {
                var keys = ParseIds(value as string).Select(id => reference(csvTarget, id)).Order(StringComparer.Ordinal).ToList();
                if (keys.Count > 0) entry.ListReferences[column.Name] = keys;
            }
            else
            {
                entry.Values[column.Name] = Serialize(value, PropertyType(table.Type, column));
            }
        }
        var rowId = (int)mapping.PK.GetValue(row);
        foreach (var join in OfficialContentSchema.Joins.Where(j => j.Owner == table.Type))
        {
            var keys = snapshot.JoinedIds(join, rowId).Select(other => reference(join.Other, other)).Order(StringComparer.Ordinal).ToList();
            if (keys.Count > 0) entry.Joins[join.Type.Name] = keys;
        }
        return entry;
    }

    // --- Import ---------------------------------------------------------------------------------------------

    /// <summary>Ce que l'import ferait, sans rien écrire. languageCode : langue des noms affichés.</summary>
    public async Task<ImportPreview> PreviewAsync(ExchangePackage package, string languageCode = "en")
    {
        await _db.Initialization;
        var plan = await ImportPlan.CreateAsync(Connection, package);
        return new ImportPreview(
            plan.Items.Select(i => new CatalogImportItem(i.Entry.Table, i.Entry.Key, NameOf(i.Entry, languageCode), i.Entry.Source, i.Status,
                package.SharedEntries.Contains(ExchangePackage.EntryId(i.Entry.Table, i.Entry.Key)),
                i.Status == CatalogImportStatus.Conflict ? plan.Differences(i.Table, i.Entry, i.LocalId!.Value, languageCode) : [])).ToList(),
            plan.Warbands.Select(w => new WarbandImportItem(w.ShareId,
                w.Row.Values.TryGetValue("Name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() ?? "" : "",
                w.Row.References.TryGetValue("WarbandArchetypeId", out var archetype) ? plan.NameOfKey(typeof(WarbandArchetypeEntity), archetype, languageCode) : "",
                WarriorCount(w.Row), w.Existing?.Id, w.Existing?.Name)).ToList(),
            plan.Missing);
    }

    private static int WarriorCount(PlayedRow warband) =>
        (warband.Children.GetValueOrDefault(nameof(WarriorEntity)) ?? [])
            .Where(w => !w.Values.TryGetValue(nameof(WarriorEntity.Status), out var status) || status.ValueKind != JsonValueKind.Number
                        || (WarriorStatus)status.GetInt32() is not (WarriorStatus.Dead or WarriorStatus.Retired))
            .Sum(w => w.Values.TryGetValue(nameof(WarriorEntity.HeadCount), out var count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : 1);

    /// <summary>Importe le paquet en une transaction (tout ou rien). Lève ExchangeFormatException si une référence
    /// est introuvable (voir ImportPreview.MissingReferences).</summary>
    public async Task<ImportResult> ImportAsync(ExchangePackage package, ImportDecisions? decisions = null)
    {
        await _db.Initialization;
        decisions ??= new ImportDecisions();
        var savedPhotos = new List<string>();
        ImportResult result = null!;
        try
        {
            await _db.RunInTransactionAsync(async () =>
            {
                var plan = await ImportPlan.CreateAsync(Connection, package);
                if (plan.Missing.Count > 0)
                    throw new ExchangeFormatException("Contenu introuvable : " + string.Join(", ", plan.Missing));

                var (created, replaced) = await ImportCatalogAsync(plan, decisions);
                var warbandIds = new List<int>();
                foreach (var warband in plan.Warbands)
                {
                    var mode = warband.ShareId is { } shareId && decisions.Warbands.TryGetValue(shareId, out var chosen) ? chosen : WarbandImportMode.NewCopy;
                    var shareIdToKeep = string.IsNullOrEmpty(warband.ShareId) ? NewShareId() : warband.ShareId;
                    if (warband.Existing is { } existing)
                    {
                        if (mode == WarbandImportMode.Replace) await DeleteWarbandAsync(existing.Id);
                        else shareIdToKeep = NewShareId();
                    }
                    var id = await InsertPlayedAsync(plan, PlayedDataSchema.Warband, warband.Row, null, package, savedPhotos);
                    plan.Identities.Add(typeof(WarbandEntity), id, shareIdToKeep);
                    warbandIds.Add(id);
                }
                await plan.Identities.SaveAsync(Connection);
                _db.InvalidateCachedTable<WarbandEntity>();
                result = new ImportResult(created, replaced, warbandIds);
            });
        }
        catch
        {
            foreach (var photo in savedPhotos) _photos?.Delete(photo);
            throw;
        }
        return result;
    }

    private async Task<(int Created, int Replaced)> ImportCatalogAsync(ImportPlan plan, ImportDecisions decisions)
    {
        // 1. Chaque entrée reçoit son Id local ; les nouvelles sont d'abord insérées à vide (références à 0/null)
        //    pour que toutes aient un Id avant de traduire la moindre référence - comme la synchro officielle.
        var toWrite = new List<(CatalogTable Table, CatalogEntry Entry, object Row)>();
        int created = 0, replaced = 0;
        foreach (var item in plan.Items)
        {
            var entryId = ExchangePackage.EntryId(item.Entry.Table, item.Entry.Key);
            if (item.LocalId is { } localId)
            {
                plan.LocalIds[entryId] = localId;
                if (item.Status == CatalogImportStatus.Conflict && decisions.TakeIncoming.Contains(entryId))
                {
                    toWrite.Add((item.Table, item.Entry, plan.Snapshot.Rows[item.Table.Type][localId]));
                    replaced++;
                }
                continue;
            }

            var mapping = plan.Snapshot.Mappings[item.Table.Type];
            var row = Activator.CreateInstance(item.Table.Type)!;
            mapping.FindColumn("Source").SetValue(row, item.Entry.Source);
            var isOfficial = item.Entry.Key.StartsWith(CatalogEntry.OfficialPrefix, StringComparison.Ordinal);
            if (isOfficial) mapping.FindColumn("OfficialId").SetValue(row, item.Entry.Key[CatalogEntry.OfficialPrefix.Length..]);
            await Connection.InsertAsync(row);
            var newId = (int)mapping.PK.GetValue(row);
            if (!isOfficial) plan.Identities.Add(item.Table.Type, newId, item.Entry.Key[CatalogEntry.SharedPrefix.Length..]);
            plan.LocalIds[entryId] = newId;
            toWrite.Add((item.Table, item.Entry, row));
            created++;
        }

        // 2. Recopie complète des nouvelles et des versions reçues retenues.
        foreach (var (table, entry, row) in toWrite)
        {
            var mapping = plan.Snapshot.Mappings[table.Type];
            foreach (var column in mapping.Columns)
            {
                if (column.IsPK || NotTransported.Contains(column.Name)) continue;
                if (table.TranslationColumns.Contains(column.Name))
                {
                    var key = await WriteTranslationsAsync(entry.Translations.GetValueOrDefault(column.Name), column.GetValue(row) as string);
                    column.SetValue(row, key ?? (column.Name == "NameKey" ? string.Empty : null));
                }
                else if (table.ForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } target })
                {
                    column.SetValue(row, entry.References.TryGetValue(column.Name, out var refKey)
                        ? plan.MapReference(target, refKey)
                        : EmptyReference(table.Type, column));
                }
                else if (table.CsvForeignKeys.FirstOrDefault(f => f.Column == column.Name) is { Target: { } csvTarget })
                {
                    column.SetValue(row, entry.ListReferences.TryGetValue(column.Name, out var keys) && keys.Count > 0
                        ? string.Join(',', keys.Select(k => plan.MapReference(csvTarget, k)))
                        : null);
                }
                else if (entry.Values.TryGetValue(column.Name, out var value))
                {
                    column.SetValue(row, Deserialize(value, PropertyType(table.Type, column)));
                }
            }
            mapping.FindColumn("Source").SetValue(row, entry.Source);
            await Connection.UpdateAsync(row);

            var rowId = (int)mapping.PK.GetValue(row);
            foreach (var join in OfficialContentSchema.Joins.Where(j => j.Owner == table.Type))
            {
                var joinMapping = await Connection.GetMappingAsync(join.Type);
                await Connection.ExecuteAsync($"DELETE FROM \"{joinMapping.TableName}\" WHERE \"{join.OwnerColumn}\" = ?", rowId);
                _db.InvalidateCachedTable(join.Type);
                foreach (var other in entry.Joins.GetValueOrDefault(join.Type.Name) ?? [])
                {
                    var joinRow = Activator.CreateInstance(join.Type)!;
                    joinMapping.FindColumn(join.OwnerColumn).SetValue(joinRow, rowId);
                    joinMapping.FindColumn(join.OtherColumn).SetValue(joinRow, plan.MapReference(join.Other, other));
                    await Connection.InsertAsync(joinRow);
                }
            }
        }
        return (created, replaced);
    }

    /// <summary>Remplace les textes de localKey (créée si besoin). Null si aucun texte reçu.</summary>
    private async Task<string?> WriteTranslationsAsync(IReadOnlyDictionary<string, string>? texts, string? localKey)
    {
        if (!string.IsNullOrEmpty(localKey))
        {
            await Connection.ExecuteAsync("DELETE FROM TranslationEntity WHERE Key = ?", localKey);
            _db.InvalidateCachedTable<TranslationEntity>();
        }
        if (texts is null || texts.Count == 0) return null;
        var key = string.IsNullOrEmpty(localKey) ? Guid.NewGuid().ToString("N") : localKey;
        foreach (var (languageCode, value) in texts)
            await Connection.InsertAsync(new TranslationEntity { Key = key, LanguageCode = languageCode, Value = value });
        return key;
    }

    private async Task<int> InsertPlayedAsync(ImportPlan plan, PlayedTable table, PlayedRow source, int? parentId,
        ExchangePackage package, List<string> savedPhotos)
    {
        var mapping = await Connection.GetMappingAsync(table.Type);
        var row = Activator.CreateInstance(table.Type)!;
        foreach (var column in mapping.Columns)
        {
            if (column.IsPK || table.Excluded.Contains(column.Name)) continue;
            if (column.Name == table.ParentColumn)
            {
                column.SetValue(row, parentId);
            }
            else if (table.References.FirstOrDefault(r => r.Column == column.Name) is { Target: { } target })
            {
                column.SetValue(row, source.References.TryGetValue(column.Name, out var key)
                    ? plan.MapReference(target, key)
                    : EmptyReference(table.Type, column));
            }
            else if (source.Values.TryGetValue(column.Name, out var value))
            {
                column.SetValue(row, Deserialize(value, PropertyType(table.Type, column)));
            }
        }
        if (row is WarriorEntity warrior)
            warrior.PhotoPath = await ImportPhotoAsync(warrior.PhotoPath, package, savedPhotos);

        await Connection.InsertAsync(row);
        var id = (int)mapping.PK.GetValue(row);
        foreach (var (childName, children) in source.Children)
        {
            var child = PlayedDataSchema.ByName(childName);
            if (child.Parent != table.Type) throw new ExchangeFormatException($"{childName} ne peut pas être rattachée à {table.Type.Name}");
            foreach (var childRow in children)
                await InsertPlayedAsync(plan, child, childRow, id, package, savedPhotos);
        }
        return id;
    }

    private async Task<string?> ImportPhotoAsync(string? packagePath, ExchangePackage package, List<string> savedPhotos)
    {
        if (_photos is null || packagePath is null || !package.Photos.TryGetValue(packagePath, out var bytes)) return null;
        using var stream = new MemoryStream(bytes, writable: false);
        var saved = await _photos.SaveAsync(stream, Path.GetExtension(packagePath));
        savedPhotos.Add(saved);
        return saved;
    }

    /// <summary>Même suppression que WarbandService.DeleteWarbandAsync (guerriers, leurs lignes et photos, puis
    /// les lignes de la bande) - ici dans la transaction de l'import.</summary>
    private async Task DeleteWarbandAsync(int warbandId)
    {
        var warriors = await Connection.QueryAsync<WarriorEntity>("SELECT * FROM WarriorEntity WHERE WarbandId = ?", warbandId);
        foreach (var warrior in warriors)
        {
            foreach (var table in AppDatabase.WarriorChildTables)
                await Connection.ExecuteAsync($"DELETE FROM {table} WHERE WarriorId = ?", warrior.Id);
            await Connection.DeleteAsync<WarriorEntity>(warrior.Id);
            if (_photos is { } photos) _db.RunAfterCommit(() => photos.Delete(warrior.PhotoPath));
        }
        foreach (var table in AppDatabase.WarbandChildTables)
            await Connection.ExecuteAsync($"DELETE FROM {table} WHERE WarbandId = ?", warbandId);
        await Connection.DeleteAsync<WarbandEntity>(warbandId);
        await Connection.ExecuteAsync("DELETE FROM ShareIdentityEntity WHERE TableName = ? AND LocalId = ?", nameof(WarbandEntity), warbandId);
    }

    /// <summary>Correspondance du paquet avec cet appareil, calculée sans rien écrire.</summary>
    private sealed class ImportPlan
    {
        public required AppDatabase.ContentSnapshot Snapshot { get; init; }
        public required ShareIdentities Identities { get; init; }
        public Dictionary<string, CatalogEntry> Entries { get; } = new(StringComparer.Ordinal);
        public List<(CatalogTable Table, CatalogEntry Entry, int? LocalId, CatalogImportStatus Status)> Items { get; } = [];
        public List<(PlayedRow Row, string? ShareId, WarbandEntity? Existing)> Warbands { get; } = [];
        public List<string> Missing { get; } = [];

        /// <summary>Id local de chaque entrée du paquet (rempli pendant l'import).</summary>
        public Dictionary<string, int> LocalIds { get; } = new(StringComparer.Ordinal);

        public static async Task<ImportPlan> CreateAsync(SQLiteAsyncConnection connection, ExchangePackage package)
        {
            ExchangeArchive.Validate(package);
            var plan = new ImportPlan
            {
                Snapshot = await AppDatabase.ContentSnapshot.LoadAsync(connection),
                Identities = await ShareIdentities.LoadAsync(connection),
            };

            foreach (var entry in package.Catalog)
            {
                var table = CatalogTableByName(entry.Table);
                if (!entry.Key.StartsWith(CatalogEntry.OfficialPrefix, StringComparison.Ordinal)
                    && !entry.Key.StartsWith(CatalogEntry.SharedPrefix, StringComparison.Ordinal))
                    throw new ExchangeFormatException($"Clé invalide : {entry.Key}");
                if (!plan.Entries.TryAdd(ExchangePackage.EntryId(entry.Table, entry.Key), entry))
                    throw new ExchangeFormatException($"Entrée en double : {entry.Table}|{entry.Key}");

                var localId = plan.Resolve(table.Type, entry.Key);
                var status = localId switch
                {
                    null => CatalogImportStatus.New,
                    _ when entry.Source == ContentSource.Official => CatalogImportStatus.Existing,
                    { } id => Canonical(entry) == Canonical(plan.LocalEntry(table, id, entry.Key))
                        ? CatalogImportStatus.Identical
                        : CatalogImportStatus.Conflict,
                };
                plan.Items.Add((table, entry, localId, status));
            }

            foreach (var entry in package.Catalog)
            {
                var table = CatalogTableByName(entry.Table);
                foreach (var (column, key) in entry.References)
                    plan.Check(table.ForeignKeys.FirstOrDefault(f => f.Column == column).Target, key);
                foreach (var (column, keys) in entry.ListReferences)
                    foreach (var key in keys) plan.Check(table.CsvForeignKeys.FirstOrDefault(f => f.Column == column).Target, key);
                foreach (var (joinName, keys) in entry.Joins)
                {
                    var join = OfficialContentSchema.Joins.FirstOrDefault(j => j.Type.Name == joinName && j.Owner == table.Type)
                        ?? throw new ExchangeFormatException($"Jointure inconnue : {joinName}");
                    foreach (var key in keys) plan.Check(join.Other, key);
                }
            }

            foreach (var warband in package.Warbands)
            {
                plan.CheckPlayed(PlayedDataSchema.Warband, warband);
                var existing = !string.IsNullOrEmpty(warband.ShareId) && plan.Identities.Find(typeof(WarbandEntity), warband.ShareId) is { } localId
                    ? await connection.FindAsync<WarbandEntity>(localId)
                    : null;
                plan.Warbands.Add((warband, warband.ShareId, existing));
            }
            return plan;
        }

        public int? Resolve(Type type, string key)
        {
            if (key.StartsWith(CatalogEntry.OfficialPrefix, StringComparison.Ordinal))
                return Snapshot.IdByOfficialId[type].TryGetValue(key[CatalogEntry.OfficialPrefix.Length..], out var id) ? id : null;
            if (key.StartsWith(CatalogEntry.SharedPrefix, StringComparison.Ordinal)
                && Identities.Find(type, key[CatalogEntry.SharedPrefix.Length..]) is { } shared
                && Snapshot.Rows[type].ContainsKey(shared))
                return shared;
            return null;
        }

        public int MapReference(Type target, string key) =>
            LocalIds.TryGetValue(ExchangePackage.EntryId(target.Name, key), out var id) ? id
            : Resolve(target, key) ?? throw new ExchangeFormatException($"Contenu introuvable : {target.Name}|{key}");

        private void Check(Type? target, string key)
        {
            if (target is null) throw new ExchangeFormatException($"Référence inconnue : {key}");
            var entryId = ExchangePackage.EntryId(target.Name, key);
            if (!Entries.ContainsKey(entryId) && Resolve(target, key) is null && !Missing.Contains(entryId)) Missing.Add(entryId);
        }

        private void CheckPlayed(PlayedTable table, PlayedRow row)
        {
            foreach (var (column, key) in row.References)
                Check(table.References.FirstOrDefault(r => r.Column == column).Target, key);
            foreach (var (childName, children) in row.Children)
            {
                var child = PlayedDataSchema.ByName(childName);
                foreach (var childRow in children) CheckPlayed(child, childRow);
            }
        }

        /// <summary>La version locale construite comme une entrée reçue ; une référence vers une ligne Personnalisée
        /// jamais exportée a une clé locale (« #Id ») qui ne peut égaler aucune clé reçue.</summary>
        private CatalogEntry LocalEntry(CatalogTable table, int id, string key) =>
            BuildEntry(Snapshot, table, Snapshot.Rows[table.Type][id], key,
                (type, refId) => Snapshot.Rows[type].TryGetValue(refId, out var row)
                    ? LocalKey(Snapshot, Identities, type, refId, row) ?? $"#{refId}"
                    : $"#missing{refId}");

        /// <summary>Nom d'une ligne désignée par sa clé : celle du paquet si elle y est, sinon celle de cet appareil
        /// (y compris une clé locale « #Id » d'une entrée Personnalisée jamais exportée).</summary>
        public string NameOfKey(Type type, string key, string languageCode)
        {
            if (Entries.TryGetValue(ExchangePackage.EntryId(type.Name, key), out var entry)) return NameOf(entry, languageCode);
            int? localId = key.StartsWith('#') && int.TryParse(key[1..], out var parsed) ? parsed : Resolve(type, key);
            return localId is { } id && Snapshot.Rows[type].TryGetValue(id, out var row)
                ? Snapshot.Text(Snapshot.Mappings[type].FindColumn("NameKey").GetValue(row) as string, languageCode)
                : string.Empty;
        }

        /// <summary>Champs où la version reçue et celle de cet appareil diffèrent, en texte (même présentation que les
        /// conflits du contenu officiel : texte traduit, nom des lignes visées, ✓/✗).</summary>
        public IReadOnlyList<ContentFieldDifference> Differences(CatalogTable table, CatalogEntry incoming, int localId, string languageCode)
        {
            var local = LocalEntry(table, localId, incoming.Key);
            var differences = new List<ContentFieldDifference>();
            void Compare(string field, string received, string mine)
            {
                if (received != mine) differences.Add(new ContentFieldDifference(field, received, mine));
            }
            string Names(Type type, IEnumerable<string>? keys) => string.Join(", ",
                (keys ?? []).Select(k => NameOfKey(type, k, languageCode)).Order(StringComparer.CurrentCulture));

            var mapping = Snapshot.Mappings[table.Type];
            foreach (var column in mapping.Columns.Where(c => !c.IsPK && !NotTransported.Contains(c.Name)))
            {
                var name = column.Name;
                if (table.TranslationColumns.Contains(name))
                    Compare(name, Text(incoming.Translations.GetValueOrDefault(name), languageCode), Text(local.Translations.GetValueOrDefault(name), languageCode));
                else if (table.ForeignKeys.FirstOrDefault(f => f.Column == name) is { Target: { } target })
                    Compare(name, incoming.References.TryGetValue(name, out var a) ? NameOfKey(target, a, languageCode) : "",
                        local.References.TryGetValue(name, out var b) ? NameOfKey(target, b, languageCode) : "");
                else if (table.CsvForeignKeys.FirstOrDefault(f => f.Column == name) is { Target: { } csvTarget })
                    Compare(name, Names(csvTarget, incoming.ListReferences.GetValueOrDefault(name)), Names(csvTarget, local.ListReferences.GetValueOrDefault(name)));
                else
                    Compare(name, ValueText(incoming.Values, name, table.Type, column), ValueText(local.Values, name, table.Type, column));
            }
            foreach (var join in OfficialContentSchema.Joins.Where(j => j.Owner == table.Type))
                Compare(join.Type.Name, Names(join.Other, incoming.Joins.GetValueOrDefault(join.Type.Name)), Names(join.Other, local.Joins.GetValueOrDefault(join.Type.Name)));
            return differences;
        }

        private static string Text(IReadOnlyDictionary<string, string>? texts, string languageCode) =>
            texts is null ? string.Empty : texts.GetValueOrDefault(languageCode) ?? texts.GetValueOrDefault("en") ?? texts.Values.FirstOrDefault() ?? string.Empty;

        private static string ValueText(IReadOnlyDictionary<string, JsonElement> values, string name, Type entity, TableMapping.Column column)
        {
            if (!values.TryGetValue(name, out var element)) return string.Empty;
            object? value;
            try
            {
                value = element.Deserialize(PropertyType(entity, column), ValueOptions);
            }
            catch (JsonException)
            {
                return element.GetRawText();
            }
            return value switch
            {
                null => string.Empty,
                bool flag => flag ? "✓" : "✗",
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }
    }

    private static string Canonical(CatalogEntry entry) => JsonSerializer.Serialize(
        new { entry.Values, entry.Translations, entry.References, entry.ListReferences, entry.Joins }, ValueOptions);

    // --- Outils -------------------------------------------------------------------------------------------

    private static string NewShareId() => Guid.NewGuid().ToString("N");

    private static string NameOf(CatalogEntry entry, string languageCode) =>
        entry.Translations.TryGetValue("NameKey", out var texts)
            ? texts.GetValueOrDefault(languageCode) ?? texts.GetValueOrDefault("en") ?? texts.Values.FirstOrDefault() ?? entry.Key
            : entry.Key;

    private static CatalogTable CatalogTableOf(Type type) => OfficialContentSchema.Tables.FirstOrDefault(t => t.Type == type)
        ?? throw new ArgumentException($"{type.Name} n'est pas une table du catalogue", nameof(type));

    private static CatalogTable CatalogTableByName(string name) => OfficialContentSchema.Tables.FirstOrDefault(t => t.Type.Name == name)
        ?? throw new ExchangeFormatException($"Table inconnue : {name}");

    private static IEnumerable<int> ParseIds(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);

    private static Type PropertyType(Type entity, TableMapping.Column column) => entity.GetProperty(column.PropertyName)!.PropertyType;

    private static object? EmptyReference(Type entity, TableMapping.Column column) =>
        Nullable.GetUnderlyingType(PropertyType(entity, column)) is null ? 0 : null;

    private static JsonElement Serialize(object? value, Type type) => JsonSerializer.SerializeToElement(value, type, ValueOptions);

    private static object? Deserialize(JsonElement value, Type type)
    {
        try
        {
            return value.Deserialize(type, ValueOptions);
        }
        catch (JsonException ex)
        {
            throw new ExchangeFormatException($"Valeur illisible : {value.GetRawText()}", ex);
        }
    }
}
