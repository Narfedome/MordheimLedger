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

    /// <summary>The seed covers several warbands (see AppDatabase.SeedWarbandFromJsonAsync) - tests
    /// needing "the" seeded archetype specifically target Reiklander by name rather than assuming
    /// there's only one.</summary>
    private async Task<WarbandArchetype> GetReiklandersAsync(string languageCode = "en") =>
        (await _library.GetWarbandArchetypesAsync(languageCode)).Single(a => a.Name is "Reiklander Mercenaries" or "Mercenaires Reiklander");

    [Fact]
    public async Task Database_SeedsReiklanderMercenariesOnFirstLaunch()
    {
        var reiklanders = await GetReiklandersAsync();
        Assert.Equal("Reiklander Mercenaries", reiklanders.Name);
        Assert.Equal(ContentSource.Official, reiklanders.Source);

        // Captain/Champion/Youngblood/Warrior/Marksman/Swordsman - see Reiklanders.json.
        var warriorArchetypes = await _library.GetWarriorArchetypesAsync(reiklanders.Id, "en");
        Assert.Equal(6, warriorArchetypes.Count);

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
    public async Task Database_SeedsTenWarbandsTotal()
    {
        var archetypes = await _library.GetWarbandArchetypesAsync("en");
        Assert.Equal(10, archetypes.Count);
        Assert.Contains(archetypes, a => a.Name == "Undead");
        Assert.Contains(archetypes, a => a.Name == "Dwarf Treasure Hunters");
        Assert.Contains(archetypes, a => a.Name == "Averland Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Ostland Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Reiklander Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Middenheim Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Marienburg Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Carnival of Chaos");
        Assert.Contains(archetypes, a => a.Name == "Cult of the Possessed");
        Assert.Contains(archetypes, a => a.Name == "Orc Mob");

        var spells = await _library.GetSpellsAsync("en");
        Assert.Equal(30, spells.Count);
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Necromancy"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Prayers of Taal"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Nurgle Rituals"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Chaos Rituals"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Waaagh! Magic"));

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

    /// <summary>Reikland/Middenheim/Marienburg share the same base Mercenary roster (see
    /// Reiklanders.json/Middenheimers.json/Marienburgers.json) but diverge on city-specific rules: Reikland's
    /// Captain has Discipline Militaire (12ps command range) instead of the shared Leader rule, and its
    /// Marksman has the +1 BS bonus baked directly into its profile; Middenheim's Captain/Champion start at
    /// Strength 4; Marienburg starts with 600gc instead of 500.</summary>
    [Fact]
    public async Task MercenaryVariants_HaveCityDistinctStats()
    {
        var archetypes = await _library.GetWarbandArchetypesAsync("en");

        var reiklanders = archetypes.Single(a => a.Name == "Reiklander Mercenaries");
        var reiklanderWarriors = await _library.GetWarriorArchetypesAsync(reiklanders.Id, "en");
        Assert.Contains(reiklanderWarriors.Single(w => w.Name == "Mercenary Captain").SpecialRules, r => r.Name == "Military Discipline");
        Assert.DoesNotContain(reiklanderWarriors.Single(w => w.Name == "Mercenary Captain").SpecialRules, r => r.Name == "Leader");
        Assert.Equal(4, reiklanderWarriors.Single(w => w.Name == "Marksman").BallisticSkill);

        var middenheimers = archetypes.Single(a => a.Name == "Middenheim Mercenaries");
        var middenheimerWarriors = await _library.GetWarriorArchetypesAsync(middenheimers.Id, "en");
        Assert.Equal(4, middenheimerWarriors.Single(w => w.Name == "Mercenary Captain").Strength);
        Assert.Equal(4, middenheimerWarriors.Single(w => w.Name == "Champion").Strength);
        Assert.Contains(middenheimerWarriors.Single(w => w.Name == "Mercenary Captain").SpecialRules, r => r.Name == "Leader");
        Assert.Equal(3, middenheimerWarriors.Single(w => w.Name == "Marksman").BallisticSkill);

        var marienburgers = archetypes.Single(a => a.Name == "Marienburg Mercenaries");
        Assert.Equal(600, marienburgers.StartingTreasury);
        Assert.Contains(marienburgers.SpecialRules, r => r.Name == "Wealthy Traders");

        // "Expert Swordsman" is shared verbatim across all 3 cities' Swordsman archetype - find-or-create
        // by English Name must resolve them to the same catalog row instead of 3 duplicates.
        var allRules = await _library.GetSpecialRulesAsync("en");
        Assert.Single(allRules, r => r.Name == "Expert Swordsman");
    }

    /// <summary>Kermesse du Chaos is the first warband to use the Mutation restriction mechanism (its
    /// Nurgle's Blessings must not leak into other Chaos-adjacent warbands' mutation pickers) and to
    /// prove two warbands can each have their own distinctly-named magic school (its "Nurgle Rituals" is
    /// unrelated to the Undead's "Necromancy").</summary>
    [Fact]
    public async Task KermesseDuChaos_MutationsAreWarbandRestricted()
    {
        var kermesse = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Carnival of Chaos");

        Assert.Contains(kermesse.MagicSchools, s => s.Name == "Nurgle Rituals");
        var master = (await _library.GetWarriorArchetypesAsync(kermesse.Id, "en")).Single(w => w.Name == "Carnival Master");
        Assert.True(master.IsSpellcaster);

        var spells = await _library.GetSpellsAsync("en");
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Nurgle Rituals"));

        var allMutations = await _library.GetMutationsAsync("en");
        var rot = allMutations.Single(m => m.Name == "Nurgle's Rot");
        Assert.Equal([kermesse.Id], rot.RestrictedToWarbandArchetypeIds);
    }

    /// <summary>Contrasts with Kermesse's restricted Nurgle's Blessings: the generic Chaos mutation
    /// list (p.76) stays unrestricted (empty RestrictedToWarbandArchetypeIds) so it's shareable with
    /// Pillards Hommes-Bêtes later - and both Possessed archetypes flagged CanBuyMutations get the
    /// mutations tab.</summary>
    [Fact]
    public async Task CulteDesPossedes_MutationsAreUnrestricted()
    {
        var possessed = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Cult of the Possessed");
        var warriors = await _library.GetWarriorArchetypesAsync(possessed.Id, "en");
        Assert.True(warriors.Single(w => w.Name == "The Possessed").CanBuyMutations);
        Assert.True(warriors.Single(w => w.Name == "Mutant").CanBuyMutations);

        var allMutations = await _library.GetMutationsAsync("en");
        var greatClaw = allMutations.Single(m => m.Name == "Great Claw");
        Assert.Empty(greatClaw.RestrictedToWarbandArchetypeIds);
    }

    /// <summary>Orc Mob is the first warband to use both the Mount catalog (War Boar, restricted) and a
    /// non-fixed Movement characteristic (Cave Squigs roll 2D6" instead of a fixed value) - see
    /// WarriorArchetype.MovementOverride/MovementDisplay, added specifically for this case.</summary>
    [Fact]
    public async Task HordeOrque_SquigMovementOverride_AndWarBoarMount()
    {
        var orcs = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Orc Mob");
        var warriors = await _library.GetWarriorArchetypesAsync(orcs.Id, "en");

        var squigs = warriors.Single(w => w.Name == "Cave Squigs");
        Assert.Equal("2D6", squigs.MovementOverride);
        Assert.Equal("2D6", squigs.MovementDisplay);
        Assert.Contains(squigs.SpecialRules, r => r.Name == "Never Gains Experience");

        var boss = warriors.Single(w => w.Name == "Orc Boss");
        Assert.Equal(boss.Movement.ToString(), boss.MovementDisplay);
        Assert.Contains(boss.SpecialRules, r => r.Name == "Leader");

        var mounts = await _library.GetMountsAsync("en");
        var warBoar = mounts.Single(m => m.Name == "War Boar");
        Assert.Equal([orcs.Id], warBoar.RestrictedToWarbandArchetypeIds);
        Assert.Contains(warBoar.SpecialRules, r => r.Name == "Furious Charge");
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
