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

    private async Task<EquipmentItem> ItemAsync(string officialId) =>
        (await _library.GetEquipmentItemsAsync("en")).Single(i => i.OfficialId == officialId);

    [Fact]
    public async Task OfficialContent_SingleUseItems()
    {
        var races = await RaceIdsAsync();
        var crimson = await ItemAsync("equipment.crimson-shade");
        Assert.Equal(SingleUseTiming.BeforeBattle, crimson.SingleUse!.Timing);
        Assert.Equal(3, crimson.SingleUse.StatModifiers.Single(m => m.Field == CharacteristicField.Initiative).DiceSides);
        Assert.Equal((2, 6), SingleUseRules.AfterBattleDice(crimson.SingleUse));
        Assert.Equal(AfterBattleOutcomeKind.Addiction, SingleUseRules.ResolveAfterBattle(crimson.SingleUse, 2)!.Kind);
        Assert.Equal(new[] { races["race.undead"], races["race.possessed"] }.Order(), crimson.NoEffectOnRaceIds.Order());

        Assert.Equal(new[] { races["race.elf"] }, (await ItemAsync("equipment.bugman-s-ale")).NoEffectOnRaceIds);
        Assert.Equal(SingleUseTarget.Warband, (await ItemAsync("equipment.bugman-s-ale")).SingleUse!.Target);
        Assert.Equal(SingleUseTarget.Weapon, (await ItemAsync("equipment.dark-venom")).SingleUse!.Target);
        Assert.True(SingleUseRules.IsPreChecked(await ItemAsync("equipment.garlic")));
        Assert.True(SingleUseRules.IsAskedAfterBattle(await ItemAsync("equipment.healing-herbs")));
        Assert.Null((await ItemAsync("equipment.sword")).SingleUse);

        // Objet déclaré dans un fichier de bande.
        var madCap = await ItemAsync("equipment.mad-cap-mushrooms");
        Assert.Equal("rule.stupidity", SingleUseRules.ResolveAfterBattle(madCap.SingleUse!, 1)!.SpecialRuleId);
        Assert.Contains(races["race.possessed"], madCap.NoEffectOnRaceIds);
        Assert.Contains(races["race.undead"], (await ItemAsync("equipment.blessed-water")).NoEffectOnRaceIds);
    }

    [Fact]
    public async Task DeleteRace_RemovesItFromItemsWithoutEffect()
    {
        var races = await RaceIdsAsync();
        var undead = races["race.undead"];
        Assert.True((await _library.GetRaceUsageAsync(undead)).EquipmentItems >= 4);

        await _library.DeleteRaceAsync(undead);

        Assert.Equal(new[] { races["race.possessed"] }, (await ItemAsync("equipment.crimson-shade")).NoEffectOnRaceIds);
        Assert.DoesNotContain(undead, (await ItemAsync("equipment.bugman-s-ale")).NoEffectOnRaceIds);
        Assert.Equal(0, (await _library.GetRaceUsageAsync(undead)).EquipmentItems);
        Assert.Equal(1, (await _library.GetRaceUsageAsync(races["race.elf"])).EquipmentItems);
    }

    [Fact]
    public async Task SaveEquipmentItem_KeepsSingleUseAndRaces()
    {
        var races = await RaceIdsAsync();
        var item = new EquipmentItem
        {
            Name = "Potion",
            Category = EquipmentCategory.Consumable,
            SingleUse = new SingleUseProfile { Timing = SingleUseTiming.DuringBattle },
            NoEffectOnRaceIds = { races["race.dwarf"] },
        };
        await _library.SaveEquipmentItemAsync(item, "en");

        var saved = (await _library.GetEquipmentItemsAsync("en")).Single(i => i.Id == item.Id);
        Assert.Equal(SingleUseTiming.DuringBattle, saved.SingleUse!.Timing);
        Assert.Equal(new[] { races["race.dwarf"] }, saved.NoEffectOnRaceIds);
    }

    [Fact]
    public async Task OfficialContent_SilkClothesLostByTheLeaderOnOneToThree()
    {
        var silk = await ItemAsync("equipment.cathayan-silk-clothes");
        Assert.Equal(3, silk.LostAfterOutOfActionOn);
        Assert.True(silk.LostAfterOutOfActionLeaderOnly);
        Assert.Null((await ItemAsync("equipment.sword")).LostAfterOutOfActionOn);

        var mapped = silk.ToEntity().ToModel(new Dictionary<string, string>());
        Assert.Equal(3, mapped.LostAfterOutOfActionOn);
        Assert.True(mapped.LostAfterOutOfActionLeaderOnly);
    }
}
