using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Départs gardés parmi les Mentions honorables : équipement réel retiré de la bande, rappel sur la carte.</summary>
public class WarriorDepartureServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;

    public WarriorDepartureServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* le test l'a déjà signalé */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<(Warband Warband, Warrior Hero)> HeroAsync()
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.OfficialId == "warband.reiklanders");
        var warband = await _warbands.CreateWarbandAsync("Loups", archetype);
        var champion = (await _library.GetWarriorArchetypesAsync(archetype.Id, "en")).Single(t => t.OfficialId == "warrior.reiklanders.champion");
        var hero = await _warbands.RecruitWarriorAsync(warband.Id, champion, "Klaus");
        return (warband, hero);
    }

    private async Task<EquipmentItem> ItemAsync(string officialId) =>
        (await _library.GetEquipmentItemsAsync("en")).Single(i => i.OfficialId == officialId);

    private async Task<Warrior> ReloadAsync(Warband warband, int id) =>
        (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == id);

    [Fact]
    public async Task Retire_KeepsAReminder_RemovesTheRealEquipment_AndKeepsUnsavedLines()
    {
        var (warband, hero) = await HeroAsync();
        await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("equipment.sword"));
        await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("equipment.shield"));
        var warrior = await ReloadAsync(warband, hero.Id);
        // Lignes que le wizard tient en mémoire sans les avoir encore écrites (id 0 ou négatif) : ni rappelées ni retirées.
        var draft = new WarriorEquipment { Id = 0, Item = await ItemAsync("equipment.dagger") };
        var pending = new WarriorEquipment { Id = -1, Item = await ItemAsync("equipment.axe") };
        warrior.Equipment.Add(draft);
        warrior.Equipment.Add(pending);

        await new WarriorDepartureService(_warbands).RetireAsync(warrior, OutOfPlayReason.Unpaid);

        Assert.Equal(new[] { draft, pending }, warrior.Equipment);
        var saved = await ReloadAsync(warband, hero.Id);
        Assert.Empty(saved.Equipment);
        Assert.Equal(WarriorStatus.Retired, saved.Status);
        Assert.Equal(OutOfPlayReason.Unpaid, saved.OutReason);
        Assert.Equal("Sword, Shield", saved.EquipmentAtDeparture);
    }

    [Fact]
    public async Task Retire_WithNothingCarried_LeavesNoReminder()
    {
        var (warband, hero) = await HeroAsync();
        await new WarriorDepartureService(_warbands).RetireAsync(hero, OutOfPlayReason.DidNotJoin);

        var saved = await ReloadAsync(warband, hero.Id);
        Assert.Null(saved.EquipmentAtDeparture);
        Assert.Equal(OutOfPlayReason.DidNotJoin, saved.OutReason);
    }
}
