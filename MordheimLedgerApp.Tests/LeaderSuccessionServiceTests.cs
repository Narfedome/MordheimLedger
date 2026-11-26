using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Succession du chef, retour d'un chef racheté et archivage appliqués en base, plus les réglages « Mort du
/// chef » du contenu officiel (Nécromancien, Vampire, Sœurs/Possédés/Kermesse). Base fraîchement seedée par test.</summary>
public class LeaderSuccessionServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDatabase _db;
    private readonly ILibraryService _library;
    private readonly IWarbandService _warbands;
    private readonly string _photoRoot;
    private readonly LeaderSuccessionService _succession;

    public LeaderSuccessionServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        _db = new AppDatabase(_dbPath);
        _library = new LibraryService(_db);
        _photoRoot = Path.Combine(Path.GetTempPath(), $"mordheimledger-photos-{Guid.NewGuid()}");
        _warbands = new WarbandService(_db, _library, new WarriorPhotoStore(_photoRoot));
        _succession = new LeaderSuccessionService(_warbands);
    }

    public void Dispose()
    {
        try { _db.Initialization.GetAwaiter().GetResult(); } catch { /* le test l'a déjà signalé */ }
        _db.Connection.CloseAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (Directory.Exists(_photoRoot)) Directory.Delete(_photoRoot, recursive: true);
    }

    private async Task<WarbandArchetype> WarbandArchetypeAsync(string officialId) =>
        (await _library.GetWarbandArchetypesAsync("en")).Single(a => a.OfficialId == officialId);

    private async Task<WarriorArchetype> WarriorArchetypeAsync(WarbandArchetype warband, string officialId) =>
        (await _library.GetWarriorArchetypesAsync(warband.Id, "en")).Single(a => a.OfficialId == officialId);

    private async Task<Warrior> ReloadAsync(Warband warband, int warriorId) =>
        (await _warbands.GetWarriorsAsync(warband.Id, "en")).Single(w => w.Id == warriorId);

    [Fact]
    public async Task OfficialContent_CarriesTheDeathOfALeaderSettings()
    {
        var undead = await WarbandArchetypeAsync("warband.undead");
        Assert.True((await WarriorArchetypeAsync(undead, "warrior.undead.necromancer")).IsDesignatedSuccessor);
        Assert.True((await WarriorArchetypeAsync(undead, "warrior.undead.vampire")).CanBeRehiredAfterDeath);
        Assert.False(undead.SuccessorLearnsMagic);

        foreach (var id in new[] { "warband.sisters-of-sigmar", "warband.cult-of-the-possessed", "warband.carnival-of-chaos" })
            Assert.True((await WarbandArchetypeAsync(id)).SuccessorLearnsMagic);

        var reiklanders = await WarbandArchetypeAsync("warband.reiklanders");
        Assert.False(reiklanders.SuccessorLearnsMagic);
        Assert.DoesNotContain(await _library.GetWarriorArchetypesAsync(reiklanders.Id, "en"),
            a => a.IsDesignatedSuccessor || a.CanBeRehiredAfterDeath);
    }

    [Fact]
    public async Task Succession_NewLeaderTakesTheLeaderRule()
    {
        var undead = await WarbandArchetypeAsync("warband.undead");
        var warband = await _warbands.CreateWarbandAsync("Court of Mortis", undead);
        var vampire = await _warbands.RecruitWarriorAsync(warband.Id, await WarriorArchetypeAsync(undead, "warrior.undead.vampire"), "Count Varek");
        var necromancer = await _warbands.RecruitWarriorAsync(warband.Id, await WarriorArchetypeAsync(undead, "warrior.undead.necromancer"), "Hieronymus");
        vampire.Status = WarriorStatus.Dead;
        await _warbands.SaveWarriorAsync(vampire);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        await _succession.ApplySuccessionAsync(roster, roster.Single(w => w.Id == necromancer.Id), successorLearnsMagic: false, alreadySpellcaster: true);

        Assert.True((await ReloadAsync(warband, necromancer.Id)).IsLeader);
        Assert.False((await ReloadAsync(warband, vampire.Id)).IsLeader);
        Assert.False((await ReloadAsync(warband, necromancer.Id)).CastsAsSuccessor);
    }

    [Fact]
    public async Task Succession_InAMagicWarband_NonCasterLearnsMagicWithAPendingSpell()
    {
        var sisters = await WarbandArchetypeAsync("warband.sisters-of-sigmar");
        var warband = await _warbands.CreateWarbandAsync("Daughters of the Hammer", sisters);
        var matriarch = await _warbands.RecruitWarriorAsync(warband.Id, await WarriorArchetypeAsync(sisters, "warrior.sisters-of-sigmar.sigmarite-matriarch"), "Mother Agathe");
        var superiorArchetype = await WarriorArchetypeAsync(sisters, "warrior.sisters-of-sigmar.sister-superior");
        var superior = await _warbands.RecruitWarriorAsync(warband.Id, superiorArchetype, "Ilse");
        matriarch.Status = WarriorStatus.Dead;
        await _warbands.SaveWarriorAsync(matriarch);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        await _succession.ApplySuccessionAsync(roster, roster.Single(w => w.Id == superior.Id), sisters.SuccessorLearnsMagic, superiorArchetype.IsSpellcaster);

        var reloaded = await ReloadAsync(warband, superior.Id);
        Assert.True(reloaded.IsLeader);
        Assert.True(reloaded.CastsAsSuccessor);
        Assert.True(reloaded.HasPendingSuccessorSpell);

        await _succession.ConsumePendingSuccessorSpellAsync(reloaded);
        var afterAdvance = await ReloadAsync(warband, superior.Id);
        Assert.True(afterAdvance.CastsAsSuccessor);
        Assert.False(afterAdvance.HasPendingSuccessorSpell);
    }

    [Fact]
    public async Task Succession_AlreadyACaster_GetsNoPendingSpell()
    {
        var sisters = await WarbandArchetypeAsync("warband.sisters-of-sigmar");
        var warband = await _warbands.CreateWarbandAsync("Daughters of the Hammer", sisters);
        var augur = await _warbands.RecruitWarriorAsync(warband.Id, await WarriorArchetypeAsync(sisters, "warrior.sisters-of-sigmar.augur"), "Clara");

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        await _succession.ApplySuccessionAsync(roster, roster.Single(w => w.Id == augur.Id), successorLearnsMagic: true, alreadySpellcaster: true);

        var reloaded = await ReloadAsync(warband, augur.Id);
        Assert.True(reloaded.IsLeader);
        Assert.False(reloaded.CastsAsSuccessor);
        Assert.False(reloaded.HasPendingSuccessorSpell);
    }

    [Fact]
    public async Task RehiredVampire_TakesTheLeadBackFromTheNecromancer()
    {
        var undead = await WarbandArchetypeAsync("warband.undead");
        var warband = await _warbands.CreateWarbandAsync("Court of Mortis", undead);
        var vampireArchetype = await WarriorArchetypeAsync(undead, "warrior.undead.vampire");
        var necromancer = await _warbands.RecruitWarriorAsync(warband.Id, await WarriorArchetypeAsync(undead, "warrior.undead.necromancer"), "Hieronymus");
        necromancer.IsLeader = true;
        await _warbands.SaveWarriorAsync(necromancer);

        var newVampire = await _warbands.RecruitWarriorAsync(warband.Id, vampireArchetype, "Countess Ilona");
        await _succession.ApplyRehiredLeaderAsync(await _warbands.GetWarriorsAsync(warband.Id, "en"), newVampire);

        Assert.True((await ReloadAsync(warband, newVampire.Id)).IsLeader);
        Assert.False((await ReloadAsync(warband, necromancer.Id)).IsLeader);
        Assert.Equal(LeaderRecruitmentNotice.ReplacesActingLeader,
            LeaderSuccessionRules.RecruitmentNotice(vampireArchetype.IsLeader, vampireArchetype.CanBeRehiredAfterDeath, leaderDiedThisGame: false));
    }

    [Fact]
    public async Task RehiredLeader_WithoutTheFlag_GetsItBack()
    {
        // Un chef dont la fiche a perdu la règle Chef (correction manuelle, ancienne édition du Codex) la retrouve.
        var undead = await WarbandArchetypeAsync("warband.undead");
        var warband = await _warbands.CreateWarbandAsync("Court of Mortis", undead);
        var vampire = await _warbands.RecruitWarriorAsync(warband.Id, await WarriorArchetypeAsync(undead, "warrior.undead.vampire"), "Countess Ilona");
        vampire.IsLeader = false;
        await _warbands.SaveWarriorAsync(vampire);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        await _succession.ApplyRehiredLeaderAsync(roster, roster.Single(w => w.Id == vampire.Id));

        Assert.True((await ReloadAsync(warband, vampire.Id)).IsLeader);
    }

    [Fact]
    public async Task ArchiveAndUnarchive_ArePersisted()
    {
        var reiklanders = await WarbandArchetypeAsync("warband.reiklanders");
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", reiklanders);
        warband.GameInProgress = true;
        var archivedAt = new DateTime(2026, 11, 26, 18, 30, 0);

        await _succession.ArchiveAsync(warband, archivedAt);
        var archived = (await _warbands.GetWarbandAsync(warband.Id))!;
        Assert.True(archived.IsArchived);
        Assert.Equal(archivedAt, archived.ArchivedAt);
        Assert.False(archived.GameInProgress);

        await _succession.UnarchiveAsync(archived);
        var restored = (await _warbands.GetWarbandAsync(warband.Id))!;
        Assert.False(restored.IsArchived);
        Assert.Null(restored.ArchivedAt);
    }

    [Fact]
    public async Task OpponentRatings_RoundTrip()
    {
        var reiklanders = await WarbandArchetypeAsync("warband.reiklanders");
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", reiklanders);

        warband.OpponentRatings = new List<int> { 176, 195 };
        await _warbands.SaveWarbandAsync(warband);
        Assert.Equal(new[] { 176, 195 }, (await _warbands.GetWarbandAsync(warband.Id))!.OpponentRatings);

        warband.OpponentRatings = new List<int>();
        await _warbands.SaveWarbandAsync(warband);
        Assert.Empty((await _warbands.GetWarbandAsync(warband.Id))!.OpponentRatings);
    }
}
