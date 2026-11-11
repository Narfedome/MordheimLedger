using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Récompenses de l'Exploration appliquées en base (ExplorationRewardService) - base fraîchement
/// seedée par test, comme WarbandMutationTests.</summary>
public class ExplorationRewardServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly ExplorationRewardService _service;

    public ExplorationRewardServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _service = new ExplorationRewardService(_warbands);
    }

    public void Dispose()
    {
        // Le seed démarre dès la création de la base : un test qui n'y touche pas (ex. photos) fermerait sinon
        // le fichier en plein seed (« utilisé par un autre processus » au moment de le supprimer).
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* le test l'a déjà signalé */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<(Warband Warband, List<WarriorArchetype> Archetypes)> WarbandAsync(string archetypeName = "Reiklander Mercenaries")
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == archetypeName);
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);
        return (warband, await _library.GetWarriorArchetypesAsync(archetype.Id, "en"));
    }

    private async Task<Warband> ReloadWarbandAsync(int id) => (await _warbands.GetWarbandAsync(id))!;

    [Fact]
    public async Task GoldAndShards_AreAddedToTheWarband()
    {
        var (warband, _) = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.ApplyAsync(warband, [], new ExplorationRewards { Gold = 12, WyrdstoneShards = 3 });

        var reloaded = await ReloadWarbandAsync(warband.Id);
        Assert.Equal(treasury + 12, reloaded.Treasury);
        Assert.Equal(3, reloaded.WyrdstoneShards);
    }

    [Fact]
    public async Task PreviousGameReminders_AreConsumed_AndNewOnesReplaceThem()
    {
        var (warband, _) = await WarbandAsync();
        warband.PendingExplorationBonusDie = true;
        warband.NextGameNote = "Ancien rappel";
        await _warbands.SaveWarbandAsync(warband);

        await _service.ApplyAsync(warband, [], new ExplorationRewards());
        var cleared = await ReloadWarbandAsync(warband.Id);
        Assert.False(cleared.PendingExplorationBonusDie);
        Assert.Null(cleared.NextGameNote);

        await _service.ApplyAsync(cleared, [], new ExplorationRewards { GrantsNextExplorationBonusDie = true, NextGameNote = "Nouveau rappel" });
        var set = await ReloadWarbandAsync(warband.Id);
        Assert.True(set.PendingExplorationBonusDie);
        Assert.Equal("Nouveau rappel", set.NextGameNote);
    }

    [Fact]
    public async Task CatacombReroll_IsPermanent()
    {
        var (warband, _) = await WarbandAsync();

        await _service.ApplyAsync(warband, [], new ExplorationRewards { GrantsCatacombReroll = true });
        var after = await ReloadWarbandAsync(warband.Id);
        await _service.ApplyAsync(after, [], new ExplorationRewards());

        Assert.True((await ReloadWarbandAsync(warband.Id)).HasCatacombReroll);
    }

    [Fact]
    public async Task LeaderAndDistributedExperience_AreAdded()
    {
        var (warband, archetypes) = await WarbandAsync();
        var heroes = archetypes.Where(a => a.IsHero).Take(2).ToList();
        var captain = await _warbands.RecruitWarriorAsync(warband.Id, heroes[0], "Gunther");
        var champion = await _warbands.RecruitWarriorAsync(warband.Id, heroes[1], "Ulrike");
        var captainXp = captain.Experience;
        var championXp = champion.Experience;

        await _service.ApplyAsync(warband, [captain, champion], new ExplorationRewards
        {
            LeaderExperience = (captain, 1),
            DistributedExperience = [(captain, 2), (champion, 1)],
        });

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        Assert.Equal(captainXp + 3, roster.Single(w => w.Id == captain.Id).Experience);
        Assert.Equal(championXp + 1, roster.Single(w => w.Id == champion.Id).Experience);
    }

    [Fact]
    public async Task FreeHenchmen_JoinAnExistingGroupOfTheSameType()
    {
        var (warband, archetypes) = await WarbandAsync("Undead");
        var zombie = archetypes.Single(a => a.Name == "Zombie");
        var group = await _warbands.RecruitWarriorAsync(warband.Id, zombie, "Shamblers", headCount: 2);

        await _service.ApplyAsync(warband, [group], new ExplorationRewards { FreeHenchmen = (zombie, 3) });

        var reloaded = Assert.Single(await _warbands.GetWarriorsAsync(warband.Id, "en"));
        Assert.Equal(5, reloaded.HeadCount);
    }

    [Fact]
    public async Task FreeHenchmen_WithoutAGroup_StartANewOne()
    {
        var (warband, archetypes) = await WarbandAsync("Undead");
        var zombie = archetypes.Single(a => a.Name == "Zombie");

        await _service.ApplyAsync(warband, [], new ExplorationRewards { FreeHenchmen = (zombie, 2) });

        var reloaded = Assert.Single(await _warbands.GetWarriorsAsync(warband.Id, "en"));
        Assert.Equal(zombie.Id, reloaded.WarriorArchetypeId);
        Assert.Equal(2, reloaded.HeadCount);
    }

    [Fact]
    public async Task EquippedPrisoner_JoinsTheGroup_AndOnlyHisEquipmentIsPaid()
    {
        var (warband, archetypes) = await WarbandAsync();
        var group = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => !a.IsHero), "Reik Blades", headCount: 2);
        var treasury = warband.Treasury;

        await _service.ApplyAsync(warband, [group], new ExplorationRewards { EquippedHenchman = (group, 15) });

        Assert.Equal(3, Assert.Single(await _warbands.GetWarriorsAsync(warband.Id, "en")).HeadCount);
        Assert.Equal(treasury - 15, (await ReloadWarbandAsync(warband.Id)).Treasury);
    }

    [Fact]
    public async Task WeaponBlessing_IsAttachedToTheChosenWeapon()
    {
        var (warband, archetypes) = await WarbandAsync();
        var hero = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => a.IsHero), "Gunther");
        var axe = (await _library.GetEquipmentItemsAsync("en")).First(i => i.Name == "Axe");
        var carried = await _warbands.AddWarriorEquipmentAsync(hero.Id, axe);
        var blessed = (await _library.GetSpecialRulesAsync("en")).Single(r => r.Name == "Blessed Weapon");

        await _service.ApplyAsync(warband, [hero], new ExplorationRewards { WeaponBlessing = (carried.Id, blessed.Id) });

        var weapon = Assert.Single(Assert.Single(await _warbands.GetWarriorsAsync(warband.Id, "en")).Equipment);
        Assert.Equal("Blessed Weapon", weapon.BlessingRule?.Name);
    }

    [Fact]
    public async Task FreeHiredSword_IsRecruitedWithPrepaidUpkeep_AndKeepsItsName()
    {
        var (warband, _) = await WarbandAsync();
        var hiredSword = (await _library.GetHiredSwordsAsync("en")).First();
        var treasury = warband.Treasury;

        await _service.ApplyAsync(warband, [], new ExplorationRewards { FreeHiredSword = (hiredSword, []) });

        var recruited = Assert.Single(await _warbands.GetWarriorsAsync(warband.Id, "en"));
        Assert.True(recruited.IsHiredSword);
        Assert.True(recruited.HiredSwordUpkeepPrepaid);
        Assert.Equal(hiredSword.Name, recruited.Name);
        Assert.Equal(treasury, (await ReloadWarbandAsync(warband.Id)).Treasury); // offert
    }

    // --- Ajouts après le passage Stryker du 2026-11-11 (mutants survivants) ---

    [Fact]
    public async Task NoCatacombReward_LeavesTheRerollOff()
    {
        var (warband, _) = await WarbandAsync();

        await _service.ApplyAsync(warband, [], new ExplorationRewards { GrantsCatacombReroll = false });

        Assert.False((await ReloadWarbandAsync(warband.Id)).HasCatacombReroll);
    }

    [Fact]
    public async Task LeaderExperience_Alone_IsPersisted()
    {
        var (warband, archetypes) = await WarbandAsync();
        var captain = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => a.IsHero), "Gunther");
        var xp = captain.Experience;

        await _service.ApplyAsync(warband, [captain], new ExplorationRewards { LeaderExperience = (captain, 1) });

        Assert.Equal(xp + 1, (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == captain.Id).Experience);
    }
}
