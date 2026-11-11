using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Réserve de bande et recherche d'objets rares en fin de partie (PostBattleTradingService) - base
/// fraîchement seedée par test, comme WarbandMutationTests.</summary>
public class PostBattleTradingServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly PostBattleTradingService _service;

    public PostBattleTradingServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _service = new PostBattleTradingService(_warbands);
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* déjà signalé par le test */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<Warband> WarbandAsync()
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Reiklander Mercenaries");
        return await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);
    }

    private async Task<EquipmentItem> ItemAsync(string name) => (await _library.GetEquipmentItemsAsync("en")).First(i => i.Name == name);

    private Task<List<WarbandEquipment>> StashAsync(Warband warband) => _warbands.GetWarbandEquipmentAsync(warband.Id, "en");

    [Fact]
    public async Task Reserve_UnchangedLinesStay_ReducedOnesShrink_RemovedOnesGo()
    {
        var warband = await WarbandAsync();
        var axe = await ItemAsync("Axe");
        var sword = await ItemAsync("Sword");
        var dagger = await ItemAsync("Dagger");
        var keptAxe = await _warbands.AddWarbandEquipmentAsync(warband.Id, axe, quantity: 2);
        var soldSwords = await _warbands.AddWarbandEquipmentAsync(warband.Id, sword, quantity: 3);
        var usedDagger = await _warbands.AddWarbandEquipmentAsync(warband.Id, dagger);
        List<StashLine> original =
        [
            new(keptAxe.Id, axe, null, 2), new(soldSwords.Id, sword, null, 3), new(usedDagger.Id, dagger, null, 1),
        ];

        await _service.ApplyReserveAsync(warband.Id, original, [new(keptAxe.Id, axe, null, 2), new(soldSwords.Id, sword, null, 1)]);

        var stash = await StashAsync(warband);
        Assert.Equal(2, stash.Single(s => s.Item.Id == axe.Id).Quantity);
        Assert.Equal(1, stash.Single(s => s.Item.Id == sword.Id).Quantity);
        Assert.DoesNotContain(stash, s => s.Item.Id == dagger.Id);
    }

    [Fact]
    public async Task Reserve_NewLines_AreMergedByItemAndMaterial()
    {
        var warband = await WarbandAsync();
        var sword = await ItemAsync("Sword");
        var gromril = (await _library.GetSpecialRulesAsync("en")).Single(r => r.Name == "Gromril Weapon");

        await _service.ApplyReserveAsync(warband.Id, [],
            [new(null, sword, null, 1), new(null, sword, null, 2), new(null, sword, gromril, 1)]);

        var stash = await StashAsync(warband);
        Assert.Equal(3, stash.Single(s => s.MaterialRule is null).Quantity);
        Assert.Equal(1, stash.Single(s => s.MaterialRule is not null).Quantity);
    }

    [Fact]
    public async Task RareItems_PricesAreDebited()
    {
        var warband = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.ApplyRareItemSearchAsync(warband, [40, 25], [], []);

        Assert.Equal(treasury - 65, (await _warbands.GetWarbandAsync(warband.Id))!.Treasury);
    }

    [Fact]
    public async Task RarePersona_PaidInGold_OrInWyrdstone()
    {
        var warband = await WarbandAsync();
        warband.WyrdstoneShards = 3;
        await _warbands.SaveWarbandAsync(warband);
        var personae = await _library.GetDramatisPersonaeAsync("en");
        var golden = personae.First(p => p.PairedWithDramatisPersona is null);
        var stony = personae.First(p => p.PairedWithDramatisPersona is null && p.Id != golden.Id);
        var treasury = warband.Treasury;

        await _service.ApplyRareItemSearchAsync(warband, [],
            [new(golden, PersonaPayment.Gold, 30), new(stony, PersonaPayment.Wyrdstone, 1)], await _library.GetEquipmentItemsAsync("en"));

        var reloaded = (await _warbands.GetWarbandAsync(warband.Id))!;
        Assert.Equal(treasury - 30, reloaded.Treasury);
        Assert.Equal(2, reloaded.WyrdstoneShards);
        Assert.Equal(2, (await _warbands.GetWarriorsAsync(warband.Id, "en")).Count);
    }

    [Fact]
    public async Task RarePersona_Pair_RecruitsBothForASingleFee()
    {
        var warband = await WarbandAsync();
        var pair = (await _library.GetDramatisPersonaeAsync("en")).First(p => p.PairedWithDramatisPersona is not null);
        var treasury = warband.Treasury;

        await _service.ApplyRareItemSearchAsync(warband, [], [new(pair, PersonaPayment.Gold, 30)], await _library.GetEquipmentItemsAsync("en"));

        var names = (await _warbands.GetWarriorsAsync(warband.Id, "en")).Select(w => w.Name).ToList();
        Assert.Contains(pair.Name, names);
        Assert.Contains(pair.PairedWithDramatisPersona!.Name, names);
        Assert.Equal(treasury - 30, (await _warbands.GetWarbandAsync(warband.Id))!.Treasury);
    }

    [Fact]
    public async Task RarePersona_DuplicateStartingEquipment_IsKeptAsQuantityTwo()
    {
        var warband = await WarbandAsync();
        var bertha = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name.StartsWith("Bertha"));

        await _service.ApplyRareItemSearchAsync(warband, [], [new(bertha, PersonaPayment.None, 0)], await _library.GetEquipmentItemsAsync("en"));

        var recruited = Assert.Single(await _warbands.GetWarriorsAsync(warband.Id, "en"));
        Assert.Contains(recruited.Equipment, e => e.Quantity == 2);
    }

    // --- Ajouts après le passage Stryker du 2026-11-11 (mutants survivants) ---

    [Fact]
    public async Task Reserve_UnchangedLine_KeepsItsRow()
    {
        var warband = await WarbandAsync();
        var axe = await ItemAsync("Axe");
        var line = await _warbands.AddWarbandEquipmentAsync(warband.Id, axe, quantity: 2);

        await _service.ApplyReserveAsync(warband.Id, [new(line.Id, axe, null, 2)], [new(line.Id, axe, null, 2)]);

        Assert.Equal(line.Id, Assert.Single(await StashAsync(warband)).Id);
    }

    [Fact]
    public async Task RarePersona_StartingEquipmentMissingFromTheCatalog_IsSkipped()
    {
        var warband = await WarbandAsync();
        var persona = (await _library.GetDramatisPersonaeAsync("en")).First(p => p.StartingEquipmentIds.Count > 0 && p.PairedWithDramatisPersona is null);

        await _service.ApplyRareItemSearchAsync(warband, [], [new(persona, PersonaPayment.None, 0)], []);

        Assert.Empty(Assert.Single(await _warbands.GetWarriorsAsync(warband.Id, "en")).Equipment);
    }
}
