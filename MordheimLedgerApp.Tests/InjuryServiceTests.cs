using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Blessures de la Fin de partie appliquées en base (InjuryService) et recherche dans le catalogue
/// (InjuryCatalogLookup) - base fraîchement seedée par test, comme WarbandMutationTests.</summary>
public class InjuryServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly InjuryService _service;

    public InjuryServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _service = new InjuryService(_warbands, _library, "en");
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* déjà signalé par le test */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<(Warband Warband, Warrior Hero, Warrior Group)> WarbandAsync(int groupSize = 3)
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Reiklander Mercenaries");
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);
        var archetypes = await _library.GetWarriorArchetypesAsync(archetype.Id, "en");
        var hero = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => a.IsHero), "Gunther");
        var group = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => !a.IsHero), "Reik Blades", groupSize);
        var axe = (await _library.GetEquipmentItemsAsync("en")).First(i => i.Name == "Axe");
        await _warbands.AddWarriorEquipmentAsync(hero.Id, axe);
        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        return (warband, roster.Single(w => w.Id == hero.Id), roster.Single(w => w.Id == group.Id));
    }

    private async Task<Warrior> ReloadAsync(Warband warband, int id) => (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == id);

    // --- Catalogue -------------------------------------------------------------------------------

    [Theory]
    [InlineData("22", 22, true)]
    [InlineData("16, 21", 21, true)]
    [InlineData("11-15", 13, true)]
    [InlineData("11-15", 16, false)]
    [InlineData("62-63", 63, true)]
    [InlineData("", 22, false)]
    [InlineData(null, 22, false)]
    public void RollRangeMatches_ReadsSinglesListsAndRanges(string? range, int roll, bool expected) =>
        Assert.Equal(expected, InjuryCatalogLookup.RollRangeMatches(range, roll));

    [Fact]
    public async Task Resolve_OfficialRoll_ReturnsTheCatalogEntry_WithoutCreatingOne()
    {
        var catalogSize = (await _library.GetInjuriesAsync("en")).Count;

        var injury = await _service.ResolveInjuryAsync(22, isHero: true, "ignored");

        Assert.True(InjuryCatalogLookup.RollRangeMatches(injury.RollRange, 22));
        Assert.Equal(catalogSize, (await _library.GetInjuriesAsync("en")).Count);
    }

    [Fact]
    public async Task Resolve_BranchingRoll_PicksTheBranchMatchingTheSubRoll()
    {
        var light = await _service.ResolveInjuryAsync(23, isHero: true, "x", branchSubRoll: 4);
        var severe = await _service.ResolveInjuryAsync(23, isHero: true, "x", branchSubRoll: 1);

        Assert.NotEqual(light.Id, severe.Id);
        Assert.True(InjuryCatalogLookup.RollRangeMatches(severe.BranchRange, 1));
    }

    [Fact]
    public async Task Resolve_UnknownRoll_CreatesTheFallbackOnlyOnce()
    {
        var first = await _service.ResolveInjuryAsync(-1, isHero: true, "Blessure maison");
        var second = await _service.ResolveInjuryAsync(-1, isHero: true, "Blessure maison");

        Assert.Equal("Blessure maison", first.Name);
        Assert.Equal(first.Id, second.Id);
    }

    // --- Effets ----------------------------------------------------------------------------------

    [Fact]
    public async Task CharacteristicPenalty_IsAppliedAndPersisted()
    {
        var (warband, hero, _) = await WarbandAsync();
        var movement = hero.Movement;
        var injury = await _service.ResolveInjuryAsync(22, true, "Leg wound");

        var changed = await _service.RecordInjuryAsync(hero, injury, new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.CharacteristicPenalty, Field = CharacteristicField.Movement });
        await _service.FinishWarriorAsync(hero, changed);

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal(movement - 1, reloaded.Movement);
        Assert.False(Assert.Single(reloaded.Injuries).IsTemporary);
    }

    [Fact]
    public async Task MissedGames_Accumulate_AndTheInjuryIsTemporary()
    {
        var (warband, hero, _) = await WarbandAsync();
        var injury = await _service.ResolveInjuryAsync(35, true, "Deep wound");

        await _service.RecordInjuryAsync(hero, injury, new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.MissNextGame });
        await _service.RecordInjuryAsync(hero, injury, new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.MissGamesRollD3, Value = 3 });
        await _service.FinishWarriorAsync(hero, true);

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal(WarriorStatus.Sick, reloaded.Status);
        Assert.Equal(4, reloaded.SickGamesRemaining); // cumul (« cumulez tous les effets »), pas remplacement
        Assert.All(reloaded.Injuries, i => Assert.True(i.IsTemporary));
    }

    [Fact]
    public async Task ForcedRetirement_AndExperienceGain()
    {
        var (warband, hero, _) = await WarbandAsync();
        var xp = hero.Experience;

        await _service.ApplyEffectAsync(hero, new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.GainExperience });
        await _service.ApplyEffectAsync(hero, new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.ForcedRetirement });
        await _service.FinishWarriorAsync(hero, true);

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal(xp + 1, reloaded.Experience);
        Assert.Equal(WarriorStatus.Retired, reloaded.Status);
    }

    [Fact]
    public async Task Robbed_LosesStoredEquipment_ButKeepsItemsReallocatedThisGame()
    {
        var (warband, hero, _) = await WarbandAsync();
        var sword = (await _library.GetEquipmentItemsAsync("en")).First(i => i.Name == "Sword");
        hero.Equipment.Add(new WarriorEquipment { Id = -1, WarriorId = hero.Id, Item = sword, Quantity = 1 });

        await _service.ApplyEffectAsync(hero, new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.LoseAllEquipment });

        Assert.Empty((await ReloadAsync(warband, hero.Id)).Equipment);
        Assert.Equal("Sword", Assert.Single(hero.Equipment).Item.Name); // reçue après la blessure, créée plus tard
    }

    [Fact]
    public async Task Hatred_IsRecorded()
    {
        var (warband, hero, _) = await WarbandAsync();

        await _service.AddHatredAsync(hero, null, "Les Pillards du Nord");

        Assert.Single((await ReloadAsync(warband, hero.Id)).Hatreds);
    }

    // --- Capture et fosses ------------------------------------------------------------------------

    [Fact]
    public async Task Captured_Ransomed_PaysAndKeepsEquipment()
    {
        var (warband, hero, _) = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.CaptureAsync(warband, hero, 30);
        await _warbands.SaveWarbandAsync(warband);
        await _service.FinishWarriorAsync(hero, true);

        Assert.Equal(treasury - 30, (await _warbands.GetWarbandAsync(warband.Id))!.Treasury);
        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal(WarriorStatus.Active, reloaded.Status);
        Assert.Single(reloaded.Equipment);
    }

    [Fact]
    public async Task Captured_NotRansomed_IsLostWithHisEquipment()
    {
        var (warband, hero, _) = await WarbandAsync();

        await _service.CaptureAsync(warband, hero, null);
        await _service.FinishWarriorAsync(hero, true);

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal(WarriorStatus.Dead, reloaded.Status);
        Assert.Empty(reloaded.Equipment);
    }

    [Fact]
    public async Task PitFight_Won_GivesGoldAndExperience_Lost_StripsEquipment()
    {
        var (warband, hero, _) = await WarbandAsync();
        var treasury = warband.Treasury;
        var xp = hero.Experience;

        _service.WinPitFight(warband, hero);
        Assert.Equal(treasury + InjuryService.PitFightWinGold, warband.Treasury);
        Assert.Equal(xp + InjuryService.PitFightWinExperience, hero.Experience);

        await _service.LosePitFightAsync(hero);
        Assert.Empty((await ReloadAsync(warband, hero.Id)).Equipment);
    }

    // --- Hommes de main -----------------------------------------------------------------------------

    [Fact]
    public async Task HenchmanDeaths_LowerTheHeadCount_AndLeaveNoInjuryOnTheGroup()
    {
        var (warband, _, group) = await WarbandAsync(groupSize: 3);

        var wiped = InjuryService.RemoveDeadFigures(group, 1);
        await _service.FinishWarriorAsync(group, true);

        Assert.False(wiped);
        var reloaded = await ReloadAsync(warband, group.Id);
        Assert.Equal(2, reloaded.HeadCount);
        Assert.Empty(reloaded.Injuries); // ni « Perdu » ni « Rétablissement complet »
    }

    [Fact]
    public async Task Repair_RemovesOldHenchmanChips_AndKeepsHeroInjuries()
    {
        var (warband, hero, group) = await WarbandAsync();
        var lost = await _service.ResolveInjuryAsync(1, isHero: false, "Dead");
        var recovered = await _service.ResolveInjuryAsync(4, isHero: false, "Full recovery");
        var heroInjury = await _service.ResolveInjuryAsync(41, isHero: true, "Leg Wound");
        await _warbands.AddWarriorInjuryAsync(group.Id, lost);       // puces posées avant le correctif
        await _warbands.AddWarriorInjuryAsync(group.Id, recovered);
        await _warbands.AddWarriorInjuryAsync(hero.Id, heroInjury);

        await _db.RemoveHenchmanInjuryChipsAsync();

        Assert.Empty((await ReloadAsync(warband, group.Id)).Injuries);
        Assert.Single((await ReloadAsync(warband, hero.Id)).Injuries);
    }

    [Fact]
    public void HenchmanDeaths_NegativeCountIsIgnored()
    {
        var group = new Warrior { HeadCount = 2 };

        Assert.False(InjuryService.RemoveDeadFigures(group, -1));
        Assert.Equal(2, group.HeadCount);
    }

    [Fact]
    public async Task HenchmanGroup_Wiped_IsDeleted()
    {
        var (warband, _, group) = await WarbandAsync(groupSize: 2);
        var wiped = InjuryService.RemoveDeadFigures(group, 2);
        await _service.FinishWarriorAsync(group, true);

        Assert.True(wiped);
        Assert.DoesNotContain(await _warbands.GetWarriorsAsync(warband.Id, "en"), w => w.Id == group.Id);
    }

    [Fact]
    public async Task Finish_DoesNotSaveAnUnchangedWarrior()
    {
        var (warband, hero, _) = await WarbandAsync();
        hero.Experience += 5; // modifié en mémoire mais signalé inchangé

        await _service.FinishWarriorAsync(hero, changed: false);

        Assert.NotEqual(hero.Experience, (await ReloadAsync(warband, hero.Id)).Experience);
    }

    // --- Ajouts après le passage Stryker du 2026-11-11 (mutants survivants) ---

    [Theory]
    [InlineData("11-15", 11, true)]
    [InlineData("11-15", 15, true)]
    [InlineData("11-15", 10, false)]
    public void RollRangeMatches_IncludesBothBounds(string range, int roll, bool expected) =>
        Assert.Equal(expected, InjuryCatalogLookup.RollRangeMatches(range, roll));

    private static Injury Row(string name, string roll, string? branch = null) =>
        new() { Name = name, Category = InjuryCategory.Hero, RollRange = roll, BranchRange = branch };

    [Fact]
    public void Find_WithoutSubRoll_PrefersTheGenericRow()
    {
        List<Injury> catalog = [Row("Light", "23", "2-6"), Row("Generic", "23"), Row("Severe", "23", "1")];
        Assert.Equal("Generic", InjuryCatalogLookup.Find(catalog, InjuryCategory.Hero, 23, null)?.Name);
    }

    [Fact]
    public void Find_WithoutSubRoll_FallsBackToABranchRow()
    {
        List<Injury> catalog = [Row("Light", "23", "2-6"), Row("Severe", "23", "1")];
        Assert.Equal("Light", InjuryCatalogLookup.Find(catalog, InjuryCategory.Hero, 23, null)?.Name);
    }

    [Fact]
    public void Find_SubRollMatchingNoBranch_ReturnsNull()
    {
        List<Injury> catalog = [Row("Light", "23", "2-6")];
        Assert.Null(InjuryCatalogLookup.Find(catalog, InjuryCategory.Hero, 23, 1));
    }

    [Fact]
    public async Task Resolve_HenchmanRoll_UsesTheHenchmanTable()
    {
        var injury = await _service.ResolveInjuryAsync(1, isHero: false, "x");
        Assert.Equal(InjuryCategory.Henchman, injury.Category);
    }

    [Fact]
    public async Task Resolve_OfficialRollWins_OverAFallbackNameThatExistsElsewhere()
    {
        var other = (await _library.GetInjuriesAsync("en")).First(i => i.Category == InjuryCategory.Hero && !InjuryCatalogLookup.RollRangeMatches(i.RollRange, 22));

        var injury = await _service.ResolveInjuryAsync(22, isHero: true, other.Name);

        Assert.True(InjuryCatalogLookup.RollRangeMatches(injury.RollRange, 22));
    }

    [Fact]
    public async Task Resolve_UnknownRoll_PersistsTheFallbackInTheCatalog()
    {
        await _service.ResolveInjuryAsync(-1, isHero: true, "Blessure maison");
        Assert.Contains(await _library.GetInjuriesAsync("en"), i => i.Name == "Blessure maison");
    }

    [Theory]
    [InlineData(SeriousInjuryEffectKind.LoseAllEquipment, false)]
    [InlineData(SeriousInjuryEffectKind.GainExperience, true)]
    [InlineData(SeriousInjuryEffectKind.MissNextGame, true)]
    [InlineData(SeriousInjuryEffectKind.MissGamesRollD3, true)]
    [InlineData(SeriousInjuryEffectKind.ForcedRetirement, true)]
    public async Task ApplyEffect_SaysWhetherTheWarriorMustBeSaved(SeriousInjuryEffectKind kind, bool expected)
    {
        var (_, hero, _) = await WarbandAsync();
        Assert.Equal(expected, await _service.ApplyEffectAsync(hero, new SeriousInjuryOutcome { Kind = kind, Value = 2 }));
    }

    [Fact]
    public async Task ApplyEffect_UnhandledKind_ChangesNothing()
    {
        var (_, hero, _) = await WarbandAsync();
        var kinds = Enum.GetValues<SeriousInjuryEffectKind>().Where(k => k is not (SeriousInjuryEffectKind.CharacteristicPenalty
            or SeriousInjuryEffectKind.LoseAllEquipment or SeriousInjuryEffectKind.GainExperience or SeriousInjuryEffectKind.MissNextGame
            or SeriousInjuryEffectKind.MissGamesRollD3 or SeriousInjuryEffectKind.ForcedRetirement)).ToList();
        foreach (var kind in kinds)
            Assert.False(await _service.ApplyEffectAsync(hero, new SeriousInjuryOutcome { Kind = kind }));
    }

    [Fact]
    public async Task DeepWound_UsesTheEnteredRoll_NotARandomOne()
    {
        var (_, hero, _) = await WarbandAsync();
        await _service.ApplyEffectAsync(hero, new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.MissGamesRollD3, Value = 5 });
        Assert.Equal(5, hero.SickGamesRemaining);
    }
}
