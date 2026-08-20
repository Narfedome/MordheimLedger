using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Exercises LibraryService against a real (temp-file) SQLite database - read-only, so every
/// test shares one seeded database via SeededDatabaseFixture instead of re-seeding per test. Tests that
/// mutate warband/catalog state live in WarbandMutationTests, each with its own fresh database.</summary>
public class DataServiceTests : IClassFixture<SeededDatabaseFixture>
{
    private readonly ILibraryService _library;

    public DataServiceTests(SeededDatabaseFixture fixture)
    {
        _library = fixture.Library;
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

    /// <summary>Injuries.json seeds the rulebook's Serious Injuries charts once, common to every
    /// warband (no warband file references it) - Heroes' D66 chart (20 named rows covering the full
    /// 11-66 range) + Henchmen's much simpler D6 chart (2 rows).</summary>
    [Fact]
    public async Task Injuries_SeedsHeroesAndHenchmenChartsFromRulebook()
    {
        var injuries = await _library.GetInjuriesAsync("en");

        var heroInjuries = injuries.Where(i => i.Category == InjuryCategory.Hero).ToList();
        Assert.Equal(20, heroInjuries.Count);
        Assert.Contains(heroInjuries, i => i.Name == "Dead" && i.RollRange == "11-15");
        Assert.Contains(heroInjuries, i => i.Name == "Survives Against the Odds" && i.RollRange == "66");
        Assert.All(heroInjuries, i => Assert.False(string.IsNullOrWhiteSpace(i.RollRange)));

        var henchmanInjuries = injuries.Where(i => i.Category == InjuryCategory.Henchman).ToList();
        Assert.Equal(2, henchmanInjuries.Count);
        Assert.Contains(henchmanInjuries, i => i.Name == "Lost" && i.RollRange == "1-2");
        Assert.Contains(henchmanInjuries, i => i.Name == "Full Recovery" && i.RollRange == "3-6");
    }

    [Fact]
    public async Task ExplorationResults_SeedsChartWithMechanizedOutcomes()
    {
        var results = await _library.GetExplorationResultsAsync("en");
        Assert.Equal(30, results.Count);

        // Corpse (3 3): a single-roll D6 sub-table mixing a Gold branch and four Item branches.
        var corpse = results.Single(r => r.DiceCount == 2 && r.Value == 3);
        Assert.Equal("Corpse", corpse.Name);
        Assert.False(corpse.RollsIndependently);
        Assert.Equal(5, corpse.Outcomes.Count);
        Assert.Contains(corpse.Outcomes, o => o is { SubRollMin: 1, SubRollMax: 2, Kind: ExplorationOutcomeKind.Gold, GoldFormula: "D6" });
        Assert.Contains(corpse.Outcomes, o => o is { SubRollMin: 6, SubRollMax: 6, Kind: ExplorationOutcomeKind.Item, EquipmentItemName: "Light Armour" });

        // Shattered Building (5 5): a flat, automatic wyrdstone outcome (no sub-roll) PLUS an additional
        // Leadership test for a bonus Wardog (see ExplorationResult.BonusStatTestField).
        var shatteredBuilding = results.Single(r => r.DiceCount == 5 && r.Value == 5);
        Assert.Equal(ExplorationStatField.Leadership, shatteredBuilding.BonusStatTestField);
        Assert.Equal(2, shatteredBuilding.Outcomes.Count);
        var wyrdstoneOutcome = Assert.Single(shatteredBuilding.Outcomes, o => o.Kind == ExplorationOutcomeKind.Wyrdstone);
        Assert.Equal("D3", wyrdstoneOutcome.GoldFormula);
        Assert.Null(wyrdstoneOutcome.SubRollMin);
        Assert.Contains(shatteredBuilding.Outcomes, o => o is { Kind: ExplorationOutcomeKind.Item, EquipmentItemName: "Wardog", StatTestPass: true });

        // Hidden Treasure (6 2): every Outcome checked independently against its own "N+" threshold.
        var hiddenTreasure = results.Single(r => r.DiceCount == 6 && r.Value == 2);
        Assert.True(hiddenTreasure.RollsIndependently);
        Assert.Equal(8, hiddenTreasure.Outcomes.Count);

        // Well (1 1): a Toughness test gates one bonus wyrdstone shard (pass) against sickness (fail) -
        // both branches Auto (no sub-roll), the wizard picks Pass/Fail itself by comparing the chosen
        // Hero's roll to their Toughness (see EndOfGameDialogViewModel.ResolveStatTest).
        var well = results.Single(r => r.DiceCount == 2 && r.Value == 1);
        Assert.Equal(ExplorationStatField.Toughness, well.StatTestField);
        Assert.Equal(2, well.Outcomes.Count);
        var wellPass = well.Outcomes.Single(o => o.StatTestPass == true);
        Assert.Equal(ExplorationOutcomeKind.Wyrdstone, wellPass.Kind);
        Assert.Null(wellPass.SubRollMin);
        var wellFail = well.Outcomes.Single(o => o.StatTestPass == false);
        Assert.Equal(ExplorationOutcomeKind.None, wellFail.Kind);
        Assert.True(wellFail.CausesSickness);
        Assert.False(string.IsNullOrWhiteSpace(well.Description));

        // Catacombs (4 6): a purely tactical/battlefield effect (no gold/item/wyrdstone anywhere in the
        // rulebook text) - genuinely nothing to mechanize, stays pure text.
        var catacombs = results.Single(r => r.DiceCount == 4 && r.Value == 6);
        Assert.Empty(catacombs.Outcomes);

        // Straggler (2 4): conditional on the warband's type, no die roll involved - every applicable
        // branch is its own Auto outcome, the player applies whichever matches their warband.
        var straggler = results.Single(r => r.DiceCount == 2 && r.Value == 4);
        Assert.True(straggler.RollsIndependently);
        Assert.Contains(straggler.Outcomes, o => o is { Kind: ExplorationOutcomeKind.Gold, GoldFormula: "2D6" });
        Assert.All(straggler.Outcomes, o => Assert.Null(o.SubRollMin));

        // Dwarf Smithy (6 3): a "Gromril Axe" branch is just the base "Axe" plus the Gromril Weapon
        // material rule, not a distinct catalog item - see ExplorationOutcome.MaterialRuleName.
        var dwarfSmithy = results.Single(r => r.DiceCount == 6 && r.Value == 3);
        Assert.Contains(dwarfSmithy.Outcomes, o => o is { EquipmentItemName: "Axe", MaterialRuleName: "Gromril Weapon" });
    }

    /// <summary>Every MagicSchool now carries a flavor-text Description (sourced from mordheimer.net's
    /// school pages) in both languages - used to be null for all 7 schools until this was filled in.</summary>
    [Fact]
    public async Task MagicSchools_AllHaveBilingualDescriptions()
    {
        var schoolsEn = await _library.GetMagicSchoolsAsync("en");
        Assert.Equal(7, schoolsEn.Count);
        Assert.All(schoolsEn, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));

        var schoolsFr = await _library.GetMagicSchoolsAsync("fr");
        Assert.Equal(7, schoolsFr.Count);
        Assert.All(schoolsFr, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
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

        // WarriorArchetype.IsLeader is a separate, explicit flag (not derived from the "Leader" special
        // rule text above, which only 4 of the 15 warbands happen to carry) - every warband has exactly
        // one IsLeader archetype, and it's the same warrior these 4 bands already flag as "Leader".
        foreach (var (bandName, warriorName) in leaderBearers)
        {
            var band = archetypes.Single(a => a.Name == bandName);
            var bandWarriors = await _library.GetWarriorArchetypesAsync(band.Id, "en");
            Assert.Single(bandWarriors, w => w.IsLeader);
            Assert.True(bandWarriors.Single(w => w.Name == warriorName).IsLeader);
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

    /// <summary>Orc Mob is the first warband to use a non-fixed Movement characteristic (Cave Squigs roll
    /// 2D6" instead of a fixed value) - see WarriorArchetype.MovementOverride/MovementDisplay, added
    /// specifically for this case.</summary>
    [Fact]
    public async Task OrcMob_SquigMovementOverride()
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
    }

    /// <summary>Covers the deferred multi-band resolution added for common-catalog entries
    /// (EquipmentSeedData.RestrictedToWarbandNames, resolved in AppDatabase.SeedOfficialContentAsync
    /// after all 15 warbands exist) via the two Equipment.json mounts that use it: Warhorse (human
    /// warbands only) and Wardog (every warband except Skaven). War Boar was removed from Orc Mob's own
    /// equipment - it's Blazing Saddles (Mordheim Annual 2002, Grade 1b optional rules), not part of Orc
    /// Mob's own core (Grade 1a, Town Cryer #6) roster, confirmed on mordheimer.net.</summary>
    [Fact]
    public async Task Equipment_MultiBandRestrictions_ResolveAcrossWarbands()
    {
        var warbands = await _library.GetWarbandArchetypesAsync("en");
        var reiklanders = warbands.Single(w => w.Name == "Reiklander Mercenaries").Id;
        var kislevites = warbands.Single(w => w.Name == "Kislevites").Id;
        var orcMob = warbands.Single(w => w.Name == "Orc Mob").Id;
        var skaven = warbands.Single(w => w.Name == "Skaven of Clan Eshin").Id;

        var equipment = await _library.GetEquipmentItemsAsync("en");

        var warhorse = equipment.Single(i => i.Name == "Warhorse");
        Assert.Equal(EquipmentCategory.Animal, warhorse.Category);
        Assert.Contains(reiklanders, warhorse.RestrictedToWarbandArchetypeIds);
        Assert.Contains(kislevites, warhorse.RestrictedToWarbandArchetypeIds);
        Assert.DoesNotContain(orcMob, warhorse.RestrictedToWarbandArchetypeIds);
        Assert.DoesNotContain(skaven, warhorse.RestrictedToWarbandArchetypeIds);

        var wardog = equipment.Single(i => i.Name == "Wardog");
        Assert.Contains(reiklanders, wardog.RestrictedToWarbandArchetypeIds);
        Assert.Contains(orcMob, wardog.RestrictedToWarbandArchetypeIds);
        Assert.DoesNotContain(skaven, wardog.RestrictedToWarbandArchetypeIds);

        Assert.DoesNotContain(equipment, i => i.Name == "War Boar");
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

    /// <summary>Each Hero archetype's row of its warband's "skill table" (which of the 6 rulebook Skill
    /// lists it may pick an Advance from) is seeded from a source CSV, per-warrior, into
    /// WarriorArchetype.AllowedSkillCategories - data-only (not enforced in the Skill picker), same
    /// convention as the other Restricted* lists. Henchmen (no skill table in the rulebook) get none.</summary>
    [Fact]
    public async Task WarriorArchetype_AllowedSkillCategories_MatchSourceSkillTable()
    {
        var witchHunters = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Witch Hunters");
        var warriors = await _library.GetWarriorArchetypesAsync(witchHunters.Id, "en");

        var captain = warriors.Single(w => w.Name == "Witch Hunter Captain");
        Assert.Equal(
            new[] { SkillCategory.Combat, SkillCategory.Shooting, SkillCategory.Academic, SkillCategory.Strength, SkillCategory.Speed },
            captain.AllowedSkillCategories);

        var priest = warriors.Single(w => w.Name == "Warrior-Priest");
        Assert.Equal([SkillCategory.Combat, SkillCategory.Academic, SkillCategory.Strength], priest.AllowedSkillCategories);

        var warHounds = warriors.Single(w => w.Name == "War Hounds");
        Assert.Empty(warHounds.AllowedSkillCategories);

        var dwarfs = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.Name == "Dwarf Treasure Hunters");
        var trollSlayer = (await _library.GetWarriorArchetypesAsync(dwarfs.Id, "en")).Single(w => w.Name == "Troll Slayer");
        Assert.Equal([SkillCategory.Combat, SkillCategory.Strength, SkillCategory.Special], trollSlayer.AllowedSkillCategories);
    }
}
