using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Sorts tirés au dé écrits en base : nouveau sort, doublon qui réduit la difficulté (cumulable), Magie Mineure
/// apprise avec le Grimoire, et la progression qui passe par la même règle. Base fraîchement seedée par test.</summary>
public class SpellLearningServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly SpellLearningService _learning;

    public SpellLearningServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _learning = new SpellLearningService(_warbands);
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* le test l'a déjà signalé */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<(Warband Warband, Warrior Warrior)> RecruitAsync(string warbandId, string warriorId, string name)
    {
        var archetype = (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.OfficialId == warbandId);
        var warband = await _warbands.CreateWarbandAsync("Test Band", archetype);
        var warriorArchetype = (await _library.GetWarriorArchetypesAsync(archetype.Id, "en")).Single(a => a.OfficialId == warriorId);
        var warrior = await _warbands.RecruitWarriorAsync(warband.Id, warriorArchetype, name);
        return (warband, await ReloadAsync(warband, warrior.Id));
    }

    private async Task<Warrior> ReloadAsync(Warband warband, int id) =>
        (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == id);

    private async Task<List<Spell>> SpellsOfAsync(string schoolOfficialId)
    {
        var school = (await _library.GetMagicSchoolsAsync("en")).Single(s => s.OfficialId == schoolOfficialId);
        return (await _library.GetSpellsAsync("en")).Where(s => s.MagicSchoolId == school.Id).OrderBy(s => s.RollValue).ToList();
    }

    [Fact]
    public async Task NewSpell_IsAdded_ThenDuplicatesLowerItsDifficulty()
    {
        var (warband, necromancer) = await RecruitAsync("warband.undead", "warrior.undead.necromancer", "Hieronymus");
        var spell = (await SpellsOfAsync(OfficialIds.LesserMagicSchool))[1];

        Assert.Equal(SpellLearningOutcome.NewSpell, await _learning.LearnAsync(necromancer, spell));
        Assert.Equal(SpellLearningOutcome.DifficultyReduced, await _learning.LearnAsync(necromancer, spell));
        Assert.Equal(SpellLearningOutcome.DifficultyReduced, await _learning.LearnAsync(necromancer, spell));

        var learned = Assert.Single((await ReloadAsync(warband, necromancer.Id)).Spells, s => s.Item.Id == spell.Id);
        Assert.Equal(2, learned.DifficultyReduction);
        Assert.EndsWith("(−2)", learned.Name);
    }

    [Fact]
    public async Task ArcaneLore_LearnsLesserMagicAndItsFirstSpell()
    {
        var (warband, champion) = await RecruitAsync("warband.reiklanders", "warrior.reiklanders.champion", "Hans");
        var lesser = (await _library.GetMagicSchoolsAsync("en")).Single(s => s.OfficialId == OfficialIds.LesserMagicSchool);
        var spell = (await SpellsOfAsync(OfficialIds.LesserMagicSchool))[0];

        await _learning.LearnSchoolAsync(champion, lesser, spell);

        var reloaded = await ReloadAsync(warband, champion.Id);
        Assert.Equal(lesser.Id, reloaded.ExtraMagicSchoolId);
        Assert.Equal(spell.Id, Assert.Single(reloaded.Spells).Item.Id);
    }

    [Fact]
    public async Task Advance_SpellAlreadyKnown_LowersDifficultyInsteadOfAddingIt()
    {
        var (warband, necromancer) = await RecruitAsync("warband.undead", "warrior.undead.necromancer", "Hieronymus");
        var spell = (await SpellsOfAsync(OfficialIds.LesserMagicSchool))[2];
        await _learning.LearnAsync(necromancer, spell);

        var advancement = new AdvancementService(_warbands);
        await advancement.ApplyAdvanceAsync(await ReloadAsync(warband, necromancer.Id), new AdvanceResult([], spell, null));

        var learned = Assert.Single((await ReloadAsync(warband, necromancer.Id)).Spells, s => s.Item.Id == spell.Id);
        Assert.Equal(1, learned.DifficultyReduction);
    }

    [Fact]
    public async Task OfficialTome_NotAvailableToWitchHuntersOrSisters()
    {
        var warbands = await _library.GetWarbandArchetypesAsync("en");
        var tome = (await _library.GetEquipmentItemsAsync("en")).Single(i => i.OfficialId == OfficialIds.TomeOfMagicItem);
        var allowed = warbands.Where(w => tome.RestrictedToWarbandArchetypeIds.Contains(w.Id)).Select(w => w.OfficialId).ToList();

        Assert.DoesNotContain("warband.witch-hunters", allowed);
        Assert.DoesNotContain("warband.sisters-of-sigmar", allowed);
        Assert.Equal(warbands.Count - 2, allowed.Count);
    }
}
