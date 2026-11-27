using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Objets à usage unique joués : pris au début de partie, consommés et conséquences en Fin de partie,
/// dépendance. Base fraîchement seedée par test.</summary>
public class SingleUseServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly SingleUseService _service;

    public SingleUseServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _service = new SingleUseService(_warbands, _library);
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* le test l'a déjà signalé */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<EquipmentItem> ItemAsync(string officialId) =>
        (await _library.GetEquipmentItemsAsync("en")).Single(i => i.OfficialId == officialId);

    private async Task<(Warband Warband, Warrior Klaus, Warrior Hans)> ReiklandersAsync()
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.OfficialId == "warband.reiklanders");
        var types = await _library.GetWarriorArchetypesAsync(archetype.Id, "en");
        var warband = await _warbands.CreateWarbandAsync("Loups", archetype);
        var champion = types.Single(t => t.OfficialId == "warrior.reiklanders.champion");
        var klaus = await _warbands.RecruitWarriorAsync(warband.Id, champion, "Klaus");
        var hans = await _warbands.RecruitWarriorAsync(warband.Id, champion, "Hans");
        return (warband, klaus, hans);
    }

    private async Task<Warrior> ReloadAsync(Warband warband, int warriorId) =>
        (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == warriorId);

    [Fact]
    public async Task StartGame_StoresTheChoicesForTheSheet()
    {
        var (warband, klaus, _) = await ReiklandersAsync();
        var shade = await ItemAsync("equipment.crimson-shade");
        var carried = await _warbands.AddWarriorEquipmentAsync(klaus.Id, shade);

        await _service.StartGameAsync(warband.Id, new[]
        {
            new GameItemUse
            {
                WarriorId = klaus.Id, EquipmentItemId = shade.Id, WarriorEquipmentId = carried.Id,
                StatModifiers = { new RolledStatModifier(CharacteristicField.Initiative, 2), new RolledStatModifier(CharacteristicField.Movement, 1) },
            },
        });
        // Rejouer le début de partie remplace la sélection.
        await _service.StartGameAsync(warband.Id, new[]
        {
            new GameItemUse
            {
                WarriorId = klaus.Id, EquipmentItemId = shade.Id, WarriorEquipmentId = carried.Id, WeaponName = "Sword", WithoutEffect = true,
                StatModifiers = { new RolledStatModifier(CharacteristicField.Initiative, 3) },
            },
        });

        var use = Assert.Single(await _warbands.GetGameItemUsesAsync(warband.Id));
        Assert.Equal(klaus.Id, use.WarriorId);
        Assert.Equal(new[] { new RolledStatModifier(CharacteristicField.Initiative, 3) }, use.StatModifiers);
        Assert.Equal("Sword", use.WeaponName);
        Assert.True(use.WithoutEffect);
        // Rien n'est retiré tant que la partie n'est pas finie.
        Assert.Contains((await ReloadAsync(warband, klaus.Id)).Equipment, e => e.Id == carried.Id);
    }

    [Fact]
    public async Task FinishGame_ConsumesTakenAndUsedItems_OneCopyAtATime()
    {
        var (warband, klaus, hans) = await ReiklandersAsync();
        var shade = await ItemAsync("equipment.crimson-shade");
        var herbs = await ItemAsync("equipment.healing-herbs");
        var ale = await ItemAsync("equipment.bugman-s-ale");
        var twoDoses = await _warbands.AddWarriorEquipmentAsync(klaus.Id, shade, quantity: 2);
        var herbsCarried = await _warbands.AddWarriorEquipmentAsync(hans.Id, herbs);
        var aleStored = await _warbands.AddWarbandEquipmentAsync(warband.Id, ale);
        await _service.StartGameAsync(warband.Id, new[]
        {
            new GameItemUse { WarriorId = klaus.Id, EquipmentItemId = shade.Id, WarriorEquipmentId = twoDoses.Id },
            new GameItemUse { EquipmentItemId = ale.Id, WarbandEquipmentId = aleStored.Id },
        });

        await _service.FinishGameAsync(warband.Id, await _warbands.GetWarriorsAsync(warband.Id, "en"), new[] { new ConsumedItem(herbsCarried.Id, null) }, Array.Empty<AfterBattleResult>(), "en");

        Assert.Equal(1, (await ReloadAsync(warband, klaus.Id)).Equipment.Single(e => e.Id == twoDoses.Id).Quantity);
        Assert.DoesNotContain((await ReloadAsync(warband, hans.Id)).Equipment, e => e.Id == herbsCarried.Id);
        Assert.DoesNotContain(await _warbands.GetWarbandEquipmentAsync(warband.Id, "en"), e => e.Id == aleStored.Id);
        Assert.Empty(await _warbands.GetGameItemUsesAsync(warband.Id));
    }

    [Fact]
    public async Task FinishGame_AnItemAlreadyGoneIsIgnored()
    {
        var (warband, klaus, _) = await ReiklandersAsync();
        var garlic = await ItemAsync("equipment.garlic");
        var carried = await _warbands.AddWarriorEquipmentAsync(klaus.Id, garlic);
        await _service.StartGameAsync(warband.Id, new[] { new GameItemUse { WarriorId = klaus.Id, EquipmentItemId = garlic.Id, WarriorEquipmentId = carried.Id } });
        await _warbands.RemoveWarriorEquipmentAsync(carried.Id);

        await _service.FinishGameAsync(warband.Id, await _warbands.GetWarriorsAsync(warband.Id, "en"), new[] { new ConsumedItem(null, 999_999), new ConsumedItem(999_999, null), new ConsumedItem(null, null) },
            Array.Empty<AfterBattleResult>(), "en");

        Assert.Empty(await _warbands.GetGameItemUsesAsync(warband.Id));
    }

    [Fact]
    public async Task FinishGame_AppliesConsequences()
    {
        var (warband, klaus, hans) = await ReiklandersAsync();
        var shade = await ItemAsync("equipment.crimson-shade");
        var root = await ItemAsync("equipment.mandrake-root");
        var madCap = await ItemAsync("equipment.mad-cap-mushrooms");
        var initiative = klaus.Initiative;
        var toughness = hans.Toughness;

        await _service.FinishGameAsync(warband.Id, await _warbands.GetWarriorsAsync(warband.Id, "en"), Array.Empty<ConsumedItem>(), new[]
        {
            new AfterBattleResult(klaus.Id, shade.Id, SingleUseRules.ResolveAfterBattle(shade.SingleUse!, 2)!),
            new AfterBattleResult(klaus.Id, shade.Id, SingleUseRules.ResolveAfterBattle(shade.SingleUse!, 12)!),
            new AfterBattleResult(hans.Id, root.Id, SingleUseRules.ResolveAfterBattle(root.SingleUse!, 3)!),
            new AfterBattleResult(hans.Id, madCap.Id, SingleUseRules.ResolveAfterBattle(madCap.SingleUse!, 1)!),
            new AfterBattleResult(hans.Id, madCap.Id, SingleUseRules.ResolveAfterBattle(madCap.SingleUse!, 1)!),
            new AfterBattleResult(999_999, shade.Id, SingleUseRules.ResolveAfterBattle(shade.SingleUse!, 2)!),
        }, "en");

        var klausAfter = await ReloadAsync(warband, klaus.Id);
        Assert.Equal(shade.Id, klausAfter.AddictedToEquipmentItemId);
        Assert.Equal(initiative + 1, klausAfter.Initiative);
        var hansAfter = await ReloadAsync(warband, hans.Id);
        Assert.Equal(toughness - 1, hansAfter.Toughness);
        var stupidity = (await _library.GetSpecialRulesAsync("en")).Single(r => r.OfficialId == "rule.stupidity");
        Assert.Equal(new[] { stupidity.Id }, hansAfter.GainedSpecialRuleIds);
        Assert.Null(hansAfter.AddictedToEquipmentItemId);
    }

    [Fact]
    public async Task FinishGame_ChangesTheRosterObjects_AndSavesThem()
    {
        var (warband, _, hans) = await ReiklandersAsync();
        var root = await ItemAsync("equipment.mandrake-root");
        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var inMemory = roster.Single(w => w.Id == hans.Id);
        var toughness = inMemory.Toughness;

        await _service.FinishGameAsync(warband.Id, roster, Array.Empty<ConsumedItem>(),
            new[] { new AfterBattleResult(hans.Id, root.Id, SingleUseRules.ResolveAfterBattle(root.SingleUse!, 2)!) }, "en");

        Assert.Equal(toughness - 1, inMemory.Toughness);
        Assert.Equal(toughness - 1, (await ReloadAsync(warband, hans.Id)).Toughness);
    }

    [Fact]
    public async Task FinishGame_AddictionAloneIsSaved()
    {
        var (warband, klaus, _) = await ReiklandersAsync();
        var shade = await ItemAsync("equipment.crimson-shade");

        await _service.FinishGameAsync(warband.Id, await _warbands.GetWarriorsAsync(warband.Id, "en"), Array.Empty<ConsumedItem>(),
            new[] { new AfterBattleResult(klaus.Id, shade.Id, SingleUseRules.ResolveAfterBattle(shade.SingleUse!, 3)!) }, "en");

        Assert.Equal(shade.Id, (await ReloadAsync(warband, klaus.Id)).AddictedToEquipmentItemId);
    }

    [Fact]
    public async Task FinishGame_GainsARuleCreatedInTheCodex()
    {
        var (warband, klaus, _) = await ReiklandersAsync();
        var custom = new SpecialRule { Name = "Paranoïa" };
        await _library.SaveSpecialRuleAsync(custom, "en");

        await _service.FinishGameAsync(warband.Id, await _warbands.GetWarriorsAsync(warband.Id, "en"), Array.Empty<ConsumedItem>(),
            new[] { new AfterBattleResult(klaus.Id, 1, new AfterBattleOutcome { Kind = AfterBattleOutcomeKind.GainSpecialRule, SpecialRuleLocalId = custom.Id }) }, "en");

        Assert.Equal(new[] { custom.Id }, (await ReloadAsync(warband, klaus.Id)).GainedSpecialRuleIds);
    }

    [Fact]
    public async Task FinishGame_IncompleteOutcomesWriteNothing()
    {
        var (warband, klaus, _) = await ReiklandersAsync();
        var before = await ReloadAsync(warband, klaus.Id);

        await _service.FinishGameAsync(warband.Id, await _warbands.GetWarriorsAsync(warband.Id, "en"), Array.Empty<ConsumedItem>(), new[]
        {
            new AfterBattleResult(klaus.Id, 1, new AfterBattleOutcome { Kind = AfterBattleOutcomeKind.CharacteristicChange, Field = CharacteristicField.Strength }),
            new AfterBattleResult(klaus.Id, 1, new AfterBattleOutcome { Kind = AfterBattleOutcomeKind.CharacteristicChange, Delta = 1 }),
            new AfterBattleResult(klaus.Id, 1, new AfterBattleOutcome { Kind = AfterBattleOutcomeKind.GainSpecialRule }),
            new AfterBattleResult(klaus.Id, 1, new AfterBattleOutcome { Kind = AfterBattleOutcomeKind.GainSpecialRule, SpecialRuleId = "rule.unknown" }),
        }, "en");

        var after = await ReloadAsync(warband, klaus.Id);
        Assert.Equal(before.Strength, after.Strength);
        Assert.Empty(after.GainedSpecialRuleIds);
    }

    [Fact]
    public async Task LeaveForAddiction_RetiresTheWarrior()
    {
        var (warband, klaus, _) = await ReiklandersAsync();
        await _warbands.AddWarriorEquipmentAsync(klaus.Id, await ItemAsync("equipment.crimson-shade"), quantity: 2);
        await _service.LeaveForAddictionAsync(await ReloadAsync(warband, klaus.Id));
        var left = await ReloadAsync(warband, klaus.Id);
        Assert.Equal(WarriorStatus.Retired, left.Status);
        Assert.Equal(OutOfPlayReason.Addiction, left.OutReason);
        Assert.Empty(left.Equipment);
        Assert.Equal("Crimson Shade x2", left.EquipmentAtDeparture);
    }

    [Fact]
    public async Task DeletingAWarriorOrWarband_DropsItsGameUses()
    {
        var (warband, klaus, hans) = await ReiklandersAsync();
        await _service.StartGameAsync(warband.Id, new[]
        {
            new GameItemUse { WarriorId = klaus.Id, EquipmentItemId = 1 },
            new GameItemUse { WarriorId = hans.Id, EquipmentItemId = 1 },
            new GameItemUse { EquipmentItemId = 1 },
        });

        await _warbands.DeleteWarriorAsync(klaus.Id);
        Assert.Equal(2, (await _warbands.GetGameItemUsesAsync(warband.Id)).Count);

        await _warbands.DeleteWarbandAsync(warband.Id);
        Assert.Empty(await _warbands.GetGameItemUsesAsync(warband.Id));
    }

    [Fact]
    public void GameItemUse_StatModifiers_RoundTripAndTolerateJunk()
    {
        var use = new GameItemUse { StatModifiers = { new RolledStatModifier(CharacteristicField.Toughness, 1), new RolledStatModifier(CharacteristicField.Initiative, 3) } };
        var entity = use.ToEntity();
        Assert.Equal("Toughness:1;Initiative:3", entity.StatModifiers);
        Assert.Equal(use.StatModifiers, entity.ToModel().StatModifiers);
        Assert.Null(new GameItemUse().ToEntity().StatModifiers);

        entity.StatModifiers = "Toughness:1;Nope:2;Strength:x;Bad;Movement:1:2";
        Assert.Equal(new[] { new RolledStatModifier(CharacteristicField.Toughness, 1) }, entity.ToModel().StatModifiers);
    }
}
