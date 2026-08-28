using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Tests that mutate warband/catalog state (recruit, delete, flip Official to Modified) - each
/// gets its own freshly-seeded database (unlike DataServiceTests' shared SeededDatabaseFixture) so one
/// test's writes can't leak into another's assertions.</summary>
public class WarbandMutationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;

    public WarbandMutationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _warbands = new WarbandService(_db, _library);
    }

    public void Dispose()
    {
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<WarbandArchetype> GetReiklandersAsync(string languageCode = "en") =>
        (await _library.GetWarbandArchetypesAsync(languageCode)).Single(a => a.Name is "Reiklander Mercenaries" or "Mercenaires Reiklander");

    [Fact]
    public async Task CreateWarband_PreFillsTreasuryFromArchetype()
    {
        var archetype = await GetReiklandersAsync();

        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", archetype);

        Assert.NotEqual(0, warband.Id);
        Assert.Equal(archetype.StartingTreasury, warband.Treasury);
        Assert.Equal(archetype.Id, warband.WarbandArchetypeId);
    }

    /// <summary>Race (2026-08-20): every warband archetype resolves to exactly one Race, seeded from
    /// each band's own JSON "race" field (SeedWarbandFromJsonAsync) - Reiklander Mercenaries are Human,
    /// same as most mercenary/Order bands.</summary>
    [Fact]
    public async Task WarbandArchetype_ResolvesRace()
    {
        var archetype = await GetReiklandersAsync();

        Assert.NotEqual(0, archetype.RaceId);
        Assert.NotNull(archetype.Race);
        Assert.Equal("Human", archetype.Race!.Name);
    }

    /// <summary>Carnival of Chaos and Cult of the Possessed are human in body but corrupted by Chaos -
    /// a distinct "Chaos Human" race, not plain "Human" (correction 2026-08-21: both were initially
    /// seeded as plain Human, same mistake a careless read of the roster would make).</summary>
    [Fact]
    public async Task WarbandArchetype_ChaosWarbands_ResolveChaosHumanRace()
    {
        var archetypes = await _library.GetWarbandArchetypesAsync("en");
        var kermesse = archetypes.Single(a => a.Name == "Carnival of Chaos");
        var possessed = archetypes.Single(a => a.Name == "Cult of the Possessed");

        Assert.Equal("Chaos Human", kermesse.Race!.Name);
        Assert.Equal("Chaos Human", possessed.Race!.Name);
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

    /// <summary>Hired Sword recruitment (see Models.Library.HiredSword/WarbandService.
    /// RecruitHiredSwordAsync) - mirrors RecruitWarrior_PreFillsStatsFromArchetype_AndPersists above,
    /// but via HiredSword.ToWarrior() (no WarriorArchetype involved) and with real starting-equipment
    /// rows inserted in the same call, since a Hired Sword's gear is fixed rather than picked.</summary>
    [Fact]
    public async Task RecruitHiredSword_PreFillsProfileAddsStartingEquipment_AndPersists()
    {
        var warbandArchetype = await GetReiklandersAsync();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var pitFighter = (await _library.GetHiredSwordsAsync("en")).Single(h => h.Name == "Pit Fighter");
        var startingEquipment = (await _library.GetEquipmentItemsAsync("en"))
            .Where(e => pitFighter.StartingEquipmentIds.Contains(e.Id)).ToList();

        var recruited = await _warbands.RecruitHiredSwordAsync(warband.Id, pitFighter, "Grimjaw", startingEquipment);

        Assert.NotEqual(0, recruited.Id);
        Assert.True(recruited.IsHiredSword);
        Assert.Equal(pitFighter.Id, recruited.HiredSwordId);
        Assert.Equal(pitFighter.BaseRating, recruited.HiredSwordBaseRating);
        Assert.False(recruited.IsHero);
        Assert.False(recruited.CanUseEquipment);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var persisted = Assert.Single(roster);
        Assert.Equal("Grimjaw", persisted.Name);
        Assert.Equal(startingEquipment.Count, persisted.Equipment.Count);
    }

    /// <summary>The Warlock is the one Hired Sword that's a spellcaster in its own right (Lesser Magic,
    /// not the hiring warband's own schools) - see Models.Library.HiredSword.MagicSchoolId, seeded via
    /// HiredSwords.json's magicSchoolName stub resolved against the same MagicSchools.json entry every
    /// other Lesser Magic spellcaster shares.</summary>
    [Fact]
    public async Task Warlock_ResolvesOwnMagicSchool()
    {
        var hiredSwords = await _library.GetHiredSwordsAsync("en");
        var warlock = hiredSwords.Single(h => h.Name == "Warlock");

        Assert.NotNull(warlock.MagicSchool);
        Assert.Equal("Lesser Magic", warlock.MagicSchool!.Name);

        var pitFighter = hiredSwords.Single(h => h.Name == "Pit Fighter");
        Assert.Null(pitFighter.MagicSchool);
    }

    /// <summary>Regression test for a real bug: GetWarriorsAsync used to resolve each carried
    /// EquipmentItem via a minimal FindAsync+ToModel(translations) call, leaving SpecialRules (and
    /// restrictions) empty for every already-recruited warrior's equipment - invisible until the
    /// warband-detail chip dialogs started actually displaying them.</summary>
    [Fact]
    public async Task RecruitedWarrior_CarriedEquipment_HasSpecialRulesResolved()
    {
        var warbandArchetype = await GetReiklandersAsync();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var captainArchetype = (await _library.GetWarriorArchetypesAsync(warbandArchetype.Id, "en")).First();
        var recruited = await _warbands.RecruitWarriorAsync(warband.Id, captainArchetype, "Otto");

        var equipmentWithRule = (await _library.GetEquipmentItemsAsync("en")).First(i => i.SpecialRules.Count > 0);
        await _warbands.AddWarriorEquipmentAsync(recruited.Id, equipmentWithRule);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var warrior = Assert.Single(roster);
        var carried = Assert.Single(warrior.Equipment);
        Assert.NotEmpty(carried.Item.SpecialRules);
    }

    /// <summary>Shrine's blessing (see ExplorationOutcome.GrantsWeaponBlessing) attaches "Blessed
    /// Weapon" via WarriorEquipment.BlessingRule - a SEPARATE slot from MaterialRule (Gromril/Ithilmar/
    /// Ornate), confirmed by the user 2026-08-21: a weapon already in Gromril that also gets blessed
    /// keeps BOTH, shown as "(G, B)" rather than the blessing overwriting the material.</summary>
    [Fact]
    public async Task BlessedWeapon_CoexistsWithExistingMaterialRule()
    {
        var warbandArchetype = await GetReiklandersAsync();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var captainArchetype = (await _library.GetWarriorArchetypesAsync(warbandArchetype.Id, "en")).First();
        var recruited = await _warbands.RecruitWarriorAsync(warband.Id, captainArchetype, "Otto");

        var axe = (await _library.GetEquipmentItemsAsync("en")).First(i => i.Name == "Axe");
        var gromril = (await _library.GetSpecialRulesAsync("en")).Single(r => r.Name == "Gromril Weapon");
        var blessed = (await _library.GetSpecialRulesAsync("en")).Single(r => r.Name == "Blessed Weapon");

        var carried = await _warbands.AddWarriorEquipmentAsync(recruited.Id, axe, materialRule: gromril);
        await _warbands.SetWarriorEquipmentBlessingRuleAsync(carried.Id, blessed.Id);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var reloaded = Assert.Single(Assert.Single(roster).Equipment);
        Assert.Equal("Gromril Weapon", reloaded.MaterialRule?.Name);
        Assert.Equal("Blessed Weapon", reloaded.BlessingRule?.Name);
        Assert.Equal("Axe (G, B)", reloaded.NameDisplay);
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

    [Fact]
    public async Task ExplorationResults_DuplicatedByADoubleSeed_AreBackfilledOnNextLaunch()
    {
        // Reproduit le bug trouvé le 2026-08-17 sur une base de dev existante : une seconde exécution de
        // SeedExplorationResultsAsync (hors du garde-fou normal "catalogue vide") insère un doublon pour
        // "Corpse" (2,3) avec ses propres clés de traduction jamais enregistrées dans TranslationEntity -
        // exactement le symptôme observé (nom/description affichant la clé brute au lieu du texte
        // résolu, le wizard tombant sur cette copie cassée via FirstOrDefault). Depuis le 2026-08-18,
        // ResyncExplorationResultsAsync (remplace l'ancien backfill ciblé sur ce seul symptôme) revide et
        // reseede tout le catalogue à chaque lancement plutôt que de juste dédupliquer - ce doublon
        // disparaît donc comme n'importe quel autre état périmé, plus seulement le cas des clés cassées.
        await _db.Initialization;

        var brokenDuplicate = new ExplorationResultEntity
        {
            DiceCount = 2, Value = 3,
            NameKey = Guid.NewGuid().ToString("N"), DescriptionKey = Guid.NewGuid().ToString("N"),
            Source = ContentSource.Official, RollsIndependently = false
        };
        await _db.Connection.InsertAsync(brokenDuplicate);
        await _db.Connection.InsertAsync(new ExplorationOutcomeEntity
        {
            ExplorationResultId = brokenDuplicate.Id, SubRollMin = 1, SubRollMax = 6,
            Kind = ExplorationOutcomeKind.Gold, GoldFormula = "D6"
        });

        // Rouvrir la même base de données (nouvelle instance AppDatabase sur le même fichier) rejoue
        // InitializeAsync : le garde-fou de seed ne se redéclenche pas (catalogue déjà peuplé), mais le
        // backfill tourne à chaque lancement, comme en conditions réelles au prochain démarrage de l'app.
        // La connexion _db du test reste ouverte en parallèle (SQLite autorise plusieurs connexions sur
        // le même fichier) - Dispose() la fermera normalement à la fin du test.
        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var results = await reopenedLibrary.GetExplorationResultsAsync("en");
        Assert.Equal(30, results.Count);
        var corpse = results.Single(r => r.DiceCount == 2 && r.Value == 3);
        Assert.Equal("Corpse", corpse.Name);

        await reopenedDb.Connection.CloseAsync();
    }
}
