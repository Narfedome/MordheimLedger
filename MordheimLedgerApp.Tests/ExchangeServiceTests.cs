using System.Text.Json;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Exchange;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Format d'échange (v2, lot 0) : export depuis une base, import dans une autre, chacune fraîchement seedée
/// (deux appareils).</summary>
public class ExchangeServiceTests : IDisposable
{
    private sealed class Device : IDisposable
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        private readonly string _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        public AppDatabase Db { get; }
        public ILibraryService Library { get; }
        public IWarbandService Warbands { get; }
        public WarriorPhotoStore Photos { get; }
        public ExchangeService Exchange { get; }

        public Device()
        {
            Db = new AppDatabase(_dbPath);
            Library = new LibraryService(Db);
            Photos = new WarriorPhotoStore(_photoRoot);
            Warbands = new WarbandService(Db, Library, Photos);
            Exchange = new ExchangeService(Db, Photos);
        }

        public void Dispose()
        {
            try { Db.Initialization.GetAwaiter().GetResult(); } catch { /* déjà signalé par le test */ }
            Db.Connection.CloseAsync().GetAwaiter().GetResult();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
        }
    }

    private readonly Device _alice = new();
    private readonly Device _bob = new();

    public void Dispose()
    {
        _alice.Dispose();
        _bob.Dispose();
    }

    private static async Task<ExchangePackage> ThroughArchiveAsync(ExchangePackage package)
    {
        using var file = new MemoryStream();
        await ExchangeArchive.WriteAsync(package, file);
        file.Position = 0;
        return await ExchangeArchive.ReadAsync(file);
    }

    /// <summary>Objet Personnalisé avec une règle spéciale officielle (jointure) et une restriction à une bande.</summary>
    private static async Task<EquipmentItem> CustomItemAsync(Device device, string name, int cost)
    {
        var rule = (await device.Library.GetSpecialRulesAsync("en")).First(r => r.OfficialId != null);
        var reiklanders = (await device.Library.GetWarbandArchetypesAsync("en")).Single(a => a.OfficialId == SampleWarband.WarbandOfficialId);
        var item = new EquipmentItem
        {
            Name = name,
            Description = "Forgée dans la pierre de malheur.",
            Category = EquipmentCategory.MeleeWeapon,
            Cost = cost,
            Source = ContentSource.Custom,
            SpecialRules = [rule],
            RestrictedToWarbandArchetypeIds = [reiklanders.Id],
        };
        await device.Library.SaveEquipmentItemAsync(item, "fr");
        return item;
    }

    private static async Task<Warband> SampleAsync(Device device) =>
        (await SampleWarband.CreateAsync(device.Warbands, device.Library, "en", "Les Roses Sanglantes", ["Guerriers", "Tireurs"]))!;

    [Fact]
    public async Task Warband_RoundTrip_KeepsRosterCustomContentHistoryAndPhoto()
    {
        var warband = await SampleAsync(_alice);
        var blade = await CustomItemAsync(_alice, "Lame de Wyrd", 42);
        var warriors = await _alice.Warbands.GetWarriorsAsync(warband.Id, "en");
        var captain = warriors.First(w => w.IsHero);
        await _alice.Warbands.AddWarriorEquipmentAsync(captain.Id, blade);
        await _alice.Warbands.AddHistoryEntryAsync(warband.Id, "Victoire au Puits.");
        var photo = await _alice.Photos.SaveAsync(new MemoryStream([1, 2, 3, 4]), ".jpg");
        await _alice.Db.Connection.ExecuteAsync("UPDATE WarriorEntity SET PhotoPath = ? WHERE Id = ?", photo, captain.Id);

        var package = await ThroughArchiveAsync(await _alice.Exchange.ExportWarbandsAsync([warband.Id]));

        var preview = await _bob.Exchange.PreviewAsync(package, "fr");
        Assert.True(preview.CanImport);
        var warbandPreview = Assert.Single(preview.Warbands);
        Assert.Null(warbandPreview.ExistingWarbandId);
        Assert.Equal("Mercenaires Reiklander", warbandPreview.ArchetypeName);
        Assert.Equal(warriors.Sum(w => w.HeadCount), warbandPreview.WarriorCount);
        var bladePreview = Assert.Single(preview.Catalog, c => c.Status == CatalogImportStatus.New);
        Assert.Equal("Lame de Wyrd", bladePreview.Name);
        Assert.All(preview.Catalog.Where(c => c != bladePreview), c => Assert.Equal(CatalogImportStatus.Existing, c.Status));

        var result = await _bob.Exchange.ImportAsync(package);

        var imported = await _bob.Warbands.GetWarbandAsync(Assert.Single(result.WarbandIds));
        Assert.Equal("Les Roses Sanglantes", imported!.Name);
        Assert.Equal(warband.Treasury, imported.Treasury);
        var bobWarriors = await _bob.Warbands.GetWarriorsAsync(imported.Id, "en");
        Assert.Equal(warriors.Select(w => (w.Name, w.HeadCount, w.Experience)), bobWarriors.Select(w => (w.Name, w.HeadCount, w.Experience)));
        var bobCaptain = bobWarriors.Single(w => w.Name == captain.Name);
        Assert.Equal(
            (await _alice.Warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == captain.Id).Equipment.Select(e => e.NameDisplay).Order(),
            bobCaptain.Equipment.Select(e => e.NameDisplay).Order());
        Assert.NotNull(_bob.Photos.GetExistingFullPath(bobCaptain.PhotoPath));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(_bob.Photos.GetFullPath(bobCaptain.PhotoPath)!));

        var bobBlade = (await _bob.Library.GetEquipmentItemsAsync("fr")).Single(i => i.Name == "Lame de Wyrd");
        Assert.Equal(ContentSource.Custom, bobBlade.Source);
        Assert.Equal(42, bobBlade.Cost);
        Assert.Equal("Forgée dans la pierre de malheur.", bobBlade.Description);
        Assert.Equal(blade.SpecialRules.Single().OfficialId, Assert.Single(bobBlade.SpecialRules).OfficialId);
        var bobReiklanders = (await _bob.Library.GetWarbandArchetypesAsync("en")).Single(a => a.OfficialId == SampleWarband.WarbandOfficialId);
        Assert.Equal([bobReiklanders.Id], bobBlade.RestrictedToWarbandArchetypeIds);
        Assert.Contains((await _bob.Warbands.GetHistoryEntriesAsync(imported.Id)), h => h.Text == "Victoire au Puits.");
    }

    [Fact]
    public async Task ReimportedWarband_IsRecognized_CopiedByDefault_OrReplaced()
    {
        var warband = await SampleAsync(_alice);
        var package = await _alice.Exchange.ExportWarbandsAsync([warband.Id]);
        var first = Assert.Single((await _bob.Exchange.ImportAsync(package)).WarbandIds);

        // La bande revient après une partie (même identifiant d'échange).
        warband.Treasury += 35;
        await _alice.Warbands.SaveWarbandAsync(warband);
        var again = await _alice.Exchange.ExportWarbandsAsync([warband.Id]);
        var preview = await _bob.Exchange.PreviewAsync(again);
        Assert.Equal(first, Assert.Single(preview.Warbands).ExistingWarbandId);

        var copy = Assert.Single((await _bob.Exchange.ImportAsync(again)).WarbandIds);
        Assert.Equal(2, (await _bob.Warbands.GetWarbandsAsync()).Count);
        var identities = await _bob.Db.Connection.Table<ShareIdentityEntity>().Where(i => i.TableName == nameof(WarbandEntity)).ToListAsync();
        Assert.NotEqual(identities.Single(i => i.LocalId == first).ShareId, identities.Single(i => i.LocalId == copy).ShareId);

        var decisions = new ImportDecisions();
        decisions.Warbands[preview.Warbands[0].ShareId!] = WarbandImportMode.Replace;
        var replaced = Assert.Single((await _bob.Exchange.ImportAsync(again, decisions)).WarbandIds);
        var bobWarbands = await _bob.Warbands.GetWarbandsAsync();
        Assert.Equal(2, bobWarbands.Count);
        Assert.DoesNotContain(bobWarbands, w => w.Id == first);
        Assert.Equal(warband.Treasury, bobWarbands.Single(w => w.Id == replaced).Treasury);
        Assert.Empty(await _bob.Db.Connection.QueryAsync<WarriorEntity>("SELECT * FROM WarriorEntity WHERE WarbandId = ?", first));
    }

    [Fact]
    public async Task EditedCustomEntry_IsAConflict_KeptByDefault_ReplacedOnRequest()
    {
        var blade = await CustomItemAsync(_alice, "Lame de Wyrd", 42);
        var share = await _alice.Exchange.ExportCatalogAsync([new CatalogItemRef(typeof(EquipmentItemEntity), blade.Id)]);
        Assert.Equal(1, (await _bob.Exchange.ImportAsync(share)).CatalogCreated);

        // Renvoyé sans changement : reconnu, rien de créé.
        var unchanged = await _alice.Exchange.ExportCatalogAsync([new CatalogItemRef(typeof(EquipmentItemEntity), blade.Id)]);
        Assert.Equal(CatalogImportStatus.Identical, (await _bob.Exchange.PreviewAsync(unchanged)).Catalog.Single(c => c.IsShared).Status);

        // Retouché chez Alice (l'édition du Codex garde l'identifiant d'échange).
        blade.Cost = 50;
        await _alice.Library.SaveEquipmentItemAsync(blade, "fr");
        var edited = await _alice.Exchange.ExportCatalogAsync([new CatalogItemRef(typeof(EquipmentItemEntity), blade.Id)]);
        var conflict = (await _bob.Exchange.PreviewAsync(edited)).Catalog.Single(c => c.IsShared);
        Assert.Equal(CatalogImportStatus.Conflict, conflict.Status);
        var difference = Assert.Single(conflict.Differences);
        Assert.Equal(("Cost", "50", "42"), (difference.Field, difference.Official, difference.Mine));

        await _bob.Exchange.ImportAsync(edited);
        Assert.Equal(42, (await _bob.Library.GetEquipmentItemsAsync("fr")).Single(i => i.Name == "Lame de Wyrd").Cost);

        var decisions = new ImportDecisions();
        decisions.TakeIncoming.Add(conflict.EntryId);
        Assert.Equal(1, (await _bob.Exchange.ImportAsync(edited, decisions)).CatalogReplaced);
        Assert.Equal(50, (await _bob.Library.GetEquipmentItemsAsync("fr")).Single(i => i.Name == "Lame de Wyrd").Cost);
        Assert.Single(await _bob.Library.GetEquipmentItemsAsync("fr"), i => i.Name == "Lame de Wyrd");
    }

    [Fact]
    public async Task CompactText_WithoutOfficialContent_ResolvesOfficialLocally_AndReportsWhatIsMissing()
    {
        var blade = await CustomItemAsync(_alice, "Lame de Wyrd", 42);
        var package = await _alice.Exchange.ExportCatalogAsync([new CatalogItemRef(typeof(EquipmentItemEntity), blade.Id)],
            new ExchangeExportOptions(EmbedOfficialContent: false, IncludePhotos: false));
        Assert.Single(package.Catalog);
        var text = ExchangeArchive.ToCompactText(package);
        Assert.True(text.Length < 3000, $"{text.Length} caractères");

        var received = ExchangeArchive.FromCompactText(text);
        await _bob.Exchange.ImportAsync(received);
        var bobBlade = (await _bob.Library.GetEquipmentItemsAsync("fr")).Single(i => i.Name == "Lame de Wyrd");
        Assert.Single(bobBlade.SpecialRules);

        // Contenu officiel d'une version plus récente, sans secours : import refusé, rien d'écrit.
        received.Catalog[0].Key = CatalogEntry.SharedPrefix + Guid.NewGuid().ToString("N");
        received.Catalog[0].Joins[nameof(EquipmentItemSpecialRuleEntity)] = [CatalogEntry.OfficialPrefix + "rule.from-the-future"];
        var preview = await _bob.Exchange.PreviewAsync(received);
        Assert.False(preview.CanImport);
        Assert.Equal($"{nameof(SpecialRuleEntity)}|o:rule.from-the-future", Assert.Single(preview.MissingReferences));
        await Assert.ThrowsAsync<ExchangeFormatException>(() => _bob.Exchange.ImportAsync(received));
        Assert.Single(await _bob.Library.GetEquipmentItemsAsync("fr"), i => i.Name == "Lame de Wyrd");
    }

    [Fact]
    public async Task UnknownOfficialEntry_TravellingAsFallback_IsCreated()
    {
        var blade = await CustomItemAsync(_alice, "Lame de Wyrd", 42);
        var package = await _alice.Exchange.ExportCatalogAsync([new CatalogItemRef(typeof(EquipmentItemEntity), blade.Id)]);
        // La règle officielle jointe est inconnue de Bob (appli plus ancienne) : elle voyage en entier.
        var rule = package.Catalog.Single(c => c.Table == nameof(SpecialRuleEntity));
        var futureKey = CatalogEntry.OfficialPrefix + "rule.from-the-future";
        package.Catalog.Single(c => c.Table == nameof(EquipmentItemEntity)).Joins[nameof(EquipmentItemSpecialRuleEntity)] = [futureKey];
        rule.Key = futureKey;
        rule.Translations["NameKey"] = new(StringComparer.Ordinal) { ["en"] = "Rule from the future" };

        var preview = await _bob.Exchange.PreviewAsync(package);
        Assert.Equal(CatalogImportStatus.New, preview.Catalog.Single(c => c.Table == nameof(SpecialRuleEntity)).Status);
        await _bob.Exchange.ImportAsync(package);

        var bobRule = Assert.Single((await _bob.Library.GetEquipmentItemsAsync("en")).Single(i => i.Id != 0 && i.Source == ContentSource.Custom).SpecialRules);
        Assert.Equal("Rule from the future", bobRule.Name);
        Assert.Equal("rule.from-the-future", bobRule.OfficialId);
        Assert.Equal(ContentSource.Official, bobRule.Source);
    }

    /// <summary>QR d'une bande (v2, lot 3) : la bande seule, découpée en plusieurs QR lus dans le désordre ; son objet
    /// Personnalisé se partage par son propre QR, sans lequel l'import le signale manquant.</summary>
    [Fact]
    public async Task WarbandQr_CarriesTheWarbandOnly_InSeveralCodes_CustomContentSharedSeparately()
    {
        var warband = await SampleAsync(_alice);
        var blade = await CustomItemAsync(_alice, "Lame de Wyrd", 42);
        var captain = (await _alice.Warbands.GetWarriorsAsync(warband.Id, "en")).First(w => w.IsHero);
        await _alice.Warbands.AddWarriorEquipmentAsync(captain.Id, blade);

        var package = await _alice.Exchange.ExportWarbandsAsync([warband.Id], ExchangeExportOptions.QrWarband);
        Assert.Empty(package.Catalog);
        var codes = ExchangeQrCodes.Encode(package, charactersPerCode: 400);
        Assert.True(codes.Count > 1);
        Assert.All(codes, c => Assert.True(c.Length <= 400));

        var collector = new ExchangeQrCodes.Collector();
        ExchangePackage? scanned = null;
        foreach (var code in codes.Reverse())
            scanned = collector.Add(code) ?? scanned;
        Assert.NotNull(scanned);

        var preview = await _bob.Exchange.PreviewAsync(scanned!);
        Assert.Equal($"{nameof(EquipmentItemEntity)}|s:", Assert.Single(preview.MissingReferences)[..^32]);

        var bladeCodes = ExchangeQrCodes.Encode(await _alice.Exchange.ExportCatalogAsync(
            [new CatalogItemRef(typeof(EquipmentItemEntity), blade.Id)], ExchangeExportOptions.QrCatalog));
        await _bob.Exchange.ImportAsync(new ExchangeQrCodes.Collector().Add(Assert.Single(bladeCodes))!);

        var imported = Assert.Single((await _bob.Exchange.ImportAsync(scanned!)).WarbandIds);
        var bobCaptain = (await _bob.Warbands.GetWarriorsAsync(imported, "en")).Single(w => w.Name == captain.Name);
        Assert.Contains(bobCaptain.Equipment, e => e.Item.Name == "Lame de Wyrd");
    }

    [Fact]
    public void QrCollector_RestartsOnAnotherSet_RefusesForeignCodes_AndTooManyCodesAreRefused()
    {
        var package = new ExchangePackage { SharedEntries = Enumerable.Range(0, 60).Select(i => $"entrée {i} {Guid.NewGuid()}").ToList() };
        var first = ExchangeQrCodes.Encode(package, charactersPerCode: 600);
        var second = ExchangeQrCodes.Encode(package, charactersPerCode: 600);
        Assert.True(first.Count > 1);

        var collector = new ExchangeQrCodes.Collector();
        Assert.Null(collector.Add(first[0]));
        Assert.Equal((1, first.Count), (collector.Received, collector.Total));
        Assert.Null(collector.Add(second[0])); // autre envoi : l'assemblage recommence
        Assert.Equal(1, collector.Received);
        ExchangePackage? result = null;
        foreach (var code in second.Skip(1)) result = collector.Add(code);
        Assert.Equal(package.SharedEntries, result!.SharedEntries);

        Assert.Throws<ExchangeFormatException>(() => collector.Add("https://example.com"));
        Assert.Throws<ExchangeFormatException>(() => collector.Add("MLQ1:abcdef:4/3:xyz"));
        Assert.Throws<ExchangeTooLargeForQrException>(() => ExchangeQrCodes.Encode(package, charactersPerCode: 60));
    }

    [Fact]
    public void Package_FromANewerVersion_OrAnotherApp_IsRefused()
    {
        var json = ExchangeArchive.ToJson(new ExchangePackage { Version = ExchangePackage.CurrentVersion + 1 });
        Assert.Throws<ExchangeFormatException>(() => ExchangeArchive.FromJson(json));
        Assert.Throws<ExchangeFormatException>(() => ExchangeArchive.FromJson("""{"format":"autre","version":1}"""));
        Assert.Throws<ExchangeFormatException>(() => ExchangeArchive.FromCompactText("pas un code"));
        Assert.Throws<ExchangeFormatException>(() => ExchangeArchive.FromCompactText("MLX1:@@@"));
    }

    /// <summary>Garde-fou : toute colonne « …Id » d'une table de partie jouée est la clé, la colonne parente, une
    /// référence déclarée ou une colonne exclue - et les tables suivent celles que la suppression d'une bande
    /// connaît (AppDatabase.WarbandChildTables/WarriorChildTables).</summary>
    [Fact]
    public void PlayedSchema_CoversEveryIdColumn()
    {
        foreach (var table in PlayedDataSchema.Tables)
            foreach (var property in table.Type.GetProperties().Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal)))
                Assert.True(property.Name == "Id" || property.Name == table.ParentColumn
                    || table.References.Any(r => r.Column == property.Name) || table.Excluded.Contains(property.Name),
                    $"{table.Type.Name}.{property.Name} n'est pas déclarée dans PlayedDataSchema");

        Assert.Equal(AppDatabase.WarbandChildTables.Append(nameof(WarriorEntity)).Order(),
            PlayedDataSchema.ChildrenOf(typeof(WarbandEntity)).Select(t => t.Type.Name).Order());
        Assert.Equal(AppDatabase.WarriorChildTables.Order(),
            PlayedDataSchema.ChildrenOf(typeof(WarriorEntity)).Select(t => t.Type.Name).Order());
    }
}
