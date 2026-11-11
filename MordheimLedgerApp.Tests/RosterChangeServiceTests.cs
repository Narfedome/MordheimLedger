using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Recrutement, renforts, renvois, ventes et réallocation de la Fin de partie (RosterChangeService) -
/// base fraîchement seedée par test, comme WarbandMutationTests.</summary>
public class RosterChangeServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly RosterChangeService _service;

    public RosterChangeServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _service = new RosterChangeService(_warbands);
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* déjà signalé par le test */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<(Warband Warband, List<WarriorArchetype> Archetypes)> WarbandAsync()
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Reiklander Mercenaries");
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);
        return (warband, await _library.GetWarriorArchetypesAsync(archetype.Id, "en"));
    }

    private async Task<EquipmentItem> ItemAsync(string name) => (await _library.GetEquipmentItemsAsync("en")).First(i => i.Name == name);

    private async Task<Warrior> ReloadAsync(Warband warband, int id) => (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == id);

    [Fact]
    public async Task NewHenchmanGroup_CarriesItsEquipmentPerModel()
    {
        var (warband, archetypes) = await WarbandAsync();
        var sword = await ItemAsync("Sword");

        var group = await _service.RecruitAsync(warband.Id,
            new RecruitDraft(archetypes.First(a => !a.IsHero), "Reik Blades", 3, [(sword, null)], []));

        var reloaded = await ReloadAsync(warband, group.Id);
        Assert.Equal(3, reloaded.HeadCount);
        Assert.Equal(1, Assert.Single(reloaded.Equipment).Quantity); // « Épée x1 », pas x3
    }

    [Fact]
    public async Task NewHero_GetsHisEquipmentAndMutations()
    {
        var (warband, archetypes) = await WarbandAsync();
        var sword = await ItemAsync("Sword");
        var mutation = (await _library.GetMutationsAsync("en")).First();

        var hero = await _service.RecruitAsync(warband.Id,
            new RecruitDraft(archetypes.First(a => a.IsHero), "Gunther", 1, [(sword, null)], [mutation]));

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal("Gunther", reloaded.Name);
        Assert.Single(reloaded.Equipment);
        Assert.Equal(mutation.Id, Assert.Single(reloaded.Mutations).Item.Id);
    }

    [Fact]
    public async Task TopUp_AddsModelsWithoutAddingEquipmentLines()
    {
        var (warband, archetypes) = await WarbandAsync();
        var sword = await ItemAsync("Sword");
        var group = await _service.RecruitAsync(warband.Id,
            new RecruitDraft(archetypes.First(a => !a.IsHero), "Reik Blades", 2, [(sword, null)], []));
        group = await ReloadAsync(warband, group.Id);

        await _service.TopUpAsync(group, 2);

        var reloaded = await ReloadAsync(warband, group.Id);
        Assert.Equal(4, reloaded.HeadCount);
        Assert.Equal(1, Assert.Single(reloaded.Equipment).Quantity);
    }

    [Fact]
    public async Task PartialDismissal_LowersTheHeadCountOnly()
    {
        var (warband, archetypes) = await WarbandAsync();
        var group = await _service.RecruitAsync(warband.Id,
            new RecruitDraft(archetypes.First(a => !a.IsHero), "Reik Blades", 3, [(await ItemAsync("Sword"), null)], []));
        group = await ReloadAsync(warband, group.Id);

        Assert.True(await _service.DismissAsync(group, 1));

        var reloaded = await ReloadAsync(warband, group.Id);
        Assert.Equal(2, reloaded.HeadCount);
        Assert.Equal(WarriorStatus.Active, reloaded.Status);
        Assert.Single(reloaded.Equipment);
    }

    [Fact]
    public async Task FullDismissal_StripsEquipment_AndRetiresInsteadOfDeleting()
    {
        var (warband, archetypes) = await WarbandAsync();
        var hero = await _service.RecruitAsync(warband.Id,
            new RecruitDraft(archetypes.First(a => a.IsHero), "Gunther", 1, [(await ItemAsync("Sword"), null)], []));
        hero = await ReloadAsync(warband, hero.Id);

        Assert.True(await _service.DismissAsync(hero, 5)); // plus que l'effectif : renvoi total

        var reloaded = await ReloadAsync(warband, hero.Id);
        Assert.Equal(WarriorStatus.Retired, reloaded.Status);
        Assert.Empty(reloaded.Equipment);
    }

    [Fact]
    public async Task SellingPartOfACarriedStack_KeepsTheRestWithItsBlessing()
    {
        var (warband, archetypes) = await WarbandAsync();
        var hero = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => a.IsHero), "Gunther");
        var carried = await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("Dagger"), quantity: 3);
        var blessed = (await _library.GetSpecialRulesAsync("en")).Single(r => r.Name == "Blessed Weapon");
        await _warbands.SetWarriorEquipmentBlessingRuleAsync(carried.Id, blessed.Id);
        var line = Assert.Single((await ReloadAsync(warband, hero.Id)).Equipment);

        await _service.SellCarriedAsync(line, 2);

        var rest = Assert.Single((await ReloadAsync(warband, hero.Id)).Equipment);
        Assert.Equal(1, rest.Quantity);
        Assert.Equal("Blessed Weapon", rest.BlessingRule?.Name);
    }

    [Fact]
    public async Task Reallocation_WritesRemovedReducedAndArrivedLines()
    {
        var (warband, archetypes) = await WarbandAsync();
        var hero = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => a.IsHero), "Gunther");
        await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("Dagger"), quantity: 2);
        await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("Axe"));
        hero = await ReloadAsync(warband, hero.Id);
        var original = hero.Equipment.ToDictionary(e => e.Id, e => e.Quantity);

        // En mémoire, comme l'étape Réallouer : une dague donnée ailleurs, la hache rangée, une épée reçue.
        hero.Equipment.Single(e => e.Item.Name == "Dagger").Quantity = 1;
        hero.Equipment.RemoveAll(e => e.Item.Name == "Axe");
        hero.Equipment.Add(new WarriorEquipment { Id = -1, WarriorId = hero.Id, Item = await ItemAsync("Sword"), Quantity = 1 });

        var arrived = await _service.ApplyEquipmentChangesAsync(hero, original);

        Assert.Equal("Sword", Assert.Single(arrived).Item.Name);
        var gear = (await ReloadAsync(warband, hero.Id)).Equipment.ToDictionary(e => e.Item.Name, e => e.Quantity);
        Assert.Equal(new Dictionary<string, int> { ["Dagger"] = 1, ["Sword"] = 1 }, gear);
    }

    [Fact]
    public async Task Treasury_IsDebitedAndCredited()
    {
        var (warband, _) = await WarbandAsync();
        var treasury = warband.Treasury;

        await _service.AdjustTreasuryAsync(warband, -35);
        await _service.AdjustTreasuryAsync(warband, 10);

        Assert.Equal(treasury - 25, (await _warbands.GetWarbandAsync(warband.Id))!.Treasury);
    }

    // --- Ajouts après le passage Stryker du 2026-11-11 (mutants survivants) ---

    [Fact]
    public async Task DismissingNobody_ChangesNothing()
    {
        var (warband, archetypes) = await WarbandAsync();
        var group = await _service.RecruitAsync(warband.Id, new RecruitDraft(archetypes.First(a => !a.IsHero), "Reik Blades", 3, [], []));
        group = await ReloadAsync(warband, group.Id);

        Assert.False(await _service.DismissAsync(group, 0));
        Assert.Equal(3, (await ReloadAsync(warband, group.Id)).HeadCount);
    }

    [Fact]
    public async Task SellingAWholeStack_LeavesNoLine()
    {
        var (warband, archetypes) = await WarbandAsync();
        var hero = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => a.IsHero), "Gunther");
        await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("Dagger"), quantity: 2);
        var line = Assert.Single((await ReloadAsync(warband, hero.Id)).Equipment);

        await _service.SellCarriedAsync(line, 2);

        Assert.Empty((await ReloadAsync(warband, hero.Id)).Equipment);
    }

    [Fact]
    public async Task Reallocation_UnchangedLine_KeepsItsRow_AndALineReducedToZeroDisappears()
    {
        var (warband, archetypes) = await WarbandAsync();
        var hero = await _warbands.RecruitWarriorAsync(warband.Id, archetypes.First(a => a.IsHero), "Gunther");
        await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("Dagger"));
        await _warbands.AddWarriorEquipmentAsync(hero.Id, await ItemAsync("Axe"));
        hero = await ReloadAsync(warband, hero.Id);
        var original = hero.Equipment.ToDictionary(e => e.Id, e => e.Quantity);
        var daggerId = hero.Equipment.Single(e => e.Item.Name == "Dagger").Id;
        hero.Equipment.Single(e => e.Item.Name == "Axe").Quantity = 0;

        await _service.ApplyEquipmentChangesAsync(hero, original);

        var reloaded = (await ReloadAsync(warband, hero.Id)).Equipment;
        Assert.Equal(daggerId, Assert.Single(reloaded).Id);
    }
}
