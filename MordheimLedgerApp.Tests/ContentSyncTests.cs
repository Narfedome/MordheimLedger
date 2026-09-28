using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>AppDatabase.SyncOfficialContentAsync : deux bases seedées indépendamment (Id et clés de
/// traduction différents, comme une vraie base installée face à une seed.db3 plus récente). La "seed" est
/// modifiée à la main pour simuler une nouvelle version du contenu officiel.</summary>
public class ContentSyncTests : IAsyncLifetime
{
    private readonly string _seedPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-seed-{Guid.NewGuid()}.db3");
    private readonly string _localPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-local-{Guid.NewGuid()}.db3");
    private AppDatabase _seed = null!;
    private AppDatabase _local = null!;
    private ILibraryService _library = null!;

    public async Task InitializeAsync()
    {
        _seed = new AppDatabase(_seedPath);
        _local = new AppDatabase(_localPath);
        _library = new LibraryService(_local);
        await Task.WhenAll(_seed.Initialization, _local.Initialization);
    }

    public async Task DisposeAsync()
    {
        await _seed.Connection.CloseAsync();
        await _local.Connection.CloseAsync();
        foreach (var path in new[] { _seedPath, _localPath })
            if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Ferme la base "seed" (la synchro l'ouvre en lecture seule) puis synchronise.</summary>
    private async Task<ContentSyncReport> SyncAsync()
    {
        await _seed.Connection.CloseAsync();
        return await _local.SyncOfficialContentAsync(_seedPath);
    }

    private static async Task<T> ByOfficialIdAsync<T>(AppDatabase db, string officialId) where T : new() =>
        (await db.Connection.QueryAsync<T>($"SELECT * FROM {typeof(T).Name} WHERE OfficialId = ?", officialId)).Single();

    private async Task<EquipmentItem> LocalItemAsync(string officialId, string languageCode = "en") =>
        (await _library.GetEquipmentItemsAsync(languageCode)).Single(i => i.OfficialId == officialId);

    private static async Task SetTranslationAsync(AppDatabase db, string key, string languageCode, string value) =>
        await db.Connection.ExecuteAsync("UPDATE TranslationEntity SET Value = ? WHERE Key = ? AND LanguageCode = ?", value, key, languageCode);

    [Fact]
    public async Task IdenticalSeed_ChangesNothing()
    {
        var report = await SyncAsync();

        Assert.False(report.HasChanges, report.ToString());
    }

    [Fact]
    public async Task OfficialRow_UpdatedInPlace_KeepsLocalId()
    {
        var before = await LocalItemAsync("equipment.axe");
        var seedAxe = await ByOfficialIdAsync<EquipmentItemEntity>(_seed, "equipment.axe");
        seedAxe.Cost = 99;
        await _seed.Connection.UpdateAsync(seedAxe);
        await SetTranslationAsync(_seed, seedAxe.NameKey, "fr", "Hache de test");

        var report = await SyncAsync();

        Assert.Equal(new ContentSyncReport(1, 0, 0, 0), report);
        var after = await LocalItemAsync("equipment.axe", "fr");
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(99, after.Cost);
        Assert.Equal("Hache de test", after.Name);
        Assert.Equal(ContentSource.Official, after.Source);
        Assert.Equal("Axe", (await LocalItemAsync("equipment.axe")).Name);
    }

    [Fact]
    public async Task NewRow_InsertedWithTranslationsAndLinks()
    {
        var nameKey = Guid.NewGuid().ToString("N");
        await _seed.Connection.InsertAsync(new TranslationEntity { Key = nameKey, LanguageCode = "en", Value = "Test Rule" });
        await _seed.Connection.InsertAsync(new TranslationEntity { Key = nameKey, LanguageCode = "fr", Value = "Règle de test" });
        var rule = new SpecialRuleEntity { NameKey = nameKey, Source = ContentSource.Official, OfficialId = "rule.test-rule" };
        await _seed.Connection.InsertAsync(rule);
        var seedAxe = await ByOfficialIdAsync<EquipmentItemEntity>(_seed, "equipment.axe");
        await _seed.Connection.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = seedAxe.Id, SpecialRuleId = rule.Id });

        var report = await SyncAsync();

        // La règle est nouvelle ; la Hache (Officielle) change parce que sa liste de règles change.
        Assert.Equal(new ContentSyncReport(1, 1, 0, 0), report);
        var localRule = (await _library.GetSpecialRulesAsync("fr")).Single(r => r.OfficialId == "rule.test-rule");
        Assert.Equal("Règle de test", localRule.Name);
        Assert.Equal(ContentSource.Official, localRule.Source);
        Assert.Contains((await LocalItemAsync("equipment.axe")).SpecialRules, r => r.Id == localRule.Id);
    }

    [Fact]
    public async Task ModifiedRow_KeptAndFlagged_WhenOfficialChanges()
    {
        var mine = await LocalItemAsync("equipment.axe");
        mine.Cost = 7;
        await _library.SaveEquipmentItemAsync(mine, "en");
        var seedAxe = await ByOfficialIdAsync<EquipmentItemEntity>(_seed, "equipment.axe");
        seedAxe.Cost = 99;
        await _seed.Connection.UpdateAsync(seedAxe);

        var report = await SyncAsync();

        Assert.Equal(new ContentSyncReport(0, 0, 0, 1), report);
        var after = await LocalItemAsync("equipment.axe");
        Assert.Equal(7, after.Cost);
        Assert.Equal(ContentSource.Modified, after.Source);
        var conflict = Assert.Single(await _local.Connection.Table<ContentConflictEntity>().ToListAsync());
        Assert.Equal("equipment.axe", conflict.OfficialId);
        Assert.Equal(nameof(EquipmentItemEntity), conflict.TableName);
    }

    private Task<Stream> OpenSeed() => Task.FromResult<Stream>(File.OpenRead(_seedPath));

    /// <summary>Hache modifiée par le joueur (coût 7) alors que l'officielle passe à 99 : en attente de choix.</summary>
    private async Task CreateAxeConflictAsync()
    {
        var mine = await LocalItemAsync("equipment.axe");
        mine.Cost = 7;
        await _library.SaveEquipmentItemAsync(mine, "en");
        var seedAxe = await ByOfficialIdAsync<EquipmentItemEntity>(_seed, "equipment.axe");
        seedAxe.Cost = 99;
        await _seed.Connection.UpdateAsync(seedAxe);
        await SyncAsync();
    }

    [Fact]
    public async Task Conflict_ListsOnlyDifferingFields()
    {
        await CreateAxeConflictAsync();

        var conflict = Assert.Single(await _local.GetContentConflictsAsync(OpenSeed, "fr"));

        Assert.Equal("equipment.axe", conflict.OfficialId);
        Assert.Equal("Hache", conflict.Name);
        Assert.Equal(new ContentFieldDifference("Cost", "99", "7"), Assert.Single(conflict.Differences));
    }

    [Fact]
    public async Task Conflict_TakeOfficial_RewritesRowAsOfficial()
    {
        await CreateAxeConflictAsync();
        var id = (await LocalItemAsync("equipment.axe")).Id;

        await _local.ResolveContentConflictAsync("equipment.axe", takeOfficial: true, OpenSeed);

        var axe = await LocalItemAsync("equipment.axe");
        Assert.Equal(id, axe.Id);
        Assert.Equal(99, axe.Cost);
        Assert.Equal(ContentSource.Official, axe.Source);
        Assert.Equal(0, await _local.GetPendingContentConflictCountAsync());
    }

    [Fact]
    public async Task Conflict_KeepMine_NotAskedAgainUntilOfficialChanges()
    {
        await CreateAxeConflictAsync();

        await _local.ResolveContentConflictAsync("equipment.axe", takeOfficial: false, OpenSeed);
        var again = await _local.SyncOfficialContentAsync(_seedPath);

        var axe = await LocalItemAsync("equipment.axe");
        Assert.Equal(7, axe.Cost);
        Assert.Equal(ContentSource.Modified, axe.Source);
        Assert.False(again.HasChanges, again.ToString());
        Assert.Equal(0, await _local.GetPendingContentConflictCountAsync());
    }

    [Fact]
    public async Task ModifiedRow_NotFlagged_WhenOfficialUnchanged()
    {
        var mine = await LocalItemAsync("equipment.axe");
        mine.Cost = 7;
        await _library.SaveEquipmentItemAsync(mine, "en");
        var seedSword = await ByOfficialIdAsync<EquipmentItemEntity>(_seed, "equipment.sword");
        seedSword.Cost = 99;
        await _seed.Connection.UpdateAsync(seedSword);

        var report = await SyncAsync();

        Assert.Equal(new ContentSyncReport(1, 0, 0, 0), report);
        Assert.Equal(7, (await LocalItemAsync("equipment.axe")).Cost);
        Assert.Empty(await _local.Connection.Table<ContentConflictEntity>().ToListAsync());
    }

    /// <summary>Base installée avant les empreintes (OfficialContentHashEntity vide) : impossible de savoir si
    /// l'officiel a changé depuis la modification du joueur - la différence est signalée une fois, puis plus
    /// jamais tant que l'officiel ne rebouge pas.</summary>
    [Fact]
    public async Task ModifiedRow_OnDatabaseWithoutHashes_FlaggedOnce()
    {
        await _local.Connection.ExecuteAsync("DELETE FROM OfficialContentHashEntity");
        var mine = await LocalItemAsync("equipment.axe");
        mine.Cost = 7;
        await _library.SaveEquipmentItemAsync(mine, "en");

        var first = await SyncAsync();
        await _local.Connection.ExecuteAsync("DELETE FROM ContentConflictEntity");
        var second = await _local.SyncOfficialContentAsync(_seedPath);

        Assert.Equal(new ContentSyncReport(0, 0, 0, 1), first);
        Assert.False(second.HasChanges, second.ToString());
    }

    [Fact]
    public async Task RowRemovedFromSeed_BecomesCustom_AndCustomRowsUntouched()
    {
        var seedMutation = (await _seed.Connection.Table<MutationEntity>().ToListAsync()).First(m => m.OfficialId is not null);
        await _seed.Connection.ExecuteAsync("DELETE FROM WarbandArchetypeMutationEntity WHERE MutationId = ?", seedMutation.Id);
        await _seed.Connection.DeleteAsync(seedMutation);
        var custom = new SpecialRule { Name = "House rule", Source = ContentSource.Custom };
        await _library.SaveSpecialRuleAsync(custom, "en");

        var report = await SyncAsync();

        Assert.Equal(new ContentSyncReport(0, 0, 1, 0), report);
        var retired = (await _local.Connection.Table<MutationEntity>().ToListAsync()).Where(m => m.OfficialId == seedMutation.OfficialId);
        Assert.Empty(retired);
        Assert.Equal(1, await _local.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM MutationEntity WHERE Source = ? AND OfficialId IS NULL", ContentSource.Custom));
        var house = (await _library.GetSpecialRulesAsync("en")).Single(r => r.Id == custom.Id);
        Assert.Equal(ContentSource.Custom, house.Source);
        Assert.Equal("House rule", house.Name);
    }

    /// <summary>Au lancement : synchro seulement si la base est en retard sur ContentVersion.json.</summary>
    [Fact]
    public async Task Startup_SyncsOnlyWhenDatabaseVersionIsBehind()
    {
        var localAxe = await ByOfficialIdAsync<EquipmentItemEntity>(_local, "equipment.axe");
        localAxe.Cost = 1;
        await _local.Connection.UpdateAsync(localAxe);
        await _local.Connection.ExecuteAsync("DELETE FROM ContentMetaEntity");
        await _local.Connection.CloseAsync();
        await _seed.Connection.CloseAsync();
        Task<Stream> OpenSeed() => Task.FromResult<Stream>(File.OpenRead(_seedPath));

        var behind = new AppDatabase(_localPath, OpenSeed);
        await behind.Initialization;
        var report = behind.StartupSyncReport;
        var version = (await behind.GetContentMetaAsync()).Version;
        var cost = (await ByOfficialIdAsync<EquipmentItemEntity>(behind, "equipment.axe")).Cost;
        await behind.Connection.CloseAsync();
        var upToDate = new AppDatabase(_localPath, OpenSeed);
        await upToDate.Initialization;
        _local = upToDate;

        Assert.Null(behind.StartupSyncError);
        Assert.Equal(new ContentSyncReport(1, 0, 0, 0), report);
        Assert.Equal(SeedContent.Version, version);
        Assert.NotEqual(1, cost);
        Assert.Null(upToDate.StartupSyncReport);
    }

    [Fact]
    public async Task ContentVersion_TakenFromSeed()
    {
        await _seed.Connection.InsertOrReplaceAsync(new ContentMetaEntity { Key = SeedContent.VersionKey, Value = "42" });

        await SyncAsync();

        Assert.Equal(42, (await _local.GetContentMetaAsync()).Version);
    }
}
