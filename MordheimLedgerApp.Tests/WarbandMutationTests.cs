using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Tests that mutate warband/catalog state (recruit, delete, flip Official to Modified) - each
/// gets its own freshly-seeded database (unlike DataServiceTests' shared SeededDatabaseFixture) so one
/// test's writes can't leak into another's assertions.</summary>
public class WarbandMutationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;

    public WarbandMutationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _warbands = new WarbandService(_db, _library);
    }

    public void Dispose()
    {
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<WarbandArchetype> GetReiklandersAsync(string languageCode = "en") =>
        (await _library.GetWarbandArchetypesAsync(languageCode)).Single(a => a.Name is "Reiklander Mercenaries" or "Mercenaires Reiklander");

    [Fact]
    public async Task CreateWarband_PreFillsTreasuryFromArchetype()
    {
        var archetype = await GetReiklandersAsync();

        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);

        Assert.NotEqual(0, warband.Id);
        Assert.Equal(archetype.StartingTreasury, warband.Treasury);
        Assert.Equal(archetype.Id, warband.WarbandArchetypeId);
    }

    [Fact]
    public async Task RecruitWarrior_PreFillsStatsFromArchetype_AndPersists()
    {
        var warbandArchetype = await GetReiklandersAsync();
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

    /// <summary>Regression test for a real bug: GetWarriorsAsync used to resolve each carried
    /// EquipmentItem via a minimal FindAsync+ToModel(translations) call, leaving SpecialRules (and
    /// restrictions) empty for every already-recruited warrior's equipment - invisible until the
    /// warband-detail chip dialogs started actually displaying them.</summary>
    [Fact]
    public async Task RecruitedWarrior_CarriedEquipment_HasSpecialRulesResolved()
    {
        var warbandArchetype = await GetReiklandersAsync();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var captainArchetype = (await _library.GetWarriorArchetypesAsync(warbandArchetype.Id, "en")).First();
        var recruited = await _warbands.RecruitWarriorAsync(warband.Id, captainArchetype, "Otto");

        var equipmentWithRule = (await _library.GetEquipmentItemsAsync("en")).First(i => i.SpecialRules.Count > 0);
        await _warbands.AddWarriorEquipmentAsync(recruited.Id, equipmentWithRule);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var warrior = Assert.Single(roster);
        var carried = Assert.Single(warrior.Equipment);
        Assert.NotEmpty(carried.Item.SpecialRules);
    }

    [Fact]
    public async Task EditingOfficialArchetype_FlipsSourceToModified()
    {
        var archetype = await GetReiklandersAsync();
        archetype.StartingTreasury = 600;

        await _library.SaveWarbandArchetypeAsync(archetype, "en");

        var reloaded = await GetReiklandersAsync();
        Assert.Equal(ContentSource.Modified, reloaded.Source);
        Assert.Equal(600, reloaded.StartingTreasury);
    }

    [Fact]
    public async Task DeleteWarband_CascadesToWarriorsAndEquipment()
    {
        var warbandArchetype = await GetReiklandersAsync();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var captainArchetype = (await _library.GetWarriorArchetypesAsync(warbandArchetype.Id, "en")).First();
        await _warbands.RecruitWarriorAsync(warband.Id, captainArchetype, "Otto");

        await _warbands.DeleteWarbandAsync(warband.Id);

        Assert.Null(await _warbands.GetWarbandAsync(warband.Id));
        Assert.Empty(await _warbands.GetWarriorsAsync(warband.Id, "en"));
    }
}
