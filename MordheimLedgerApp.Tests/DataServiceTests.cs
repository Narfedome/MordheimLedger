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

    /// <summary>The seed now covers 3 warbands (Reiklander Mercenaries hardcoded + Undead/Dwarf Treasure
    /// Hunters from JSON, see AppDatabase.SeedWarbandFromJsonAsync) - tests needing "the" seeded archetype
    /// specifically target Reiklander by name rather than assuming there's only one.</summary>
    private async Task<WarbandArchetype> GetReiklandersAsync(string languageCode = "en") =>
        (await _library.GetWarbandArchetypesAsync(languageCode)).Single(a => a.Name is "Reiklander Mercenaries" or "Mercenaires Reiklander");

    [Fact]
    public async Task Database_SeedsReiklanderMercenariesOnFirstLaunch()
    {
        var reiklanders = await GetReiklandersAsync();
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
        var reiklanders = await GetReiklandersAsync("fr");
        Assert.Equal("Mercenaires Reiklander", reiklanders.Name);
    }

    [Fact]
    public async Task Database_SeedsFourWarbandsTotal()
    {
        var archetypes = await _library.GetWarbandArchetypesAsync("en");
        Assert.Equal(5, archetypes.Count);
        Assert.Contains(archetypes, a => a.Name == "Undead");
        Assert.Contains(archetypes, a => a.Name == "Dwarf Treasure Hunters");
        Assert.Contains(archetypes, a => a.Name == "Averland Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Ostland Mercenaries");

        var spells = await _library.GetSpellsAsync("en");
        Assert.Equal(12, spells.Count);
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Necromancy"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Prayers of Taal"));

        var necromancer = (await _library.GetWarriorArchetypesAsync(
            archetypes.Single(a => a.Name == "Undead").Id, "en")).Single(w => w.Name == "Necromancer");
        Assert.True(necromancer.IsSpellcaster);

        var undead = archetypes.Single(a => a.Name == "Undead");
        Assert.Contains(undead.MagicSchools, s => s.Name == "Necromancy");

        var dwarfAxe = (await _library.GetEquipmentItemsAsync("en")).Single(e => e.Name == "Dwarf axe");
        Assert.Single(dwarfAxe.RestrictedToWarbandArchetypeIds);
    }

    /// <summary>"Leader" is attached from 4 different warbands' JSON files (Averlanders/Captain,
    /// Ostlanders/Elder, Dwarf Treasure Hunters/Noble, Undead/Vampire) - find-or-create by English Name
    /// (see AppDatabase.FindOrCreateSpecialRuleAsync) must resolve them all to the SAME catalog row
    /// instead of 4 duplicates, and each archetype's WarriorArchetype.SpecialRules must carry it.</summary>
    [Fact]
    public async Task SpecialRules_SharedAcrossWarbands_ResolveToSameCatalogRow()
    {
        var allRules = await _library.GetSpecialRulesAsync("en");
        Assert.Single(allRules, r => r.Name == "Leader");
        var leaderId = allRules.Single(r => r.Name == "Leader").Id;

        var archetypes = await _library.GetWarbandArchetypesAsync("en");
        var leaderBearers = new[] { ("Averland Mercenaries", "Captain"), ("Ostland Mercenaries", "Elder"),
            ("Dwarf Treasure Hunters", "Noble"), ("Undead", "Vampire") };

        foreach (var (bandName, warriorName) in leaderBearers)
        {
            var band = archetypes.Single(a => a.Name == bandName);
            var warrior = (await _library.GetWarriorArchetypesAsync(band.Id, "en")).Single(w => w.Name == warriorName);
            Assert.Contains(warrior.SpecialRules, r => r.Id == leaderId);
        }

        // Band-wide rules (not tied to one warrior type) live on the WarbandArchetype itself.
        var dwarfs = archetypes.Single(a => a.Name == "Dwarf Treasure Hunters");
        Assert.Contains(dwarfs.SpecialRules, r => r.Name == "Hard to Kill");
        var ostlanders = archetypes.Single(a => a.Name == "Ostland Mercenaries");
        Assert.Contains(ostlanders.SpecialRules, r => r.Name == "Self-Reliant");
    }

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
