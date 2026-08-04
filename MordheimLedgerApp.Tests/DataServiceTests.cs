using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Exercises LibraryService/WarbandService against a real (temp-file) SQLite database.</summary>
public class DataServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;

    public DataServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _warbands = new WarbandService(_db);
    }

    public void Dispose()
    {
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Database_SeedsReiklanderMercenariesOnFirstLaunch()
    {
        var archetypes = await _library.GetWarbandArchetypesAsync("en");
        var reiklanders = Assert.Single(archetypes);
        Assert.Equal("Reiklander Mercenaries", reiklanders.Name);
        Assert.Equal(ContentSource.Official, reiklanders.Source);

        var warriorArchetypes = await _library.GetWarriorArchetypesAsync(reiklanders.Id, "en");
        Assert.Equal(4, warriorArchetypes.Count);

        var equipment = await _library.GetEquipmentItemsAsync("en");
        Assert.NotEmpty(equipment);
    }

    [Fact]
    public async Task Database_SeedsReiklanderMercenaries_InFrenchToo()
    {
        var archetypes = await _library.GetWarbandArchetypesAsync("fr");
        var reiklanders = Assert.Single(archetypes);
        Assert.Equal("Mercenaires Reiklander", reiklanders.Name);
    }

    [Fact]
    public async Task CreateWarband_PreFillsTreasuryFromArchetype()
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single();

        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);

        Assert.NotEqual(0, warband.Id);
        Assert.Equal(archetype.StartingTreasury, warband.Treasury);
        Assert.Equal(archetype.Id, warband.WarbandArchetypeId);
    }

    [Fact]
    public async Task RecruitWarrior_PreFillsStatsFromArchetype_AndPersists()
    {
        var warbandArchetype = (await _library.GetWarbandArchetypesAsync("en")).Single();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var captainArchetype = (await _library.GetWarriorArchetypesAsync(warbandArchetype.Id, "en"))
            .Single(a => a.Name == "Mercenary Captain");

        var recruited = await _warbands.RecruitWarriorAsync(warband.Id, captainArchetype, "Otto");

        Assert.NotEqual(0, recruited.Id);
        Assert.Equal(captainArchetype.Movement, recruited.Movement);
        Assert.Equal(captainArchetype.Cost, recruited.Cost);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var persisted = Assert.Single(roster);
        Assert.Equal("Otto", persisted.Name);
    }

    [Fact]
    public async Task EditingOfficialArchetype_FlipsSourceToModified()
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single();
        archetype.StartingTreasury = 600;

        await _library.SaveWarbandArchetypeAsync(archetype, "en");

        var reloaded = (await _library.GetWarbandArchetypesAsync("en")).Single();
        Assert.Equal(ContentSource.Modified, reloaded.Source);
        Assert.Equal(600, reloaded.StartingTreasury);
    }

    [Fact]
    public async Task DeleteWarband_CascadesToWarriorsAndEquipment()
    {
        var warbandArchetype = (await _library.GetWarbandArchetypesAsync("en")).Single();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var captainArchetype = (await _library.GetWarriorArchetypesAsync(warbandArchetype.Id, "en")).First();
        await _warbands.RecruitWarriorAsync(warband.Id, captainArchetype, "Otto");

        await _warbands.DeleteWarbandAsync(warband.Id);

        Assert.Null(await _warbands.GetWarbandAsync(warband.Id));
        Assert.Empty(await _warbands.GetWarriorsAsync(warband.Id, "en"));
    }
}
