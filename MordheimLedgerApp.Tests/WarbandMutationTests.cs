using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Data.Entities;
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

    /// <summary>Bertha's own catalogue entry lists "Sigmarite Warhammer" twice in
    /// DramatisPersonae.json.startingEquipmentNames (she carries two) - locks in the 2026-09-01 fix
    /// (user report: "j'ai pas réussi à le gérer") for two distinct bugs found together: (1) the
    /// EquipmentItemId resolution used to go through EquipmentItem.Where(id-list.Contains(...)), which
    /// silently collapsed the duplicate since it iterates the catalog, never the id list twice; (2) even
    /// with the id preserved twice, RecruitDramatisPersonaAsync used to insert one WarriorEquipment row
    /// per occurrence instead of a single row with Quantity=2. Both fixed together: a single row,
    /// Quantity=2, "Sigmarite Warhammer x2" (see WarriorEquipment.NameDisplay's own quantity suffix, also
    /// new this pass).</summary>
    [Fact]
    public async Task RecruitingBertha_ConsolidatesDuplicateStartingEquipmentIntoOneQuantityTwoRow()
    {
        var warbandArchetype = await GetReiklandersAsync();
        var warband = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);

        var bertha = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name.StartsWith("Bertha"));
        var allEquipment = await _library.GetEquipmentItemsAsync("en");
        // Même résolution que l'appelant réel (WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync) -
        // par id, pas par Where(catalog).Contains(ids), pour préserver les doublons.
        var startingEquipment = bertha.StartingEquipmentIds.Select(id => allEquipment.First(e => e.Id == id)).ToList();
        Assert.Equal(2, startingEquipment.Count(e => e.Name == "Sigmarite Warhammer"));

        var recruited = await _warbands.RecruitDramatisPersonaAsync(warband.Id, bertha, "Bertha", startingEquipment, bertha.Skills);

        var roster = await _warbands.GetWarriorsAsync(warband.Id, "en");
        var warrior = Assert.Single(roster, w => w.Id == recruited.Id);
        var warhammer = Assert.Single(warrior.Equipment, e => e.Item.Name == "Sigmarite Warhammer");
        Assert.Equal(2, warhammer.Quantity);
        Assert.Equal("Sigmarite Warhammer x2", warhammer.NameDisplay);
        Assert.Equal(4, warrior.Equipment.Count); // Warhammer(x2)/Gromril Armour/Blessed Water/Holy Relic - 4 distinct rows, not 5.
    }

    /// <summary>The fix above only helps a fresh install: an already-seeded database (Bertha seeded with
    /// a single Sigmarite Warhammer row, before her JSON entry gained the duplicate on 2026-09-01) never
    /// re-runs SeedDramatisPersonaeAsync (empty-catalog gate only fires once) - same class of bug as
    /// ExplorationResults_DuplicatedByADoubleSeed_AreBackfilledOnNextLaunch above, fixed the same way via
    /// a dedicated Backfill* method (BackfillDramatisPersonaStartingEquipmentAsync) that runs
    /// unconditionally on every launch and re-syncs a mismatched Official persona's equipment row count
    /// from the JSON.</summary>
    [Fact]
    public async Task DramatisPersonaEquipment_StaleFromBeforeADuplicateWasAdded_IsBackfilledOnNextLaunch()
    {
        await _db.Initialization;

        var bertha = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name.StartsWith("Bertha"));
        var allEquipment = await _library.GetEquipmentItemsAsync("en");
        var warhammerId = allEquipment.Single(e => e.Name == "Sigmarite Warhammer").Id;

        // Simule une base seedée AVANT l'ajout du doublon dans DramatisPersonae.json : ne garder qu'UNE
        // seule ligne DramatisPersonaEquipmentEntity pour le Marteau de Sigmarite (au lieu de 2).
        var warhammerRows = await _db.Connection.Table<DramatisPersonaEquipmentEntity>()
            .Where(r => r.DramatisPersonaId == bertha.Id && r.EquipmentItemId == warhammerId).ToListAsync();
        Assert.Equal(2, warhammerRows.Count);
        await _db.Connection.DeleteAsync(warhammerRows[0]);

        // Rouvrir la même base (nouvelle instance AppDatabase sur le même fichier) rejoue InitializeAsync -
        // le garde-fou de seed ne se redéclenche pas, mais le backfill tourne à chaque lancement.
        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var reopenedBertha = (await reopenedLibrary.GetDramatisPersonaeAsync("en")).Single(p => p.Name.StartsWith("Bertha"));
        Assert.Equal(2, reopenedBertha.StartingEquipmentIds.Count(id => id == warhammerId));

        await reopenedDb.Connection.CloseAsync();
    }

    /// <summary>Same class of bug as the equipment-count backfill above, for the OTHER field
    /// BackfillDramatisPersonaStartingEquipmentAsync now also fixes: an already-seeded database has
    /// Johann's AlternativePaymentItemId still null (seeded before this field/JSON entry existed) - the
    /// backfill overwrites the plain FK column directly (no join table involved, simpler than the
    /// equipment-count fix) once it disagrees with DramatisPersonae.json's current
    /// alternativePaymentItemName.</summary>
    [Fact]
    public async Task DramatisPersonaAlternativePaymentItem_StaleFromBeforeItExisted_IsBackfilledOnNextLaunch()
    {
        await _db.Initialization;

        var johann = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name.StartsWith("Johann"));
        Assert.NotNull(johann.AlternativePaymentItemId);

        // Simule une base seedée AVANT l'ajout du champ - efface la valeur déjà résolue.
        var entity = await _db.Connection.FindAsync<DramatisPersonaEntity>(johann.Id);
        entity.AlternativePaymentItemId = null;
        await _db.Connection.UpdateAsync(entity);

        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var reopenedJohann = (await reopenedLibrary.GetDramatisPersonaeAsync("en")).Single(p => p.Name.StartsWith("Johann"));
        Assert.Equal("Crimson Shade", reopenedJohann.AlternativePaymentItem?.Name);

        await reopenedDb.Connection.CloseAsync();
    }

    /// <summary>Same backfill, same class of bug, for the pairing fields added 2026-09-01 ("on va bien
    /// s'amuser pour finaliser le duo") - an already-seeded database has Ulli/Marquand's
    /// PairedWithDramatisPersonaId/IsHiddenFromSearchPicker still at their defaults (null/false).</summary>
    [Fact]
    public async Task DramatisPersonaPairing_StaleFromBeforeItExisted_IsBackfilledOnNextLaunch()
    {
        await _db.Initialization;

        var marquand = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name == "Marquand Volker");
        var ulli = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name == "Ulli Leitpold");

        // Simule une base seedée AVANT l'ajout des champs - efface les valeurs déjà résolues.
        var marquandEntity = await _db.Connection.FindAsync<DramatisPersonaEntity>(marquand.Id);
        marquandEntity.PairedWithDramatisPersonaId = null;
        await _db.Connection.UpdateAsync(marquandEntity);
        var ulliEntity = await _db.Connection.FindAsync<DramatisPersonaEntity>(ulli.Id);
        ulliEntity.PairedWithDramatisPersonaId = null;
        ulliEntity.IsHiddenFromSearchPicker = false;
        await _db.Connection.UpdateAsync(ulliEntity);

        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var reopenedPersonae = await reopenedLibrary.GetDramatisPersonaeAsync("en");
        var reopenedMarquand = reopenedPersonae.Single(p => p.Name == "Marquand Volker");
        var reopenedUlli = reopenedPersonae.Single(p => p.Name == "Ulli Leitpold");
        Assert.Equal("Ulli Leitpold", reopenedMarquand.PairedWithDramatisPersona?.Name);
        Assert.Equal("Marquand Volker", reopenedUlli.PairedWithDramatisPersona?.Name);
        Assert.True(reopenedUlli.IsHiddenFromSearchPicker);

        await reopenedDb.Connection.CloseAsync();
    }

    /// <summary>Same class of bug again: "A Fistful of Crowns"/"Une Poignée d'Or" (2026-09-01) was split
    /// out of Marquand/Ulli's free-text Description into a real SpecialRule AFTER the pair had already
    /// seeded once - a database seeded before that split never picks it up without a dedicated backfill
    /// (BackfillDramatisPersonaSpecialRulesAsync). Additive-only: removing just this one link (simulating
    /// the stale state) and reopening must add it back WITHOUT touching "Inseparable", which was already
    /// there and must survive untouched.</summary>
    [Fact]
    public async Task DramatisPersonaSpecialRule_AddedAfterADatabaseWasAlreadySeeded_IsBackfilledOnNextLaunch()
    {
        await _db.Initialization;

        var marquand = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name == "Marquand Volker");
        Assert.Equal(2, marquand.SpecialRules.Count);

        // Simule une base seedée AVANT l'ajout de la règle - retire uniquement son lien de jointure,
        // laisse "Inseparable" intact.
        var staleLinks = await _db.Connection.Table<DramatisPersonaSpecialRuleEntity>()
            .Where(l => l.DramatisPersonaId == marquand.Id).ToListAsync();
        var fistfulLink = staleLinks.Single(l => l.SpecialRuleId == marquand.SpecialRules.Single(r => r.Name == "A Fistful of Crowns").Id);
        await _db.Connection.DeleteAsync(fistfulLink);

        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var reopenedMarquand = (await reopenedLibrary.GetDramatisPersonaeAsync("en")).Single(p => p.Name == "Marquand Volker");
        Assert.Equal(new[] { "A Fistful of Crowns", "Inseparable" }, reopenedMarquand.SpecialRules.Select(r => r.Name).OrderBy(n => n));

        await reopenedDb.Connection.CloseAsync();
    }

    /// <summary>Same class of bug again: Marquand/Ulli's Description text (2026-09-01, replaced their
    /// short trimmed bio with each half's real individual biography) and Marquand's new PairDescription
    /// (the shared "duo" lore, previously nonexistent) both need to reach an already-seeded database -
    /// BackfillDramatisPersonaDescriptionsAsync compares against the CURRENT English text (not a simple
    /// missing-row check like the other Backfill* methods), so this simulates BOTH a stale existing
    /// Description (old text still in the DB) and a genuinely missing PairDescriptionKey (null, as any
    /// database seeded before this field existed would have).</summary>
    [Fact]
    public async Task DramatisPersonaDescriptions_StaleOrMissing_AreBackfilledOnNextLaunch()
    {
        await _db.Initialization;

        var marquand = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name == "Marquand Volker");
        var marquandEntity = await _db.Connection.FindAsync<DramatisPersonaEntity>(marquand.Id);
        var staleDescKey = marquandEntity.DescriptionKey!;
        var staleTranslation = await _db.Connection.Table<TranslationEntity>()
            .Where(t => t.Key == staleDescKey && t.LanguageCode == "en").FirstAsync();
        staleTranslation.Value = "stale placeholder bio";
        await _db.Connection.UpdateAsync(staleTranslation);
        marquandEntity.PairDescriptionKey = null;
        await _db.Connection.UpdateAsync(marquandEntity);

        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var reopenedMarquand = (await reopenedLibrary.GetDramatisPersonaeAsync("en")).Single(p => p.Name == "Marquand Volker");
        Assert.Contains("mercenary and assassin", reopenedMarquand.Description);
        Assert.NotNull(reopenedMarquand.PairDescription);
        Assert.Contains("Marquand Volker and Ulli Leitpold", reopenedMarquand.PairDescription);

        await reopenedDb.Connection.CloseAsync();
    }

    /// <summary>Same class of bug again, one layer up: Equipment.json has no dedup-at-runtime mechanism
    /// at all (unlike DramatisPersona/Skill/Mutation), so a genuinely NEW entry added to it after a
    /// database already seeded once (2026-09-01: "Dagger (Johann)") would otherwise never reach a machine
    /// that had already seeded before that entry existed - fixed via BackfillNewEquipmentItemsAsync,
    /// which only INSERTS items missing by English name (mirrors SeedEquipmentAsync's own per-item logic),
    /// never touches an existing row.</summary>
    [Fact]
    public async Task NewEquipmentItem_AddedAfterADatabaseWasAlreadySeeded_IsBackfilledOnNextLaunch()
    {
        await _db.Initialization;

        var johannDagger = (await _library.GetEquipmentItemsAsync("en")).Single(e => e.Name == "Dagger (Johann)");

        // Simule une base seedée AVANT l'ajout de "Dagger (Johann)" à Equipment.json : supprime la ligne
        // (+ sa règle Parade attachée) comme si elle n'avait jamais existé.
        await _db.Connection.ExecuteAsync("DELETE FROM EquipmentItemSpecialRuleEntity WHERE EquipmentItemId = ?", johannDagger.Id);
        await _db.Connection.DeleteAsync<EquipmentItemEntity>(johannDagger.Id);

        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var reopenedDagger = Assert.Single(await reopenedLibrary.GetEquipmentItemsAsync("en"), e => e.Name == "Dagger (Johann)");
        Assert.True(reopenedDagger.IsUniqueArtefact);
        Assert.Equal(0, reopenedDagger.Cost);
        Assert.Contains(reopenedDagger.SpecialRules, r => r.Name == "Parry (Sword)");

        await reopenedDb.Connection.CloseAsync();
    }

    /// <summary>Second case the same backfill covers: an ALREADY-existing equipment item whose
    /// specialRules changed (2026-09-01: "Wizard's Staff (Nicodemus)" gained "Concussion"/
    /// "Parry (Buckler)" alongside its own "Two-Handed Grip") - detected by a rule-COUNT mismatch and
    /// re-synced (delete + reinsert), same idiom as the DramatisPersona equipment-count backfill.</summary>
    [Fact]
    public async Task EquipmentItemSpecialRules_StaleFromBeforeTheyWereAdded_AreBackfilledOnNextLaunch()
    {
        await _db.Initialization;

        var staff = (await _library.GetEquipmentItemsAsync("en")).Single(e => e.Name == "Wizard's Staff (Nicodemus)");
        Assert.Equal(3, staff.SpecialRules.Count);

        // Simule une base seedée AVANT l'ajout de Concussion/Parry (Buckler) : ne garder que "Two-Handed
        // Grip" (sa règle d'origine).
        var twoHandedGripRule = staff.SpecialRules.Single(r => r.Name == "Two-Handed Grip");
        await _db.Connection.ExecuteAsync(
            "DELETE FROM EquipmentItemSpecialRuleEntity WHERE EquipmentItemId = ? AND SpecialRuleId != ?", staff.Id, twoHandedGripRule.Id);

        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var reopenedLibrary = new LibraryService(reopenedDb);

        var reopenedStaff = (await reopenedLibrary.GetEquipmentItemsAsync("en")).Single(e => e.Name == "Wizard's Staff (Nicodemus)");
        Assert.Equal(new[] { "Concussion", "Parry (Buckler)", "Two-Handed Grip" },
            reopenedStaff.SpecialRules.Select(r => r.Name).OrderBy(n => n));

        await reopenedDb.Connection.CloseAsync();
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

    /// <summary>Délai de re-recherche (2026-09-01, "on va bien s'amuser pour finaliser le duo") - voir
    /// DramatisPersona.RequiresCooldownBeforeResearch. Couvre les 3 opérations CRUD de
    /// WarbandService directement (pas le picker/le wizard, testés côté tête MAUI, hors périmètre de ce
    /// projet de tests) : Add pose un cooldown, Get le retrouve, Clear efface tout pour la bande - jamais
    /// pour une AUTRE bande (Aenur pourrait être en cooldown pour la Bande A tout en restant recherchable
    /// par la Bande B).</summary>
    [Fact]
    public async Task DramatisPersonaCooldown_AddGetClear_ScopedPerWarband()
    {
        var warbandArchetype = await GetReiklandersAsync();
        var warbandA = await _warbands.CreateWarbandAsync("The Bleeding Roses", warbandArchetype);
        var warbandB = await _warbands.CreateWarbandAsync("The Iron Fists", warbandArchetype);

        var aenur = (await _library.GetDramatisPersonaeAsync("en")).Single(p => p.Name.StartsWith("Aenur"));
        Assert.True(aenur.RequiresCooldownBeforeResearch);

        await _warbands.AddDramatisPersonaCooldownAsync(warbandA.Id, aenur.Id);

        Assert.Equal(new[] { aenur.Id }, await _warbands.GetDramatisPersonaCooldownIdsAsync(warbandA.Id));
        Assert.Empty(await _warbands.GetDramatisPersonaCooldownIdsAsync(warbandB.Id));

        await _warbands.ClearAllDramatisPersonaCooldownsAsync(warbandA.Id);
        Assert.Empty(await _warbands.GetDramatisPersonaCooldownIdsAsync(warbandA.Id));
    }

    /// <summary>Cache de lecture d'AppDatabase (2026-09-24) : une édition du catalogue doit être visible
    /// dès la lecture suivante - nom (traduction, Update ORM) comme restrictions vidées (DELETE en SQL
    /// brut sans Insert derrière, invisible pour TableChanged - voir LibraryService.
    /// ExecuteRawDeleteAsync).</summary>
    [Fact]
    public async Task EditingCatalogItem_IsVisibleImmediately_DespiteReadCache()
    {
        var item = (await _library.GetEquipmentItemsAsync("en")).First(i => i.RestrictedToWarbandArchetypeIds.Count > 0);

        item.Name = "Renamed Item";
        item.RestrictedToWarbandArchetypeIds = new List<int>();
        await _library.SaveEquipmentItemAsync(item, "en");

        var reloaded = (await _library.GetEquipmentItemsAsync("en")).Single(i => i.Id == item.Id);
        Assert.Equal("Renamed Item", reloaded.Name);
        Assert.Empty(reloaded.RestrictedToWarbandArchetypeIds);
        Assert.Equal(ContentSource.Modified, reloaded.Source);
    }

    /// <summary>BackfillWarriorArchetypeRacialProfileAsync (2026-09-24) : gardé par PRAGMA user_version
    /// plutôt que par son propre filtre (qui ne se refermait jamais, 0 étant aussi "aucun profil") - doit
    /// quand même réparer une base antérieure au marqueur, puis ne plus jamais retourner relire les JSON.</summary>
    [Fact]
    public async Task RacialProfileBackfill_RunsOnLegacyDatabase_ThenIsMarkedDone()
    {
        await _db.Initialization;
        Assert.True(await _db.Connection.ExecuteScalarAsync<int>("PRAGMA user_version") >= 1);

        var reiklanders = await GetReiklandersAsync();
        var captain = (await _library.GetWarriorArchetypesAsync(reiklanders.Id, "en")).First(a => a.RacialProfileId != 0);
        var expectedProfileId = captain.RacialProfileId;

        // Simule une base d'avant RacialProfileId ET d'avant le marqueur.
        var entity = await _db.Connection.FindAsync<WarriorArchetypeEntity>(captain.Id);
        entity.RacialProfileId = 0;
        await _db.Connection.UpdateAsync(entity);
        await _db.Connection.ExecuteAsync("PRAGMA user_version = 0");
        await _db.Connection.CloseAsync();

        var reopenedDb = new AppDatabase(_dbPath);
        await reopenedDb.Initialization;
        var repaired = await reopenedDb.Connection.FindAsync<WarriorArchetypeEntity>(captain.Id);
        Assert.Equal(expectedProfileId, repaired.RacialProfileId);
        Assert.True(await reopenedDb.Connection.ExecuteScalarAsync<int>("PRAGMA user_version") >= 1);
        await reopenedDb.Connection.CloseAsync();
    }

    /// <summary>Les modèles sont reconstruits à chaque appel depuis les lignes cachées - modifier un
    /// modèle renvoyé sans le sauvegarder ne doit pas fuiter dans les lectures suivantes.</summary>
    [Fact]
    public async Task MutatingReturnedModel_DoesNotLeakIntoCache()
    {
        var first = (await _library.GetEquipmentItemsAsync("en")).First();
        var originalName = first.Name;
        first.Name = "Unsaved Change";
        first.SpecialRules.Clear();

        var again = (await _library.GetEquipmentItemsAsync("en")).Single(i => i.Id == first.Id);
        Assert.Equal(originalName, again.Name);
        Assert.NotSame(first, again);
    }
}
