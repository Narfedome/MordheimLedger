using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Captifs et vente de pierre magique en fin de partie (PostBattleSequenceService) - base fraîchement
/// seedée par test, comme les autres services de la Fin de partie.</summary>
public class PostBattleSequenceServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly PostBattleSequenceService _service;

    public PostBattleSequenceServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _service = new PostBattleSequenceService(_warbands);
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* déjà signalé par le test */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<Warband> WarbandAsync(string archetypeName = "Reiklander Mercenaries")
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == archetypeName);
        return await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);
    }

    private async Task<List<WarriorArchetype>> AllWarriorArchetypesAsync() =>
        await _library.GetWarriorArchetypesAsync((await _library.GetWarbandArchetypesAsync("en")).Select(a => a.Id), "en");

    private async Task<WarriorArchetype> ZombieAsync() => (await AllWarriorArchetypesAsync()).First(a => a.OfficialId == OfficialIds.ZombieWarrior);

    private async Task<WarriorArchetype> ArchetypeAsync(string name) => (await AllWarriorArchetypesAsync()).First(a => a.Name == name);

    private Task<List<Warrior>> RosterAsync(Warband warband) => _warbands.GetWarriorsAsync(warband.Id, "en");

    [Fact]
    public async Task Captives_RansomAndSale_AddTheirGold()
    {
        var warband = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.ApplyCaptivesAsync(warband, [new(CaptiveFate.Ransomed, 30), new(CaptiveFate.SoldToSlavers, 12), new(CaptiveFate.Ransomed, -5)],
            [], null, null);

        Assert.Equal(treasury + 42, (await _warbands.GetWarbandAsync(warband.Id))!.Treasury);
    }

    [Fact]
    public async Task Captives_KilledForZombie_JoinTheExistingZombieGroup()
    {
        var warband = await WarbandAsync();
        var zombie = await ZombieAsync();
        var group = await _warbands.RecruitWarriorAsync(warband.Id, zombie, "Zombies", headCount: 2);

        await _service.ApplyCaptivesAsync(warband, [new(CaptiveFate.KilledForZombie), new(CaptiveFate.KilledForZombie)], [group], zombie, null);

        var zombies = (await RosterAsync(warband)).Where(w => w.WarriorArchetypeId == zombie.Id).ToList();
        Assert.Equal(4, Assert.Single(zombies).HeadCount);
    }

    [Fact]
    public async Task Captives_TwoZombiesWithoutAGroup_FormOneGroupOfTwo()
    {
        var warband = await WarbandAsync();
        var zombie = await ZombieAsync();

        await _service.ApplyCaptivesAsync(warband, [new(CaptiveFate.KilledForZombie), new(CaptiveFate.KilledForZombie)], [], zombie, null);

        var zombies = (await RosterAsync(warband)).Where(w => w.WarriorArchetypeId == zombie.Id).ToList();
        Assert.Equal(2, Assert.Single(zombies).HeadCount);
    }

    [Fact]
    public async Task Captives_ZombieGroupWipedThisBattle_IsNotRevived()
    {
        var warband = await WarbandAsync();
        var zombie = await ZombieAsync();
        var wiped = await _warbands.RecruitWarriorAsync(warband.Id, zombie, "Zombies", headCount: 1);
        await _warbands.DeleteWarriorAsync(wiped.Id);
        wiped.HeadCount = 0; // anéanti plus tôt dans la même Fin de partie

        await _service.ApplyCaptivesAsync(warband, [new(CaptiveFate.KilledForZombie)], [wiped], zombie, null);

        var zombies = (await RosterAsync(warband)).Where(w => w.WarriorArchetypeId == zombie.Id).ToList();
        Assert.Equal(1, Assert.Single(zombies).HeadCount);
        Assert.NotEqual(wiped.Id, zombies[0].Id);
    }

    [Fact]
    public async Task Captives_KilledForZombie_WithoutTheZombieType_DoesNothing()
    {
        var warband = await WarbandAsync();
        var before = (await RosterAsync(warband)).Count;

        await _service.ApplyCaptivesAsync(warband, [new(CaptiveFate.KilledForZombie)], [], null, null);

        Assert.Equal(before, (await RosterAsync(warband)).Count);
    }

    [Fact]
    public async Task Captives_Sacrificed_GiveOneXpEachToTheLeader()
    {
        var warband = await WarbandAsync();
        var leader = await _warbands.RecruitWarriorAsync(warband.Id, await ArchetypeAsync("Mercenary Captain"), "Gunther");
        var xp = leader.Experience;

        var sacrifices = await _service.ApplyCaptivesAsync(warband, [new(CaptiveFate.SacrificedForXp), new(CaptiveFate.SacrificedForXp)], [], null, leader);

        Assert.Equal(2, sacrifices);
        Assert.Equal(xp + 2, (await RosterAsync(warband)).Single(w => w.Id == leader.Id).Experience);
    }

    [Fact]
    public async Task Captives_Sacrificed_WithoutALeader_CountsNothing()
    {
        var warband = await WarbandAsync();

        Assert.Equal(0, await _service.ApplyCaptivesAsync(warband, [new(CaptiveFate.SacrificedForXp)], [], null, null));
    }

    [Fact]
    public async Task Wyrdstone_Sale_MovesShardsToGold()
    {
        var warband = await WarbandAsync();
        warband.WyrdstoneShards = 5;
        var treasury = warband.Treasury;

        var sold = await _service.SellWyrdstoneAsync(warband, 3, 85);

        var saved = (await _warbands.GetWarbandAsync(warband.Id))!;
        Assert.Equal(3, sold);
        Assert.Equal(2, saved.WyrdstoneShards);
        Assert.Equal(treasury + 85, saved.Treasury);
    }

    [Fact]
    public async Task Wyrdstone_Sale_NeverSellsMoreThanTheStock()
    {
        var warband = await WarbandAsync();
        warband.WyrdstoneShards = 2;

        Assert.Equal(2, await _service.SellWyrdstoneAsync(warband, 5, 70));
        Assert.Equal(0, (await _warbands.GetWarbandAsync(warband.Id))!.WyrdstoneShards);
    }

    [Fact]
    public async Task Wyrdstone_Sale_OfNothing_ChangesNothing()
    {
        var warband = await WarbandAsync();
        warband.WyrdstoneShards = 4;
        var treasury = warband.Treasury;

        Assert.Equal(0, await _service.SellWyrdstoneAsync(warband, 0, 50));
        Assert.Equal(treasury, warband.Treasury);
    }

    private async Task<Warrior> HiredSwordAsync(Warband warband)
    {
        var hiredSword = (await _library.GetHiredSwordsAsync("en")).First();
        return await _warbands.RecruitHiredSwordAsync(warband.Id, hiredSword, "Ogre", []);
    }

    [Fact]
    public async Task Scenario_GoldAndShards_AreAdded_EvenANegativeGoldAmount()
    {
        var warband = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.ApplyScenarioRewardsAsync(warband, -10, 2);

        var saved = (await _warbands.GetWarbandAsync(warband.Id))!;
        Assert.Equal(treasury - 10, saved.Treasury);
        Assert.Equal(2, saved.WyrdstoneShards);
    }

    [Fact]
    public async Task Upkeep_EachChoice_IsApplied()
    {
        var warband = await WarbandAsync();
        warband.WyrdstoneShards = 3;
        var treasury = warband.Treasury;
        var paid = await HiredSwordAsync(warband);
        var paidInShards = await HiredSwordAsync(warband);
        var refused = await HiredSwordAsync(warband);
        var prepaid = await HiredSwordAsync(warband);
        prepaid.HiredSwordUpkeepPrepaid = true;
        await _warbands.SaveWarriorAsync(prepaid);

        await _service.ApplyUpkeepAsync(warband, [
            new(paid, UpkeepChoice.PayGold, 15), new(paidInShards, UpkeepChoice.PayWyrdstone, 1),
            new(refused, UpkeepChoice.Refuse, 20), new(prepaid, UpkeepChoice.Prepaid, 0)]);

        var saved = (await _warbands.GetWarbandAsync(warband.Id))!;
        Assert.Equal(treasury - 15, saved.Treasury);
        Assert.Equal(2, saved.WyrdstoneShards);
        var roster = await RosterAsync(warband);
        Assert.DoesNotContain(roster, w => w.Id == refused.Id);
        Assert.False(roster.Single(w => w.Id == prepaid.Id).HiredSwordUpkeepPrepaid);
        Assert.Contains(roster, w => w.Id == paid.Id);
    }

    [Fact]
    public async Task Hire_RecruitsAndDebitsTheHireCost()
    {
        var warband = await WarbandAsync();
        var treasury = warband.Treasury;
        var hiredSword = (await _library.GetHiredSwordsAsync("en")).First(h => h.HireCost > 0);

        var recruited = await _service.HireAsync(warband, hiredSword, "Grim", []);

        Assert.Equal(treasury - hiredSword.HireCost, (await _warbands.GetWarbandAsync(warband.Id))!.Treasury);
        Assert.Contains(await RosterAsync(warband), w => w.Id == recruited.Id && w.Name == "Grim");
    }

    [Fact]
    public async Task Wanderers_Leave_AndOnlyThoseWithACooldownSetIt()
    {
        var warband = await WarbandAsync();
        var personae = await _library.GetDramatisPersonaeAsync("en");
        var first = await _warbands.RecruitDramatisPersonaAsync(warband.Id, personae[0], personae[0].Name, [], []);
        var second = await _warbands.RecruitDramatisPersonaAsync(warband.Id, personae[1], personae[1].Name, [], []);

        await _service.DepartWanderersAsync(warband.Id, [new(first, personae[0].Id), new(second, null)]);

        var roster = await RosterAsync(warband);
        Assert.DoesNotContain(roster, w => w.Id == first.Id || w.Id == second.Id);
        Assert.Equal([personae[0].Id], await _warbands.GetDramatisPersonaCooldownIdsAsync(warband.Id));
    }

    [Fact]
    public async Task Pair_PaymentIsDebited_ZeroDoesNothing()
    {
        var warband = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.PayPairAsync(warband, 0);
        await _service.PayPairAsync(warband, 40);

        Assert.Equal(treasury - 40, (await _warbands.GetWarbandAsync(warband.Id))!.Treasury);
    }

    [Fact]
    public async Task Duel_Lost_KillsTheLeader()
    {
        var warband = await WarbandAsync();
        var leader = await _warbands.RecruitWarriorAsync(warband.Id, await ArchetypeAsync("Mercenary Captain"), "Gunther");

        await _service.LoseDuelAsync(leader);

        Assert.Equal(WarriorStatus.Dead, (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == leader.Id).Status);
    }

    [Fact]
    public async Task Sickness_CountsDown_RecoveredOnesLoseTemporaryInjuries()
    {
        var warband = await WarbandAsync();
        var captain = await ArchetypeAsync("Mercenary Captain");
        var recovering = await _warbands.RecruitWarriorAsync(warband.Id, captain, "Otto");
        var stillSick = await _warbands.RecruitWarriorAsync(warband.Id, captain, "Hans");
        var injury = (await _library.GetInjuriesAsync("en")).First();
        await _warbands.AddWarriorInjuryAsync(recovering.Id, injury, isTemporary: true);
        recovering.Status = stillSick.Status = WarriorStatus.Sick;
        recovering.SickGamesRemaining = 1;
        stillSick.SickGamesRemaining = 2;

        await _service.ApplySicknessAsync([recovering, stillSick], null, null);

        var roster = await RosterAsync(warband);
        var otto = roster.Single(w => w.Id == recovering.Id);
        Assert.Equal(WarriorStatus.Active, otto.Status);
        Assert.Empty(otto.Injuries);
        var hans = roster.Single(w => w.Id == stillSick.Id);
        Assert.Equal(WarriorStatus.Sick, hans.Status);
        Assert.Equal(1, hans.SickGamesRemaining);
    }

    [Fact]
    public async Task Sickness_WellAndPit_SetSickAndDead()
    {
        var warband = await WarbandAsync();
        var captain = await ArchetypeAsync("Mercenary Captain");
        var sick = await _warbands.RecruitWarriorAsync(warband.Id, captain, "Karl");
        var devoured = await _warbands.RecruitWarriorAsync(warband.Id, captain, "Fritz");

        await _service.ApplySicknessAsync([], sick, devoured);

        var roster = await RosterAsync(warband);
        Assert.Equal(WarriorStatus.Sick, roster.Single(w => w.Id == sick.Id).Status);
        Assert.Equal(1, roster.Single(w => w.Id == sick.Id).SickGamesRemaining);
        Assert.Equal(WarriorStatus.Dead, roster.Single(w => w.Id == devoured.Id).Status);
    }

    [Theory]
    [InlineData(25, 0)]
    [InlineData(0, 3)]
    public async Task Scenario_GoldAlone_OrShardsAlone_AreSaved(int gold, int shards)
    {
        var warband = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.ApplyScenarioRewardsAsync(warband, gold, shards);

        var saved = (await _warbands.GetWarbandAsync(warband.Id))!;
        Assert.Equal(treasury + gold, saved.Treasury);
        Assert.Equal(shards, saved.WyrdstoneShards);
    }

    [Fact]
    public async Task Upkeep_PaidOnlyInWyrdstone_IsSaved()
    {
        var warband = await WarbandAsync();
        warband.WyrdstoneShards = 3;
        await _warbands.SaveWarbandAsync(warband);
        var persona = await HiredSwordAsync(warband);

        await _service.ApplyUpkeepAsync(warband, [new(persona, UpkeepChoice.PayWyrdstone, 1)]);

        Assert.Equal(2, (await _warbands.GetWarbandAsync(warband.Id))!.WyrdstoneShards);
    }

    [Fact]
    public async Task Pair_NegativeAmount_NeverGivesGold()
    {
        var warband = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.PayPairAsync(warband, -30);

        Assert.Equal(treasury, warband.Treasury);
    }
}
