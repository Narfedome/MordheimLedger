using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Race par type de guerrier, Franc-Tireur et Dramatis Persona (1.6) : règles, contenu officiel, suppression d'une
/// race citée ailleurs. Base fraîchement seedée par test.</summary>
public class RaceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;

    public RaceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* le test l'a déjà signalé */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Theory]
    [InlineData(3, 7, 3)]
    [InlineData(null, 7, 7)]
    [InlineData(0, 7, 7)]
    [InlineData(null, null, null)]
    [InlineData(null, 0, null)]
    public void EffectiveRace_TypeOverridesWarband(int? own, int? warband, int? expected)
    {
        Assert.Equal(expected, RaceRules.EffectiveRaceId(own, warband));
    }

    [Fact]
    public void IsWithoutEffect_OnlyForListedRaces()
    {
        Assert.True(RaceRules.IsWithoutEffect(4, new[] { 4, 9 }));
        Assert.False(RaceRules.IsWithoutEffect(5, new[] { 4, 9 }));
        Assert.False(RaceRules.IsWithoutEffect(null, new[] { 4, 9 }));
        Assert.False(RaceRules.IsWithoutEffect(4, Array.Empty<int>()));
    }

    [Fact]
    public void RaceUsage_TotalsAllPlaces()
    {
        Assert.Equal(10, new RaceUsage(1, 2, 3, 4, 0).Total);
        Assert.True(new RaceUsage(0, 0, 0, 0, 1).IsUsed);
        Assert.False(new RaceUsage(0, 0, 0, 0, 0).IsUsed);
    }

    private async Task<Dictionary<string, int>> RaceIdsAsync() =>
        (await _library.GetRacesAsync("en")).Where(r => r.OfficialId is not null).ToDictionary(r => r.OfficialId!, r => r.Id);

    private async Task<WarriorArchetype> WarriorAsync(string warbandId, string warriorId)
    {
        var warband = (await _library.GetWarbandArchetypesAsync("en")).Single(w => w.OfficialId == warbandId);
        return (await _library.GetWarriorArchetypesAsync(warband.Id, "en")).Single(w => w.OfficialId == warriorId);
    }

    [Fact]
    public async Task OfficialContent_GivesRacesWhereTheWarbandsRaceIsWrong()
    {
        var races = await RaceIdsAsync();
        Assert.Contains("race.possessed", races.Keys);
        Assert.Contains("race.daemon", races.Keys);

        Assert.Equal(races["race.human"], (await WarriorAsync("warband.undead", "warrior.undead.necromancer")).RaceId);
        Assert.Equal(races["race.human"], (await WarriorAsync("warband.undead", "warrior.undead.dreg")).RaceId);
        Assert.Null((await WarriorAsync("warband.undead", "warrior.undead.zombie")).RaceId);
        Assert.Equal(races["race.possessed"], (await WarriorAsync("warband.cult-of-the-possessed", "warrior.cult-of-the-possessed.the-possessed")).RaceId);
        Assert.Equal(races["race.daemon"], (await WarriorAsync("warband.carnival-of-chaos", "warrior.carnival-of-chaos.plague-bearer")).RaceId);

        var hiredSwords = await _library.GetHiredSwordsAsync("en");
        Assert.Equal(races["race.elf"], hiredSwords.Single(h => h.OfficialId == "hiredsword.elf-ranger").RaceId);
        Assert.Null(hiredSwords.Single(h => h.OfficialId == "hiredsword.ogre-bodyguard").RaceId);
        var personae = await _library.GetDramatisPersonaeAsync("en");
        Assert.Equal(races["race.undead"], personae.Single(p => p.OfficialId == "persona.countess-marianna-chevaux-vampire-assassin").RaceId);
    }

    [Fact]
    public async Task EffectiveRace_ZombieIsUndeadAndNecromancerHuman()
    {
        var races = await RaceIdsAsync();
        var undead = (await _library.GetWarbandArchetypesAsync("en")).Single(w => w.OfficialId == "warband.undead");

        Assert.Equal(races["race.undead"], RaceRules.EffectiveRaceId((await WarriorAsync("warband.undead", "warrior.undead.zombie")).RaceId, undead.RaceId));
        Assert.Equal(races["race.human"], RaceRules.EffectiveRaceId((await WarriorAsync("warband.undead", "warrior.undead.necromancer")).RaceId, undead.RaceId));
    }

    [Fact]
    public async Task DeleteRace_ClearsEveryReference()
    {
        var races = await RaceIdsAsync();
        var human = races["race.human"];
        var usage = await _library.GetRaceUsageAsync(human);
        Assert.True(usage.WarbandArchetypes > 0);
        Assert.True(usage.WarriorArchetypes >= 2);
        Assert.True(usage.HiredSwords > 0);
        Assert.True(usage.DramatisPersonae > 0);

        await _library.DeleteRaceAsync(human);

        Assert.DoesNotContain(await _library.GetRacesAsync("en"), r => r.Id == human);
        Assert.False((await _library.GetRaceUsageAsync(human)).IsUsed);
        Assert.Null((await WarriorAsync("warband.undead", "warrior.undead.necromancer")).RaceId);
        Assert.Equal(0, (await _library.GetWarbandArchetypesAsync("en")).Single(w => w.OfficialId == "warband.reiklanders").RaceId);
    }
}
