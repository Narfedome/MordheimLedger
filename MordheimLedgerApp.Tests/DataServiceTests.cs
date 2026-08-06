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
    public async Task Database_SeedsFifteenWarbandsTotal()
    {
        var archetypes = await _library.GetWarbandArchetypesAsync("en");
        Assert.Equal(15, archetypes.Count);
        Assert.Contains(archetypes, a => a.Name == "Undead");
        Assert.Contains(archetypes, a => a.Name == "Dwarf Treasure Hunters");
        Assert.Contains(archetypes, a => a.Name == "Averlander Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Ostlander Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Reiklander Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Middenheim Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Marienburg Mercenaries");
        Assert.Contains(archetypes, a => a.Name == "Carnival of Chaos");
        Assert.Contains(archetypes, a => a.Name == "Cult of the Possessed");
        Assert.Contains(archetypes, a => a.Name == "Orc Mob");
        Assert.Contains(archetypes, a => a.Name == "Beastmen Raiders");
        Assert.Contains(archetypes, a => a.Name == "Witch Hunters");
        Assert.Contains(archetypes, a => a.Name == "Skaven of Clan Eshin");
        Assert.Contains(archetypes, a => a.Name == "The Sisters of Sigmar");
        Assert.Contains(archetypes, a => a.Name == "Kislevites");

        var spells = await _library.GetSpellsAsync("en");
        Assert.Equal(42, spells.Count);
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Necromancy"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Prayers of Taal"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Nurgle Rituals"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Chaos Rituals"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Waaagh! Magic"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Prayers of Sigmar"));
        Assert.Equal(6, spells.Count(s => s.MagicSchool?.Name == "Magic of the Horned Rat"));

        var necromancer = (await _library.GetWarriorArchetypesAsync(
            archetypes.Single(a => a.Name == "Undead").Id, "en")).Single(w => w.Name == "Necromancer");
        Assert.True(necromancer.IsSpellcaster);

        var undead = archetypes.Single(a => a.Name == "Undead");
        Assert.Contains(undead.MagicSchools, s => s.Name == "Necromancy");

        var dwarfAxe = (await _library.GetEquipmentItemsAsync("en")).Single(e => e.Name == "Dwarf axe");
        Assert.Single(dwarfAxe.RestrictedToWarbandArchetypeIds);
    }

    /// <summary>The 5 common seed files (SpecialRules/Equipment/Mutations/Skills/MagicSchools.json,
    /// seeded first in AppDatabase.SeedOfficialContentAsync) replace what used to be duplicated across
    /// warband files. This locks in the 3 real name/cost collisions that consolidation fixed (Short Bow,
    /// Flail, Holy Tome each existed 2-3× at different prices before) and the Skills catalog, which
    /// wasn't seeded at all before this.</summary>
    [Fact]
    public async Task CommonCatalogs_DedupCollisions_AndSeedSkills()
    {
        var equipment = await _library.GetEquipmentItemsAsync("en");
        Assert.Equal(5, Assert.Single(equipment, e => e.Name == "Short Bow").Cost);
        Assert.Equal(15, Assert.Single(equipment, e => e.Name == "Flail").Cost);
        Assert.Equal(100, Assert.Single(equipment, e => e.Name == "Holy Tome").Cost);

        var mutations = await _library.GetMutationsAsync("en");
        Assert.Contains(mutations, m => m.Name == "Hideous" && m.Description!.Contains("causes Fear"));

        var skills = await _library.GetSkillsAsync("en");
        Assert.True(skills.Count > 30, $"Expected the core rulebook skill lists (~34 entries), got {skills.Count}");
        var coreSkills = skills.Where(s => s.Category != SkillCategory.Special).ToList();
        Assert.True(coreSkills.Count > 30, $"Expected the core rulebook skill lists (~34 entries), got {coreSkills.Count}");
        Assert.All(coreSkills, s => Assert.Empty(s.RestrictedToWarbandArchetypeIds));
        foreach (var category in new[] { SkillCategory.Combat, SkillCategory.Shooting, SkillCategory.Academic, SkillCategory.Strength, SkillCategory.Speed })
            Assert.Contains(skills, s => s.Category == category);

        // Special = each warband's own special-skill table (e.g. Orc Mob's Waaagh!/'Ard 'Ead/...),
        // seed-only via WarbandSeedData.Skills - always warband-restricted, some further restricted to
        // a specific WarriorArchetype (e.g. Orc Boss's "Da Cunnin' Plan").
        var specialSkills = skills.Where(s => s.Category == SkillCategory.Special).ToList();
        Assert.NotEmpty(specialSkills);
        Assert.All(specialSkills, s => Assert.Single(s.RestrictedToWarbandArchetypeIds));
        Assert.Single(specialSkills, s => s.Name == "Da Cunnin' Plan" && s.RestrictedToWarriorArchetypeIds.Count == 1);
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
        var leaderBearers = new[] { ("Averlander Mercenaries", "Captain"), ("Ostlander Mercenaries", "Elder"),
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
        var ostlanders = archetypes.Single(a => a.Name == "Ostlander Mercenaries");
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
    public async Task CarnivalOfChaos_MutationsAreWarbandRestricted()
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
    public async Task CultOfThePossessed_MutationsAreUnrestricted()
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
    public async Task OrcMob_SquigMovementOverride_AndWarBoarMount()
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

    /// <summary>Undead is a 1-equipment-list band (all equipped warriors share it) - the simplest shape,
    /// see AppDatabase.SeedWarbandFromJsonAsync's Equipment/EquipmentLists/Warriors seeding order.</summary>
    [Fact]
    public async Task Undead_AllEquippedWarriorsShareOneEquipmentList()
    {
        var undead = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Undead");
        var lists = await _library.GetEquipmentListsAsync(undead.Id, "en");
        var list = Assert.Single(lists);
        Assert.Equal("Undead Equipment List", list.Name);
        Assert.NotEmpty(list.ItemIds);

        var warriors = await _library.GetWarriorArchetypesAsync(undead.Id, "en");
        foreach (var name in new[] { "Vampire", "Necromancer", "Dreg" })
            Assert.Equal(list.Id, warriors.Single(w => w.Name == name).EquipmentListId);

        // Zombies/Ghouls/Dire Wolves carry no equipment at all.
        Assert.Null(warriors.Single(w => w.Name == "Zombie").EquipmentListId);
    }

    /// <summary>Skaven is a 2-list band where a Hero (Night Runners) draws from the Henchmen list rather
    /// than the Heroes one - the list<->archetype mapping isn't a fixed Hero/Henchman split.</summary>
    [Fact]
    public async Task Skaven_NightRunnersHero_UsesHenchmenEquipmentList()
    {
        var skavens = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Skaven of Clan Eshin");
        var lists = await _library.GetEquipmentListsAsync(skavens.Id, "en");
        Assert.Equal(2, lists.Count);

        var warriors = await _library.GetWarriorArchetypesAsync(skavens.Id, "en");
        var heroesList = lists.Single(l => l.Name == "Heroes Equipment List");
        var henchmenList = lists.Single(l => l.Name == "Henchmen Equipment List");

        var nightRunners = warriors.Single(w => w.Name == "Night Runners");
        Assert.True(nightRunners.IsHero);
        Assert.Equal(henchmenList.Id, nightRunners.EquipmentListId);

        var assassinAdept = warriors.Single(w => w.Name == "Assassin Adept");
        Assert.Equal(heroesList.Id, assassinAdept.EquipmentListId);
    }

    /// <summary>Reiklanders.json declares no band-specific "equipment" (empty array) - its two
    /// equipment lists are built purely from ItemNames referencing the common Equipment.json pool.</summary>
    [Fact]
    public async Task Reiklanders_EquipmentListsBuiltEntirelyFromCommonPool()
    {
        var reiklanders = await GetReiklandersAsync();
        var lists = await _library.GetEquipmentListsAsync(reiklanders.Id, "en");
        Assert.Equal(2, lists.Count);
        Assert.All(lists, l => Assert.NotEmpty(l.ItemIds));

        var warriors = await _library.GetWarriorArchetypesAsync(reiklanders.Id, "en");
        Assert.All(warriors, w => Assert.NotNull(w.EquipmentListId));
    }

    /// <summary>Averlanders' "Hunting Arrows" is restricted to the Bergjaeger archetype even though it's
    /// a member of the Scout Equipment List shared with Halfling Scouts - see
    /// EquipmentItem.RestrictedToWarriorArchetypeIds.</summary>
    [Fact]
    public async Task Averlanders_HuntingArrows_RestrictedToBergjaegerOnly()
    {
        var averlanders = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Averlander Mercenaries");
        var warriors = await _library.GetWarriorArchetypesAsync(averlanders.Id, "en");
        var bergjaeger = warriors.Single(w => w.Name == "Bergjaeger");

        var equipment = await _library.GetEquipmentItemsAsync("en");
        var huntingArrows = equipment.Single(e => e.Name == "Hunting Arrows");
        Assert.Equal([bergjaeger.Id], huntingArrows.RestrictedToWarriorArchetypeIds);

        var scoutList = (await _library.GetEquipmentListsAsync(averlanders.Id, "en")).Single(l => l.Name == "Scout Equipment List");
        Assert.Contains(huntingArrows.Id, scoutList.ItemIds);
    }

    /// <summary>Holy Tome is a Rare item shared by two unrelated bands (Witch Hunters, Sisters of
    /// Sigmar), each restricting it to a different set of their own Heroes - must resolve to a single
    /// catalog row (not two duplicates) via AppDatabase.SeedWarbandFromJsonAsync's find-or-create
    /// Equipment loop, with both bands' warband/warrior restriction rows accumulating on it.</summary>
    [Fact]
    public async Task HolyTome_SharedRareItem_RestrictedPerBandAndPerHero()
    {
        var equipment = await _library.GetEquipmentItemsAsync("en");
        var holyTomes = equipment.Where(e => e.Name == "Holy Tome").ToList();
        var holyTome = Assert.Single(holyTomes);

        var witchHunters = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Witch Hunters");
        var sisters = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "The Sisters of Sigmar");
        Assert.Equal(new[] { witchHunters.Id, sisters.Id }.OrderBy(x => x), holyTome.RestrictedToWarbandArchetypeIds.OrderBy(x => x));

        var warriorPriest = (await _library.GetWarriorArchetypesAsync(witchHunters.Id, "en")).Single(w => w.Name == "Warrior-Priest");
        var sisterHeroines = (await _library.GetWarriorArchetypesAsync(sisters.Id, "en"))
            .Where(w => w.Name is "Sigmarite Matriarch" or "Augur" or "Sister Superior").Select(w => w.Id);
        var expectedWarriors = new[] { warriorPriest.Id }.Concat(sisterHeroines).OrderBy(x => x);
        Assert.Equal(expectedWarriors, holyTome.RestrictedToWarriorArchetypeIds.OrderBy(x => x));

        // The recruit picker is list-only now (see EquipmentItemViewModel.ApplyFilter) - a Rare item a
        // warrior can actually buy must be a member of their list, not just warband/warrior-restricted.
        var witchHunterList = (await _library.GetEquipmentListsAsync(witchHunters.Id, "en")).Single(l => l.Name == "Witch Hunter Equipment List");
        var sistersList = (await _library.GetEquipmentListsAsync(sisters.Id, "en")).Single(l => l.Name == "Sisters of Sigmar Equipment List");
        Assert.Contains(holyTome.Id, witchHunterList.ItemIds);
        Assert.Contains(holyTome.Id, sistersList.ItemIds);
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
