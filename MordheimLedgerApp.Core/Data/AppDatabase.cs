using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data;

public partial class AppDatabase
{
    private readonly SQLiteAsyncConnection _db;
    public SQLiteAsyncConnection Connection => _db;

    /// <summary>
    /// Table creation, run at construction. Data services must await this before their first query:
    /// SQLiteAsyncConnection does not guarantee ordering between operations, so a query issued before
    /// init completes could hit "no such table" on first launch.
    /// </summary>
    public Task Initialization { get; }

    /// <param name="officialSeed">Ouvre la seed.db3 embarquée dans l'appli (null en tests/outils) : si elle est
    /// fournie, l'initialisation synchronise le contenu officiel dès que ContentVersion.json est plus récent
    /// que la version enregistrée en base - voir SyncOfficialContentAsync, StartupSyncReport.</param>
    public AppDatabase(string path, Func<Task<Stream>>? officialSeed = null)
    {
        _officialSeed = officialSeed;
        _db = new SQLiteAsyncConnection(path);
        // Toute écriture ORM (Insert/Update/Delete, d'où qu'elle vienne) évince la table concernée du
        // cache - les DELETE en SQL brut ne déclenchent pas cet événement, voir InvalidateCachedTable.
        _db.GetConnection().TableChanged += (_, e) => InvalidateCachedTable(e.Table.MappedType);
        Initialization = InitializeAsync();
    }

    // --- Cache de lecture (2026-09-24) -------------------------------------------------------------
    // Le catalogue (Library) est relu en entier à chaque Get*Async - et chaque Get*Async en rappelle
    // d'autres (GetEquipmentItemsAsync -> GetSpecialRulesAsync, GetDramatisPersonaeAsync -> équipement +
    // compétences + règles...), si bien que l'ouverture du wizard Fin de Partie relisait la même table
    // d'équipement ~5 fois et les règles spéciales ~10 fois. On ne cache que les LIGNES BRUTES (entités)
    // et les traductions, jamais les modèles : chaque appel reconstruit des modèles neufs, donc aucun
    // écran ne partage d'instance mutable avec un autre. Les listes renvoyées ne doivent pas être
    // modifiées (IReadOnlyList), ni les entités qu'elles contiennent (les Save* relisent via FindAsync).
    private readonly ConcurrentDictionary<Type, Task<object>> _tableCache = new();

    /// <summary>Contenu complet de la table T, lu une fois puis servi depuis la mémoire jusqu'à la
    /// prochaine écriture sur T. Réservé aux tables du catalogue et aux traductions (petites, lues bien
    /// plus souvent qu'écrites).</summary>
    internal async Task<IReadOnlyList<T>> CachedTableAsync<T>() where T : new()
    {
        var task = _tableCache.GetOrAdd(typeof(T), async _ => (object)await _db.Table<T>().ToListAsync());
        try
        {
            return (IReadOnlyList<T>)await task;
        }
        catch
        {
            // Ne pas garder une lecture en échec en cache pour toujours.
            _tableCache.TryRemove(new KeyValuePair<Type, Task<object>>(typeof(T), task));
            throw;
        }
    }

    /// <summary>À appeler après tout DELETE/UPDATE en SQL brut (ExecuteAsync) sur une table cachée -
    /// seules les écritures ORM passent par TableChanged.</summary>
    internal void InvalidateCachedTable(Type entityType) => _tableCache.TryRemove(entityType, out _);

    internal void InvalidateCachedTable<T>() => InvalidateCachedTable(typeof(T));

    private void InvalidateAllCachedTables() => _tableCache.Clear();

    private async Task InitializeAsync()
    {
        await CreateAllTablesAsync();

        // Chaque étape dans une transaction : sans elle, chaque Insert/Update/Delete est sa propre
        // transaction implicite, donc un flush disque par ligne - mesuré le 2026-09-24 : ~45 s au premier
        // lancement et ~4,5 s à CHAQUE lancement (quasi tout dans ResyncExplorationResultsAsync, ~200 lignes
        // supprimées/réinsérées une à une), que tout service attend via Initialization avant sa première
        // requête. Trois temps, dans cet ordre :
        // 1. seed (base vide) ou identification des entrées officielles d'une base d'avant les ids stables ;
        // 2. synchro du contenu officiel (sa propre transaction) - elle a remplacé les anciens Backfill* du
        //    catalogue (race des bandes, profil racial, nouveaux objets, Dramatis Personae...) ;
        // 3. réparation des parties jouées, que la synchro ne touche jamais - APRÈS elle, pour s'appuyer sur un
        //    catalogue déjà à jour (ex. les maximums raciaux d'un guerrier viennent du profil de son archétype).
        await RunInTransactionAsync(SeedOrIdentifyOfficialContentAsync);
        await SyncOfficialContentOnStartupAsync();
        await RunInTransactionAsync(RepairPlayedDataAsync);
    }

    /// <summary>BEGIN/COMMIT explicites plutôt que SQLiteAsyncConnection.RunInTransactionAsync, qui
    /// n'accepte qu'un callback synchrone sur SQLiteConnection - tout le pipeline de seed est écrit en
    /// async contre _db. Sûr ici : SQLiteAsyncConnection sérialise tout sur une seule connexion par
    /// chemin, et aucun appelant n'écrit en parallèle (tous les services attendent Initialization,
    /// ResetAsync aussi). Ne pas appeler d'InsertAllAsync/RunInTransactionAsync à l'intérieur : sqlite-
    /// net ouvrirait son propre BEGIN, refusé dans une transaction déjà ouverte.</summary>
    private async Task RunInTransactionAsync(Func<Task> work)
    {
        await _db.ExecuteAsync("BEGIN TRANSACTION");
        try
        {
            await work();
            await _db.ExecuteAsync("COMMIT");
        }
        catch
        {
            await _db.ExecuteAsync("ROLLBACK");
            throw;
        }
        finally
        {
            // Seed/backfill/reset écrivent aussi en SQL brut (DROP, DELETE) - invisible pour
            // TableChanged, et un ROLLBACK annulerait des lignes éventuellement déjà cachées.
            InvalidateAllCachedTables();
        }
    }

    private async Task SeedOrIdentifyOfficialContentAsync()
    {
        // First-launch only: if the archetype catalog is empty, nothing has been seeded yet (and
        // nothing the player made is at risk of being duplicated).
        if (await _db.Table<WarbandArchetypeEntity>().CountAsync() == 0)
            await SeedOfficialContentAsync();

        await AssignOfficialIdsOnceAsync();
    }

    /// <summary>Réparations des parties jouées (Warrior...) - jamais concernées par la synchro du contenu
    /// officiel, qui ne touche que le catalogue. No-op dès que tout est réparé.</summary>
    private async Task RepairPlayedDataAsync()
    {
        await BackfillNeverGainsExperienceAsync();
        await BackfillWarriorRacialMaxesAsync();
        await BackfillWarriorStartingStatsAsync();

        // Contrairement au reste du catalogue, reconstruite à chaque lancement (voir sa doc) - n'a pas de
        // meilleur endroit que celui-ci depuis que la synchro a remplacé les Backfill* du catalogue.
        await ResyncExplorationResultsAsync();
    }

    /// <summary>One-time-per-row data fix for campaigns that started before WarriorArchetype/
    /// Warrior.GainsExperience existed (2026-08-17): the new column's SQLite-added default is `true`
    /// even for archetypes (Zombie, etc.) that already carry the "Never Gains Experience" special rule -
    /// so the flag would silently disagree with the rule already shown on the warrior's sheet until
    /// something corrects it. Unlike the "editing an archetype doesn't retroactively change
    /// already-recruited warriors" rule elsewhere in this app (a deliberate design choice about future
    /// edits), this is a missing-initial-value bug, not an edit - so any already-recruited Warrior
    /// snapshot gets corrected here, once (the archetype itself is also fixed by the official content
    /// sync, but only for an Official row - a Modified one still needs it). Cheap no-op every run after
    /// the first since the filters (GainsExperience still true) then match nothing.</summary>
    private async Task BackfillNeverGainsExperienceAsync()
    {
        var ruleIds = (await _db.Table<SpecialRuleEntity>().Where(r => r.OfficialId == OfficialIds.NeverGainsExperienceRule).ToListAsync())
            .Select(r => r.Id)
            .ToHashSet();
        if (ruleIds.Count == 0) return;

        var archetypeIds = (await _db.Table<WarriorArchetypeSpecialRuleEntity>().ToListAsync())
            .Where(j => ruleIds.Contains(j.SpecialRuleId))
            .Select(j => j.WarriorArchetypeId)
            .ToHashSet();
        if (archetypeIds.Count == 0) return;

        var staleArchetypes = (await _db.Table<WarriorArchetypeEntity>().ToListAsync())
            .Where(a => archetypeIds.Contains(a.Id) && a.GainsExperience)
            .ToList();
        foreach (var archetype in staleArchetypes)
        {
            archetype.GainsExperience = false;
            await _db.UpdateAsync(archetype);
        }

        var staleWarriors = (await _db.Table<WarriorEntity>().ToListAsync())
            .Where(w => w.WarriorArchetypeId is { } waId && archetypeIds.Contains(waId) && w.GainsExperience)
            .ToList();
        foreach (var warrior in staleWarriors)
        {
            warrior.GainsExperience = false;
            await _db.UpdateAsync(warrior);
        }
    }

    /// <summary>The 15 warband seed file names - same list as SeedOfficialContentAsync's explicit
    /// SeedWarbandFromJsonAsync calls, duplicated here (rather than having that method iterate this
    /// array) so the ordered/commented call list there stays easy to scan on its own. Consumed by the
    /// official-id bridge (AppDatabase.OfficialIdBridge.cs), which needs to revisit every band file.</summary>
    internal static readonly string[] WarbandFileNames =
    [
        "Undead.json", "DwarfTreasureHunters.json", "Averlanders.json", "Ostlanders.json",
        "Reiklanders.json", "Middenheimers.json", "Marienburgers.json", "CarnivalOfChaos.json",
        "CultOfThePossessed.json", "OrcMob.json", "BeastmenRaiders.json", "WitchHunters.json",
        "SkavenOfClanEshin.json", "SistersOfSigmar.json", "Kislevites.json"
    ];

    /// <summary>One-time-per-row data fix for Warriors recruited before the racial-maximum snapshot
    /// fields (MaxWeaponSkill etc.) existed - unlike the RaceId/RacialProfileId backfills above, this
    /// doesn't need seed data or English-name lookups: every WarriorEntity already carries a real
    /// WarriorArchetypeId - if that archetype's own RacialProfileId resolves to a real profile (0 =
    /// genuinely none, see SeedWarbandFromJsonAsync/RacialProfiles.json's own doc - stays null forever,
    /// nothing to backfill), copy its 9 maximums across. Filters on MaxWeaponSkill == null rather than
    /// all 9 fields at once, same cheap-no-op-after-first-run idiom as the other backfills - re-scans
    /// (harmlessly, a no-op via the profilesById lookup below) every launch for warriors whose archetype
    /// has no profile, since null is otherwise indistinguishable from "not yet backfilled".</summary>
    private async Task BackfillWarriorRacialMaxesAsync()
    {
        var staleWarriors = (await _db.Table<WarriorEntity>().ToListAsync())
            .Where(w => w.MaxWeaponSkill == null)
            .ToList();
        if (staleWarriors.Count == 0) return;

        var archetypesById = (await _db.Table<WarriorArchetypeEntity>().ToListAsync()).ToDictionary(a => a.Id);
        var profilesById = (await _db.Table<RacialProfileEntity>().ToListAsync()).ToDictionary(p => p.Id);

        foreach (var warrior in staleWarriors)
        {
            if (warrior.WarriorArchetypeId is not { } warriorArchetypeId) continue;
            if (!archetypesById.TryGetValue(warriorArchetypeId, out var archetype)) continue;
            if (!profilesById.TryGetValue(archetype.RacialProfileId, out var profile)) continue;

            warrior.MaxMovement = profile.MovementOverride is null ? profile.Movement : null;
            warrior.MaxWeaponSkill = profile.WeaponSkill;
            warrior.MaxBallisticSkill = profile.BallisticSkill;
            warrior.MaxStrength = profile.Strength;
            warrior.MaxToughness = profile.Toughness;
            warrior.MaxWounds = profile.Wounds;
            warrior.MaxInitiative = profile.Initiative;
            warrior.MaxAttacks = profile.Attacks;
            warrior.MaxLeadership = profile.Leadership;
            await _db.UpdateAsync(warrior);
        }
    }

    /// <summary>One-time-per-row data fix for warriors recruited before Warrior.StartingMovement/etc
    /// existed (2026-08-25) - the new columns default to 0 for pre-existing rows, which would make the
    /// stat-changed color code (StatRowView's DataTriggers) show every single one of a warrior's
    /// current stats as "increased" the moment this ships. All 9 fields being exactly 0 is used as the
    /// "never set" marker (safe in practice: no real profile has Wounds/Attacks/Leadership all at 0 -
    /// that would mean an unfieldable model) - baseline resets to whatever each warrior's CURRENT stats
    /// happen to be right now (their true recruitment-time values aren't recoverable retroactively), so
    /// no false delta shows up today and tracking is accurate from this point forward.</summary>
    private async Task BackfillWarriorStartingStatsAsync()
    {
        var staleWarriors = (await _db.Table<WarriorEntity>().ToListAsync())
            .Where(w => w.StartingMovement == 0 && w.StartingWeaponSkill == 0 && w.StartingBallisticSkill == 0 &&
                        w.StartingStrength == 0 && w.StartingToughness == 0 && w.StartingWounds == 0 &&
                        w.StartingInitiative == 0 && w.StartingAttacks == 0 && w.StartingLeadership == 0)
            .ToList();

        foreach (var warrior in staleWarriors)
        {
            warrior.StartingMovement = warrior.Movement;
            warrior.StartingWeaponSkill = warrior.WeaponSkill;
            warrior.StartingBallisticSkill = warrior.BallisticSkill;
            warrior.StartingStrength = warrior.Strength;
            warrior.StartingToughness = warrior.Toughness;
            warrior.StartingWounds = warrior.Wounds;
            warrior.StartingInitiative = warrior.Initiative;
            warrior.StartingAttacks = warrior.Attacks;
            warrior.StartingLeadership = warrior.Leadership;
            await _db.UpdateAsync(warrior);
        }
    }

    /// <summary>Objet du catalogue commun (Equipment.json) tel que le seed l'insère.</summary>
    private static EquipmentItem NewCommonEquipmentItem(EquipmentSeedData eq) => new()
    {
        OfficialId = eq.Id,
        Category = Enum.Parse<EquipmentCategory>(eq.Category),
        Cost = eq.Cost,
        Rarity = eq.Rarity,
        CostRandomMax = eq.CostRandomMax,
        Source = ContentSource.Official,
        IsFreeDagger = eq.IsFreeDagger,
        Movement = eq.Movement,
        WeaponSkill = eq.WeaponSkill,
        BallisticSkill = eq.BallisticSkill,
        Strength = eq.Strength,
        Toughness = eq.Toughness,
        Wounds = eq.Wounds,
        Initiative = eq.Initiative,
        Attacks = eq.Attacks,
        Leadership = eq.Leadership,
        GrantsSkillCategory = eq.GrantsSkillCategory is { } grantsSkillCategory ? Enum.Parse<SkillCategory>(grantsSkillCategory) : null,
        GrantsSpecificSkillOfficialId = eq.GrantsSpecificSkillId,
        GrantsRareItemSearchBonus = eq.GrantsRareItemSearchBonus,
        IsSellable = eq.IsSellable,
        GrantsBonusExplorationDice = eq.GrantsBonusExplorationDice,
        IsUniqueArtefact = eq.IsUniqueArtefact,
        IsExplorationOnly = eq.IsExplorationOnly
    };

    /// <summary>Wipes and re-seeds the Exploration chart from Data/SeedData/ExplorationResults.json on
    /// EVERY launch, unconditionally - not gated behind InitializeAsync's "catalog empty" check like the
    /// rest of SeedOfficialContentAsync. Two problems this solves at once:
    /// (1) Found 2026-08-17: SeedExplorationResultsAsync got invoked a second time against a live dev
    ///     database outside the normal single-pass seed, silently doubling every row (no dedup guard of
    ///     its own, same "plain insert" precedent as Injury/EquipmentItem).
    /// (2) Found 2026-08-18, the more fundamental issue: an ALREADY-SEEDED database never re-seeds
    ///     anything (the empty-catalog gate only fires once, ever), so an edit to ExplorationResults.json
    ///     - like adding Puits/StatTestField - silently never reached a machine that had already run the
    ///     seed once before that edit existed. Every other Library catalog has a real CRUD editor and a
    ///     "no rules engine" boundary that makes this a non-issue; Exploration is pure reference content
    ///     with no editor and, critically, no other table holding a foreign key into
    ///     ExplorationResultEntity/ExplorationOutcomeEntity (History entries are plain strings, not
    ///     references) - nothing is lost by deleting and recreating it wholesale every launch. Cheap
    ///     (30 rows) compared to re-running the ~22-pass full catalog+warband seed this replaces having
    ///     to trigger manually.</summary>
    private async Task ResyncExplorationResultsAsync()
    {
        var staleResults = await _db.Table<ExplorationResultEntity>().ToListAsync();
        if (staleResults.Count > 0)
        {
            var staleOutcomes = await _db.Table<ExplorationOutcomeEntity>().ToListAsync();
            foreach (var outcome in staleOutcomes)
                await _db.DeleteAsync<ExplorationOutcomeEntity>(outcome.Id);

            var staleKeys = staleResults.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }).ToHashSet();
            var staleTranslations = (await _db.Table<TranslationEntity>().ToListAsync())
                .Where(t => staleKeys.Contains(t.Key));
            foreach (var translation in staleTranslations)
                await _db.DeleteAsync<TranslationEntity>(translation.Id);

            foreach (var result in staleResults)
                await _db.DeleteAsync<ExplorationResultEntity>(result.Id);
        }

        await SeedExplorationResultsAsync();
    }

    private async Task CreateAllTablesAsync()
    {
        await _db.CreateTableAsync<CampaignEntity>();
        await _db.CreateTableAsync<WarbandArchetypeEntity>();
        await _db.CreateTableAsync<WarbandEntity>();
        await _db.CreateTableAsync<WarriorArchetypeEntity>();
        await _db.CreateTableAsync<WarriorEntity>();
        await _db.CreateTableAsync<EquipmentItemEntity>();
        await _db.CreateTableAsync<SkillEntity>();
        await _db.CreateTableAsync<InjuryEntity>();
        await _db.CreateTableAsync<InjurySpecialRuleEntity>();
        await _db.CreateTableAsync<WarriorEquipmentEntity>();
        await _db.CreateTableAsync<WarbandEquipmentEntity>();
        await _db.CreateTableAsync<WarriorSkillEntity>();
        await _db.CreateTableAsync<WarriorInjuryEntity>();
        await _db.CreateTableAsync<WarriorHatredEntity>();
        await _db.CreateTableAsync<WarriorSpellEntity>();
        await _db.CreateTableAsync<HistoryEntryEntity>();
        await _db.CreateTableAsync<TranslationEntity>();
        await _db.CreateTableAsync<SpellEntity>();
        await _db.CreateTableAsync<WarbandArchetypeEquipmentEntity>();
        await _db.CreateTableAsync<WarbandArchetypeSkillEntity>();
        await _db.CreateTableAsync<WarriorArchetypeSkillEntity>();
        await _db.CreateTableAsync<SpecialRuleEntity>();
        await _db.CreateTableAsync<WarbandArchetypeSpecialRuleEntity>();
        await _db.CreateTableAsync<WarriorArchetypeSpecialRuleEntity>();
        await _db.CreateTableAsync<MutationEntity>();
        await _db.CreateTableAsync<WarriorMutationEntity>();
        await _db.CreateTableAsync<MagicSchoolEntity>();
        await _db.CreateTableAsync<WarbandArchetypeMagicSchoolEntity>();
        await _db.CreateTableAsync<RaceEntity>();
        await _db.CreateTableAsync<RacialProfileEntity>();
        await _db.CreateTableAsync<WarbandArchetypeMutationEntity>();
        await _db.CreateTableAsync<EquipmentListEntity>();
        await _db.CreateTableAsync<EquipmentListItemEntity>();
        await _db.CreateTableAsync<WarriorArchetypeEquipmentEntity>();
        await _db.CreateTableAsync<EquipmentItemSpecialRuleEntity>();
        await _db.CreateTableAsync<ExplorationResultEntity>();
        await _db.CreateTableAsync<ExplorationOutcomeEntity>();
        await _db.CreateTableAsync<HiredSwordEntity>();
        await _db.CreateTableAsync<HiredSwordEquipmentEntity>();
        await _db.CreateTableAsync<WarbandArchetypeHiredSwordEntity>();
        await _db.CreateTableAsync<HiredSwordSpecialRuleEntity>();
        await _db.CreateTableAsync<DramatisPersonaEntity>();
        await _db.CreateTableAsync<DramatisPersonaSpecialRuleEntity>();
        await _db.CreateTableAsync<WarbandArchetypeDramatisPersonaEntity>();
        await _db.CreateTableAsync<DramatisPersonaEquipmentEntity>();
        await _db.CreateTableAsync<DramatisPersonaSkillEntity>();
        await _db.CreateTableAsync<WarbandDramatisPersonaCooldownEntity>();
        await _db.CreateTableAsync<ContentMetaEntity>();
        await _db.CreateTableAsync<OfficialContentHashEntity>();
        await _db.CreateTableAsync<ContentConflictEntity>();
    }

    private async Task DropAllTablesAsync()
    {
        await _db.DropTableAsync<CampaignEntity>();
        await _db.DropTableAsync<WarbandArchetypeEntity>();
        await _db.DropTableAsync<WarbandEntity>();
        await _db.DropTableAsync<WarriorArchetypeEntity>();
        await _db.DropTableAsync<WarriorEntity>();
        await _db.DropTableAsync<EquipmentItemEntity>();
        await _db.DropTableAsync<SkillEntity>();
        await _db.DropTableAsync<InjuryEntity>();
        await _db.DropTableAsync<InjurySpecialRuleEntity>();
        await _db.DropTableAsync<WarriorEquipmentEntity>();
        await _db.DropTableAsync<WarriorSkillEntity>();
        await _db.DropTableAsync<WarriorInjuryEntity>();
        await _db.DropTableAsync<WarriorHatredEntity>();
        await _db.DropTableAsync<WarriorSpellEntity>();
        await _db.DropTableAsync<HistoryEntryEntity>();
        await _db.DropTableAsync<TranslationEntity>();
        await _db.DropTableAsync<SpellEntity>();
        await _db.DropTableAsync<WarbandArchetypeEquipmentEntity>();
        await _db.DropTableAsync<WarbandArchetypeSkillEntity>();
        await _db.DropTableAsync<WarriorArchetypeSkillEntity>();
        await _db.DropTableAsync<SpecialRuleEntity>();
        await _db.DropTableAsync<WarbandArchetypeSpecialRuleEntity>();
        await _db.DropTableAsync<WarriorArchetypeSpecialRuleEntity>();
        await _db.DropTableAsync<MutationEntity>();
        await _db.DropTableAsync<WarriorMutationEntity>();
        await _db.DropTableAsync<MagicSchoolEntity>();
        await _db.DropTableAsync<WarbandArchetypeMagicSchoolEntity>();
        await _db.DropTableAsync<WarbandArchetypeMutationEntity>();
        await _db.DropTableAsync<RacialProfileEntity>();
        await _db.DropTableAsync<EquipmentListEntity>();
        await _db.DropTableAsync<EquipmentListItemEntity>();
        await _db.DropTableAsync<WarriorArchetypeEquipmentEntity>();
        await _db.DropTableAsync<EquipmentItemSpecialRuleEntity>();
        await _db.DropTableAsync<ExplorationResultEntity>();
        await _db.DropTableAsync<ExplorationOutcomeEntity>();
        await _db.DropTableAsync<WarbandEquipmentEntity>();
        await _db.DropTableAsync<HiredSwordEntity>();
        await _db.DropTableAsync<HiredSwordEquipmentEntity>();
        await _db.DropTableAsync<WarbandArchetypeHiredSwordEntity>();
        await _db.DropTableAsync<HiredSwordSpecialRuleEntity>();
        await _db.DropTableAsync<DramatisPersonaEntity>();
        await _db.DropTableAsync<DramatisPersonaSpecialRuleEntity>();
        await _db.DropTableAsync<WarbandArchetypeDramatisPersonaEntity>();
        await _db.DropTableAsync<DramatisPersonaEquipmentEntity>();
        await _db.DropTableAsync<DramatisPersonaSkillEntity>();
        await _db.DropTableAsync<WarbandDramatisPersonaCooldownEntity>();
        await _db.DropTableAsync<ContentMetaEntity>();
        await _db.DropTableAsync<OfficialContentHashEntity>();
        await _db.DropTableAsync<ContentConflictEntity>();
    }

    /// <summary>Wipes every table (all campaign data AND Library edits/custom content) and recreates +
    /// reseeds from the bundled JSON - lets the Settings "Réinitialiser" button re-run the seed after a
    /// Core schema/data change without the user manually deleting the db file. Also clears the
    /// find-or-create caches (SpecialRule/Mutation/MagicSchool) since an id resolved during a previous
    /// seeding pass is meaningless against the fresh tables.</summary>
    public async Task ResetAsync()
    {
        await Initialization;
        await DropAllTablesAsync();
        InvalidateAllCachedTables();
        _specialRuleIdsByOfficialId.Clear();
        _mutationIdsByOfficialId.Clear();
        _magicSchoolIdsByOfficialId.Clear();
        _equipmentIdsByOfficialId.Clear();
        _skillIdsByOfficialId.Clear();
        _racialProfileIdsByOfficialId.Clear();
        _raceIdsByOfficialId.Clear();
        _warbandArchetypeIdsByOfficialId.Clear();
        _pendingSharedRestrictions.Clear();
        await CreateAllTablesAsync();
        await RunInTransactionAsync(async () =>
        {
            await SeedOfficialContentAsync();

            // Pas dans SeedOfficialContentAsync (voir ResyncExplorationResultsAsync, appelée à chaque
            // lancement plutôt que gardée derrière son garde-fou "catalogue vide") - après un DropTableAsync
            // complet ci-dessus, la table est garantie vide, un seed direct suffit ici (pas besoin du
            // nettoyage préalable que fait ResyncExplorationResultsAsync sur une base déjà peuplée).
            await SeedExplorationResultsAsync();
        });
    }

    private async Task SeedOfficialContentAsync()
    {
        // The 7 common catalogs (Data/SeedData/SpecialRules.json, Equipment.json, Mutations.json,
        // Skills.json, Injuries.json, MagicSchools.json, ExplorationResults.json) must seed before any
        // warband file below - warband JSON files only declare rules/equipment/mutations/schools that
        // are genuinely THEIRS, and find-or-create-by-English-Name (SpecialRule/Mutation/MagicSchool) or
        // a plain unrestricted insert (Equipment/Skill/Injury) relies on the canonical row already
        // existing by the time a warband references it. Injuries.json/ExplorationResults.json aren't
        // referenced by any warband file at all (no per-band injury/exploration tables in the rulebook),
        // they just need to seed once. Equipment.json's core rulebook mounts (Cheval/Destrier/Chien de
        // guerre, EquipmentCategory.Animal) carry RestrictedToWarbandNames instead of a single-band flag
        // - band-only mounts (e.g. Orc Mob's Sanglier de guerre) stay declared directly in their own
        // warband file, same split as any other band-declared equipment.
        await SeedSpecialRulesAsync();
        await SeedEquipmentAsync();
        await SeedMutationsAsync();
        await SeedSkillsAsync();
        await SeedInjuriesAsync();
        // Avant SeedHiredSwordsAsync : le Sorcier ("Warlock") référence "Lesser Magic" par un stub
        // name-only (HiredSwordSeedData.MagicSchoolName, même idiome que les stubs SpecialRules/Mutations
        // d'un fichier de bande) résolu via le même cache find-or-create que celui-ci alimente - le
        // find-or-create renvoie l'id existant SANS jamais mettre à jour la Description si le nom a déjà
        // été créé par un stub, donc l'ordre importe ici comme pour les 15 fichiers de bande (voir la
        // note juste au-dessus) : school MagicSchools.json doit être LE créateur (avec sa vraie
        // Description), pas le stub. Bug réel trouvé le 2026-08-27 (DataServiceTests.
        // MagicSchools_AllHaveBilingualDescriptions) : Lesser Magic seedait sans description tant que
        // HiredSwords seedait en premier.
        await SeedMagicSchoolsAsync();
        await SeedHiredSwordsAsync();
        await SeedRacesAsync();
        await SeedRacialProfilesAsync();
        // Pas ici : voir ResyncExplorationResultsAsync, appelée inconditionnellement depuis
        // InitializeAsync plutôt que gardée derrière le garde-fou "catalogue vide" de cette méthode.

        await SeedWarbandFromJsonAsync("Undead.json");
        await SeedWarbandFromJsonAsync("DwarfTreasureHunters.json");
        await SeedWarbandFromJsonAsync("Averlanders.json");
        await SeedWarbandFromJsonAsync("Ostlanders.json");
        await SeedWarbandFromJsonAsync("Reiklanders.json");
        await SeedWarbandFromJsonAsync("Middenheimers.json");
        await SeedWarbandFromJsonAsync("Marienburgers.json");
        await SeedWarbandFromJsonAsync("CarnivalOfChaos.json");
        await SeedWarbandFromJsonAsync("CultOfThePossessed.json");
        await SeedWarbandFromJsonAsync("OrcMob.json");
        await SeedWarbandFromJsonAsync("BeastmenRaiders.json");
        await SeedWarbandFromJsonAsync("WitchHunters.json");
        await SeedWarbandFromJsonAsync("SkavenOfClanEshin.json");
        await SeedWarbandFromJsonAsync("SistersOfSigmar.json");
        await SeedWarbandFromJsonAsync("Kislevites.json");

        // Après les 15 bandes (pas avant, contrairement à HiredSwords) : certains personnages référencent
        // par nom une Compétence propre à une bande (ex. Bertha/"Righteous Fury", propre aux Sœurs de
        // Sigmar - RestrictedToThisWarband dans SistersOfSigmar.json) via _skillIdsByOfficialId, qui
        // n'existe donc qu'une fois cette bande seedée. Conséquence positive : plus besoin de la passe de
        // résolution différée pour RestrictedToWarbandNames (voir SeedDramatisPersonaeAsync) - chaque
        // WarbandArchetypeId existe déjà, résolu directement via _warbandArchetypeIdsByOfficialId.
        await SeedDramatisPersonaeAsync();

        // Deferred resolution: common-catalog entries (Equipment/Skill/Mutation) that named several
        // bands via RestrictedToWarbandIds couldn't resolve a WarbandArchetypeId at seed time, since
        // none of the 15 warband files above had been seeded yet. Every band now exists, so resolve each
        // warband id against _warbandArchetypeIdsByOfficialId (throws on an unknown id - fail-fast,
        // surfaces a JSON typo at seed generation) and insert the matching join row.
        foreach (var pending in _pendingSharedRestrictions)
        {
            // SpecialRule's Hatred target isn't a join table (see SpecialRuleEntity.
            // HatredTargetWarbandArchetypeIds) - resolve every stem for this rule first, then write the
            // whole CSV list back in one update, rather than one join row per stem like the other 3 kinds.
            if (pending.Kind == SharedRestrictionKind.SpecialRule)
            {
                var targetIds = pending.WarbandOfficialIds.Select(id => _warbandArchetypeIdsByOfficialId[id]).ToList();
                var ruleEntity = await _db.Table<SpecialRuleEntity>().Where(r => r.Id == pending.ItemId).FirstAsync();
                ruleEntity.HatredTargetWarbandArchetypeIds = string.Join(',', targetIds);
                await _db.UpdateAsync(ruleEntity);
                continue;
            }

            // Skill's Hatred target mirrors SpecialRule's above - same CSV-write shape, same reason
            // (not a join table, see SkillEntity.HatredTargetWarbandArchetypeIds).
            if (pending.Kind == SharedRestrictionKind.SkillHatredTarget)
            {
                var targetIds = pending.WarbandOfficialIds.Select(id => _warbandArchetypeIdsByOfficialId[id]).ToList();
                var skillEntityForHatred = await _db.Table<SkillEntity>().Where(s => s.Id == pending.ItemId).FirstAsync();
                skillEntityForHatred.HatredTargetWarbandArchetypeIds = string.Join(',', targetIds);
                await _db.UpdateAsync(skillEntityForHatred);
                continue;
            }

            foreach (var warbandOfficialId in pending.WarbandOfficialIds)
            {
                var warbandArchetypeId = _warbandArchetypeIdsByOfficialId[warbandOfficialId];
                switch (pending.Kind)
                {
                    case SharedRestrictionKind.Equipment:
                        await _db.InsertAsync(new WarbandArchetypeEquipmentEntity { WarbandArchetypeId = warbandArchetypeId, EquipmentItemId = pending.ItemId });
                        break;
                    case SharedRestrictionKind.Skill:
                        await _db.InsertAsync(new WarbandArchetypeSkillEntity { WarbandArchetypeId = warbandArchetypeId, SkillId = pending.ItemId });
                        break;
                    case SharedRestrictionKind.Mutation:
                        await _db.InsertAsync(new WarbandArchetypeMutationEntity { WarbandArchetypeId = warbandArchetypeId, MutationId = pending.ItemId });
                        break;
                    case SharedRestrictionKind.HiredSword:
                        await _db.InsertAsync(new WarbandArchetypeHiredSwordEntity { WarbandArchetypeId = warbandArchetypeId, HiredSwordId = pending.ItemId });
                        break;
                }
            }
        }

        await SetContentMetaAsync(SeedContent.Version, SeedContent.Fingerprint);
        await StoreOfficialContentHashesAsync();
    }

    /// <summary>Version et empreinte du contenu officiel de cette base (voir SeedContent) - version 0 et
    /// empreinte nulle pour une base installée avant ContentMetaEntity.</summary>
    public async Task<(int Version, string? Fingerprint)> GetContentMetaAsync()
    {
        await Initialization;
        return await ReadContentMetaAsync();
    }

    private async Task<(int Version, string? Fingerprint)> ReadContentMetaAsync()
    {
        var meta = (await _db.Table<ContentMetaEntity>().ToListAsync()).ToDictionary(m => m.Key, m => m.Value);
        return (meta.TryGetValue(SeedContent.VersionKey, out var v) && int.TryParse(v, out var version) ? version : 0,
            meta.GetValueOrDefault(SeedContent.FingerprintKey));
    }

    private async Task SetContentMetaAsync(int version, string fingerprint)
    {
        await _db.InsertOrReplaceAsync(new ContentMetaEntity { Key = SeedContent.VersionKey, Value = version.ToString() });
        await _db.InsertOrReplaceAsync(new ContentMetaEntity { Key = SeedContent.FingerprintKey, Value = fingerprint });
    }

    /// <summary>Deserializes an embedded Data/SeedData/*.json file and inserts its warband, warrior
    /// archetypes, band-specific equipment (with restriction rows where flagged) and spells - each
    /// translatable field gets a fresh key via SeedTranslationAsync, same as the Reiklander seed above.</summary>
    /// <summary>Deserializes one warband seed file into its full WarbandSeedData - shared by
    /// SeedWarbandFromJsonAsync (first-launch seeding) and BackfillWarriorArchetypeRacialProfileAsync
    /// (which re-reads all 15 files to resolve WarriorSeedData.RacialProfileName by English archetype
    /// name, since that's per-band JSON data rather than a shared lookup table).</summary>
    private static async Task<WarbandSeedData> LoadWarbandSeedDataAsync(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return await JsonSerializer.DeserializeAsync<WarbandSeedData>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Empty or invalid seed file: {fileName}");
    }

    private async Task SeedWarbandFromJsonAsync(string fileName)
    {
        var data = await LoadWarbandSeedDataAsync(fileName);

        var warband = new WarbandArchetype
        {
            OfficialId = data.Id,
            Source = ContentSource.Official,
            Grade = Enum.Parse<WarbandGrade>(data.Grade),
            StartingTreasury = data.StartingTreasury,
            MaxWarriors = data.MaxWarriors,
            MinWarriors = data.MinWarriors,
            ImagePath = data.ImagePath ?? string.Empty,
            // Indexeur direct (pas GetValueOrDefault) : une bande sans race reconnue dans Races.json
            // (typo, ou Races.json pas encore seedé avant celle-ci) doit planter à la génération plutôt
            // que silencieusement RaceId=0.
            RaceId = _raceIdsByOfficialId[data.RaceId]
        };
        warband.NameKey = await SeedTranslationAsync(data.Name.En, data.Name.Fr);
        warband.DescriptionKey = data.Description is null ? null : await SeedTranslationAsync(data.Description.En, data.Description.Fr);
        var warbandEntity = warband.ToEntity();
        await _db.InsertAsync(warbandEntity);
        _warbandArchetypeIdsByOfficialId[data.Id] = warbandEntity.Id;

        foreach (var sr in data.SpecialRules)
        {
            var ruleId = await FindOrCreateSpecialRuleAsync(sr);
            await _db.InsertAsync(new WarbandArchetypeSpecialRuleEntity { WarbandArchetypeId = warbandEntity.Id, SpecialRuleId = ruleId });
        }

        // Doit précéder le traitement de data.Spells plus bas : chaque Spell référence son école par
        // id (SpellSeedData.MagicSchoolId), résolu contre ce cache.
        foreach (var ms in data.MagicSchools)
        {
            var schoolId = await FindOrCreateMagicSchoolAsync(ms);
            await _db.InsertAsync(new WarbandArchetypeMagicSchoolEntity { WarbandArchetypeId = warbandEntity.Id, MagicSchoolId = schoolId });
        }

        // Doit précéder EquipmentLists (qui référence ces items par id) et Warriors (dont
        // EquipmentListId dépend des listes ci-dessous) - donc seedé avant les guerriers cette fois,
        // contrairement à SpecialRules/Skills qui restent après. RestrictedToWarriorIds ne peut pas
        // encore être résolu ici (les ids de guerrier n'existent pas), voir pendingEquipmentWarriorRestrictions.
        var pendingEquipmentWarriorRestrictions = new List<(int ItemId, List<string> WarriorOfficialIds)>();

        foreach (var eq in data.Equipment)
        {
            // Find-or-create par id (comme SpecialRule/Mutation/MagicSchool) - un objet Rare partagé par
            // plusieurs bandes avec des restrictions différentes (ex. Holy Tome : Warrior-Priest chez les
            // Répurgateurs, Héroïnes chez les Sœurs de Sigmar) garde le même id dans chaque fichier, donc
            // une seule ligne de catalogue, chaque bande n'ajoutant que SES propres lignes de restriction.
            int itemId;
            if (_equipmentIdsByOfficialId.TryGetValue(eq.Id, out var existingItemId))
            {
                itemId = existingItemId;
            }
            else
            {
                var item = new EquipmentItem
                {
                    OfficialId = eq.Id,
                    Category = Enum.Parse<EquipmentCategory>(eq.Category),
                    Cost = eq.Cost,
                    Rarity = eq.Rarity,
                    CostRandomMax = eq.CostRandomMax,
                    Source = ContentSource.Official
                };
                item.NameKey = await SeedTranslationAsync(eq.Name.En, eq.Name.Fr);
                item.DescriptionKey = eq.Description is null ? null : await SeedTranslationAsync(eq.Description.En, eq.Description.Fr);
                var itemEntity = item.ToEntity();
                await _db.InsertAsync(itemEntity);
                itemId = itemEntity.Id;
                _equipmentIdsByOfficialId[eq.Id] = itemId;

                foreach (var sr in eq.SpecialRules)
                {
                    var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                    await _db.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = itemId, SpecialRuleId = ruleId });
                }
            }

            if (eq.RestrictedToThisWarband)
                await _db.InsertAsync(new WarbandArchetypeEquipmentEntity { WarbandArchetypeId = warbandEntity.Id, EquipmentItemId = itemId });

            if (eq.RestrictedToWarriorIds is { Count: > 0 } eqWarriorIds)
                pendingEquipmentWarriorRestrictions.Add((itemId, eqWarriorIds));
        }

        // Résout EquipmentListSeedData.ItemIds contre tout le catalogue déjà seedé (Equipment.json +
        // les objets propres à cette bande ci-dessus) - construit equipmentListIdsByOfficialId, consommé
        // juste en dessous par WarriorSeedData.EquipmentListId.
        var equipmentListIdsByOfficialId = new Dictionary<string, int>();
        foreach (var el in data.EquipmentLists)
        {
            var list = new EquipmentList { OfficialId = el.Id, WarbandArchetypeId = warbandEntity.Id, Source = ContentSource.Official };
            list.NameKey = await SeedTranslationAsync(el.Name.En, el.Name.Fr);
            var listEntity = list.ToEntity();
            await _db.InsertAsync(listEntity);
            equipmentListIdsByOfficialId[el.Id] = listEntity.Id;

            foreach (var itemOfficialId in el.ItemIds)
                await _db.InsertAsync(new EquipmentListItemEntity { EquipmentListId = listEntity.Id, EquipmentItemId = _equipmentIdsByOfficialId[itemOfficialId] });
        }

        // Id officiel -> id en base, alimenté ci-dessous pour résoudre les RestrictedToWarriorIds de cette
        // même bande (équipement et compétences).
        var warriorIdsByOfficialId = new Dictionary<string, int>();

        foreach (var w in data.Warriors)
        {
            var warrior = new WarriorArchetype
            {
                OfficialId = w.Id,
                WarbandArchetypeId = warbandEntity.Id,
                IsHero = w.IsHero,
                Cost = w.Cost,
                MaxCount = w.MaxCount,
                MinCount = w.MinCount,
                StartingExperience = w.StartingExperience,
                Movement = w.Movement,
                MovementOverride = w.MovementOverride,
                WeaponSkill = w.WeaponSkill,
                BallisticSkill = w.BallisticSkill,
                Strength = w.Strength,
                Toughness = w.Toughness,
                Wounds = w.Wounds,
                Initiative = w.Initiative,
                Attacks = w.Attacks,
                Leadership = w.Leadership,
                Source = ContentSource.Official,
                IsSpellcaster = w.IsSpellcaster,
                CanBuyMutations = w.CanBuyMutations,
                MustStartWithMutation = w.MustStartWithMutation,
                EquipmentListId = w.EquipmentListId is null ? null : equipmentListIdsByOfficialId[w.EquipmentListId],
                CanUseEquipment = w.CanUseEquipment,
                AllowedSkillCategories = w.SkillCategories.Select(Enum.Parse<SkillCategory>).ToList(),
                IsLargeCreature = w.IsLargeCreature,
                GainsExperience = w.GainsExperience,
                IsLeader = w.IsLeader,
                // 0 (jamais fail-fast, contrairement à WarbandArchetype.RaceId ci-dessus) si : (a)
                // w.RacialProfileId est null (archétype qui ne gagne jamais d'Expérience - Zombie/
                // Loup Funeste/Chien de guerre/Squig des Cavernes/Troll/Rats géants - l'étape
                // Progression ne se déclenche jamais pour lui, voir WarriorOutcomeRow.
                // ShowsInExperienceStep, donc ses maximums raciaux ne sont jamais consultés) ; ou (b) le
                // profil référencé n'existe pas (encore) dans RacialProfiles.json - référence en avance
                // volontaire (ex. "profile.rat-ogre"). 0 se comporte comme "aucun maximum connu, ne
                // bloque jamais" (voir Warrior.MaxWeaponSkill etc., nullable) plutôt que "plafonné à 0" -
                // ajouter le profil manquant plus tard suffit à l'activer, aucun changement de code requis.
                RacialProfileId = w.RacialProfileId is { } racialProfileOfficialId
                    && _racialProfileIdsByOfficialId.TryGetValue(racialProfileOfficialId, out var racialProfileId)
                        ? racialProfileId
                        : 0
            };
            warrior.NameKey = await SeedTranslationAsync(w.Name.En, w.Name.Fr);
            warrior.DescriptionKey = w.Description is null ? null : await SeedTranslationAsync(w.Description.En, w.Description.Fr);
            var warriorEntity = warrior.ToEntity();
            await _db.InsertAsync(warriorEntity);
            warriorIdsByOfficialId[w.Id] = warriorEntity.Id;

            foreach (var sr in w.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new WarriorArchetypeSpecialRuleEntity { WarriorArchetypeId = warriorEntity.Id, SpecialRuleId = ruleId });
            }
        }

        // Différé depuis la boucle Equipment ci-dessus - les ids de guerrier n'existaient pas encore.
        foreach (var (itemId, warriorOfficialIds) in pendingEquipmentWarriorRestrictions)
        {
            foreach (var warriorOfficialId in warriorOfficialIds)
                await _db.InsertAsync(new WarriorArchetypeEquipmentEntity { EquipmentItemId = itemId, WarriorArchetypeId = warriorIdsByOfficialId[warriorOfficialId] });
        }

        foreach (var sk in data.Skills)
        {
            var skill = new Skill
            {
                OfficialId = sk.Id,
                Category = Enum.Parse<SkillCategory>(sk.Category),
                Source = ContentSource.Official
            };
            skill.NameKey = await SeedTranslationAsync(sk.Name.En, sk.Name.Fr);
            skill.DescriptionKey = sk.Description is null ? null : await SeedTranslationAsync(sk.Description.En, sk.Description.Fr);
            var skillEntity = skill.ToEntity();
            await _db.InsertAsync(skillEntity);
            _skillIdsByOfficialId[sk.Id] = skillEntity.Id;

            if (sk.RestrictedToThisWarband)
                await _db.InsertAsync(new WarbandArchetypeSkillEntity { WarbandArchetypeId = warbandEntity.Id, SkillId = skillEntity.Id });

            if (sk.RestrictedToWarriorIds is { Count: > 0 } skWarriorIds)
            {
                foreach (var warriorOfficialId in skWarriorIds)
                    await _db.InsertAsync(new WarriorArchetypeSkillEntity { WarriorArchetypeId = warriorIdsByOfficialId[warriorOfficialId], SkillId = skillEntity.Id });
            }

            if (sk.HatredTargetWarbandIds is { Count: > 0 } hatredTargetIds)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.SkillHatredTarget, skillEntity.Id, hatredTargetIds));
        }

        foreach (var sp in data.Spells)
        {
            var spell = new Spell
            {
                OfficialId = sp.Id,
                MagicSchoolId = _magicSchoolIdsByOfficialId[sp.MagicSchoolId],
                RollValue = sp.RollValue,
                Difficulty = sp.Difficulty,
                Source = ContentSource.Official
            };
            spell.NameKey = await SeedTranslationAsync(sp.Name.En, sp.Name.Fr);
            spell.DescriptionKey = sp.Description is null ? null : await SeedTranslationAsync(sp.Description.En, sp.Description.Fr);
            await _db.InsertAsync(spell.ToEntity());
        }

        foreach (var mu in data.Mutations)
            await FindOrCreateMutationAsync(mu, warbandEntity.Id);
    }

    /// <summary>Deserializes an embedded Data/SeedData/*.json file that is a bare top-level array (the 5
    /// common catalogs, as opposed to the warband files' single top-level object) - same embedded-
    /// resource lookup as SeedWarbandFromJsonAsync.</summary>
    private static async Task<List<T>> LoadSeedArrayAsync<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Empty or invalid seed file: {fileName}");
    }

    private async Task SeedSpecialRulesAsync()
    {
        foreach (var sr in await LoadSeedArrayAsync<SpecialRuleSeedData>("SpecialRules.json"))
            await FindOrCreateSpecialRuleAsync(sr);
    }

    /// <summary>Plain insert, no dedup lookup needed - Equipment.json is hand-authored to be duplicate-
    /// free internally, and after this runs no warband file declares any of these names anymore.</summary>
    private async Task SeedEquipmentAsync()
    {
        foreach (var eq in await LoadSeedArrayAsync<EquipmentSeedData>("Equipment.json"))
        {
            var item = NewCommonEquipmentItem(eq);
            item.NameKey = await SeedTranslationAsync(eq.Name.En, eq.Name.Fr);
            item.DescriptionKey = eq.Description is null ? null : await SeedTranslationAsync(eq.Description.En, eq.Description.Fr);
            var itemEntity = item.ToEntity();
            await _db.InsertAsync(itemEntity);
            _equipmentIdsByOfficialId[eq.Id] = itemEntity.Id;

            foreach (var sr in eq.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = itemEntity.Id, SpecialRuleId = ruleId });
            }

            if (eq.RestrictedToWarbandIds is { Count: > 0 } eqWarbandIds)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.Equipment, itemEntity.Id, eqWarbandIds));
        }
    }

    private async Task SeedMutationsAsync()
    {
        foreach (var mu in await LoadSeedArrayAsync<MutationSeedData>("Mutations.json"))
            await FindOrCreateMutationAsync(mu, warbandArchetypeId: null);
    }

    /// <summary>Plain insert, no dedup - first (and, for the standard 5 skill lists, only) source of
    /// Skill data in the whole seed pipeline.</summary>
    private async Task SeedSkillsAsync()
    {
        foreach (var sk in await LoadSeedArrayAsync<SkillSeedData>("Skills.json"))
        {
            var skill = new Skill
            {
                OfficialId = sk.Id,
                Category = Enum.Parse<SkillCategory>(sk.Category),
                Source = ContentSource.Official
            };
            skill.NameKey = await SeedTranslationAsync(sk.Name.En, sk.Name.Fr);
            skill.DescriptionKey = sk.Description is null ? null : await SeedTranslationAsync(sk.Description.En, sk.Description.Fr);
            var skillEntity = skill.ToEntity();
            await _db.InsertAsync(skillEntity);
            _skillIdsByOfficialId[sk.Id] = skillEntity.Id;

            if (sk.RestrictedToWarbandIds is { Count: > 0 } skWarbandIds)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.Skill, skillEntity.Id, skWarbandIds));

            if (sk.HatredTargetWarbandIds is { Count: > 0 } skHatredTargetIds)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.SkillHatredTarget, skillEntity.Id, skHatredTargetIds));
        }
    }

    /// <summary>Plain insert, no dedup - the only source of HiredSword data in the seed pipeline. Runs
    /// after SeedEquipmentAsync (needs _equipmentIdsByOfficialId populated to resolve
    /// StartingEquipmentIds) and before any SeedWarbandFromJsonAsync call (its RestrictedToWarbandIds
    /// needs the same deferred-resolution pass as Equipment/Skill/Mutation, see SeedOfficialContentAsync).</summary>
    private async Task SeedHiredSwordsAsync()
    {
        foreach (var hs in await LoadSeedArrayAsync<HiredSwordSeedData>("HiredSwords.json"))
        {
            var hiredSword = new HiredSword
            {
                OfficialId = hs.Id,
                HireCost = hs.HireCost,
                Upkeep = hs.Upkeep,
                BaseRating = hs.BaseRating,
                Movement = hs.Movement,
                WeaponSkill = hs.WeaponSkill,
                BallisticSkill = hs.BallisticSkill,
                Strength = hs.Strength,
                Toughness = hs.Toughness,
                Wounds = hs.Wounds,
                Initiative = hs.Initiative,
                Attacks = hs.Attacks,
                Leadership = hs.Leadership,
                AllowedSkillCategories = hs.AllowedSkillCategories.Select(Enum.Parse<SkillCategory>).ToList(),
                Source = ContentSource.Official
            };
            // MagicSchools.json est seedé juste avant (voir SeedOfficialContentAsync) - une école inconnue
            // est une faute de frappe dans le JSON, fail-fast.
            if (hs.MagicSchoolId is { } magicSchoolOfficialId)
                hiredSword.MagicSchoolId = _magicSchoolIdsByOfficialId[magicSchoolOfficialId];
            hiredSword.NameKey = await SeedTranslationAsync(hs.Name.En, hs.Name.Fr);
            hiredSword.DescriptionKey = hs.Description is null ? null : await SeedTranslationAsync(hs.Description.En, hs.Description.Fr);
            var entity = hiredSword.ToEntity();
            await _db.InsertAsync(entity);

            foreach (var itemOfficialId in hs.StartingEquipmentIds)
            {
                if (!_equipmentIdsByOfficialId.TryGetValue(itemOfficialId, out var itemId))
                    throw new InvalidOperationException($"HiredSwords.json references unknown equipment '{itemOfficialId}'");
                await _db.InsertAsync(new HiredSwordEquipmentEntity { HiredSwordId = entity.Id, EquipmentItemId = itemId });
            }

            foreach (var sr in hs.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new HiredSwordSpecialRuleEntity { HiredSwordId = entity.Id, SpecialRuleId = ruleId });
            }

            if (hs.RestrictedToWarbandIds is { Count: > 0 } hsWarbandIds)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.HiredSword, entity.Id, hsWarbandIds));
        }
    }

    /// <summary>Plain insert, no dedup - the only source of DramatisPersona data in the seed pipeline.
    /// Runs AFTER all 15 SeedWarbandFromJsonAsync calls (unlike SeedHiredSwordsAsync, which runs before
    /// them) - some characters reference a band-exclusive Skill (e.g. Bertha/"Righteous Fury",
    /// RestrictedToThisWarband in SistersOfSigmar.json), which only exists in _skillIdsByOfficialId once
    /// that band has seeded. Side benefit: every WarbandArchetypeId already exists by this point, so
    /// RestrictedToWarbandIds resolves directly via _warbandArchetypeIdsByOfficialId - no deferred-
    /// resolution pass needed here, unlike Equipment/Skill/Mutation/HiredSword (see
    /// SeedOfficialContentAsync).</summary>
    private async Task SeedDramatisPersonaeAsync()
    {
        // Résolution différée du pairage Ulli/Marquand (2026-09-01, "vous devez les recruter tous les
        // deux") - Marquand référence Ulli AVANT qu'elle n'existe (elle vient après lui dans le fichier).
        // Peuplé pendant la boucle, résolu juste après (les deux entrées existent alors forcément).
        var personaIdsByOfficialId = new Dictionary<string, int>();
        var pendingPairings = new List<(int EntityId, string PairedWithOfficialId)>();

        foreach (var dp in await LoadSeedArrayAsync<DramatisPersonaSeedData>("DramatisPersonae.json"))
        {
            var persona = new DramatisPersona
            {
                OfficialId = dp.Id,
                Movement = dp.Movement,
                WeaponSkill = dp.WeaponSkill,
                BallisticSkill = dp.BallisticSkill,
                Strength = dp.Strength,
                Toughness = dp.Toughness,
                Wounds = dp.Wounds,
                Initiative = dp.Initiative,
                Attacks = dp.Attacks,
                Leadership = dp.Leadership,
                FeeKind = Enum.Parse<DramatisPersonaHireFeeKind>(dp.FeeKind),
                HireCost = dp.HireCost,
                Upkeep = dp.Upkeep,
                RatingBonus = dp.RatingBonus,
                IsWanderer = dp.IsWanderer,
                RequiresRatingDisadvantage = dp.RequiresRatingDisadvantage,
                RequiresCooldownBeforeResearch = dp.RequiresCooldownBeforeResearch,
                IsHiddenFromSearchPicker = dp.HiddenFromSearchPicker,
                Source = ContentSource.Official
            };
            if (dp.MagicSchoolId is { } magicSchoolOfficialId)
                persona.MagicSchoolId = _magicSchoolIdsByOfficialId[magicSchoolOfficialId];
            if (dp.AlternativePaymentItemId is { } alternativePaymentItemOfficialId)
            {
                if (!_equipmentIdsByOfficialId.TryGetValue(alternativePaymentItemOfficialId, out var alternativePaymentItemId))
                    throw new InvalidOperationException($"DramatisPersonae.json references unknown equipment '{alternativePaymentItemOfficialId}'");
                persona.AlternativePaymentItemId = alternativePaymentItemId;
            }
            persona.NameKey = await SeedTranslationAsync(dp.Name.En, dp.Name.Fr);
            persona.DescriptionKey = dp.Description is null ? null : await SeedTranslationAsync(dp.Description.En, dp.Description.Fr);
            persona.PairDescriptionKey = dp.PairDescription is null ? null : await SeedTranslationAsync(dp.PairDescription.En, dp.PairDescription.Fr);
            var entity = persona.ToEntity();
            await _db.InsertAsync(entity);
            personaIdsByOfficialId[dp.Id] = entity.Id;
            if (dp.PairedWithPersonaId is { } pairedWithOfficialId)
                pendingPairings.Add((entity.Id, pairedWithOfficialId));

            foreach (var sr in dp.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new DramatisPersonaSpecialRuleEntity { DramatisPersonaId = entity.Id, SpecialRuleId = ruleId });
            }

            foreach (var itemOfficialId in dp.StartingEquipmentIds)
            {
                if (!_equipmentIdsByOfficialId.TryGetValue(itemOfficialId, out var itemId))
                    throw new InvalidOperationException($"DramatisPersonae.json references unknown equipment '{itemOfficialId}'");
                await _db.InsertAsync(new DramatisPersonaEquipmentEntity { DramatisPersonaId = entity.Id, EquipmentItemId = itemId });
            }

            foreach (var skillOfficialId in dp.SkillIds)
            {
                if (!_skillIdsByOfficialId.TryGetValue(skillOfficialId, out var skillId))
                    throw new InvalidOperationException($"DramatisPersonae.json references unknown skill '{skillOfficialId}'");
                await _db.InsertAsync(new DramatisPersonaSkillEntity { DramatisPersonaId = entity.Id, SkillId = skillId });
            }

            if (dp.RestrictedToWarbandIds is { Count: > 0 } dpWarbandIds)
            {
                foreach (var warbandOfficialId in dpWarbandIds)
                    await _db.InsertAsync(new WarbandArchetypeDramatisPersonaEntity { DramatisPersonaId = entity.Id, WarbandArchetypeId = _warbandArchetypeIdsByOfficialId[warbandOfficialId] });
            }
        }

        foreach (var (entityId, pairedWithOfficialId) in pendingPairings)
        {
            if (!personaIdsByOfficialId.TryGetValue(pairedWithOfficialId, out var pairedWithId))
                throw new InvalidOperationException($"DramatisPersonae.json references unknown persona '{pairedWithOfficialId}' for pairedWithPersonaId");
            var entity = await _db.Table<DramatisPersonaEntity>().Where(e => e.Id == entityId).FirstAsync();
            entity.PairedWithDramatisPersonaId = pairedWithId;
            await _db.UpdateAsync(entity);
        }
    }

    /// <summary>Plain insert, no dedup - the rulebook's Serious Injuries charts (Heroes' D66 + Henchmen's
    /// D6), common to every warband. Purely a browsable/editable reference catalog - see Injury's doc
    /// comment for why this is deliberately not wired into SeriousInjuryTable/HenchmanInjuryTable.</summary>
    private async Task SeedInjuriesAsync()
    {
        foreach (var inj in await LoadSeedArrayAsync<InjurySeedData>("Injuries.json"))
        {
            var injury = new Injury
            {
                OfficialId = inj.Id,
                Category = Enum.Parse<InjuryCategory>(inj.Category),
                RollRange = inj.RollRange,
                BranchRange = inj.BranchRange,
                Source = ContentSource.Official
            };
            injury.NameKey = await SeedTranslationAsync(inj.Name.En, inj.Name.Fr);
            injury.DescriptionKey = inj.Description is null ? null : await SeedTranslationAsync(inj.Description.En, inj.Description.Fr);
            var entity = injury.ToEntity();
            await _db.InsertAsync(entity);

            foreach (var sr in inj.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new InjurySpecialRuleEntity { InjuryId = entity.Id, SpecialRuleId = ruleId });
            }
        }
    }

    /// <summary>Plain insert, no dedup - the rulebook's Exploration chart (doubles through
    /// six-of-a-kind), common to every warband. EquipmentOutcome.EquipmentItemOfficialId is stored as-is (a
    /// plain name, not an id): it's resolved by lookup against the Trading Post catalog by the End of
    /// Game wizard at roll time, not at seed time - see Models.Library.ExplorationOutcome.</summary>
    private async Task SeedExplorationResultsAsync()
    {
        foreach (var res in await LoadSeedArrayAsync<ExplorationResultSeedData>("ExplorationResults.json"))
        {
            var result = new ExplorationResult
            {
                OfficialId = res.Id,
                DiceCount = res.DiceCount,
                Value = res.Value,
                RollsIndependently = res.RollsIndependently,
                StatTestField = res.StatTestField is { } field ? Enum.Parse<ExplorationStatField>(field) : null,
                StatTestTargetsLeader = res.StatTestTargetsLeader,
                AutoPassStatTestWarbandArchetypeOfficialIds = res.AutoPassStatTestWarbandArchetypeIds ?? new(),
                RequiresDoubleRoll = res.RequiresDoubleRoll,
                BonusStatTestField = res.BonusStatTestField is { } bonusField ? Enum.Parse<ExplorationStatField>(bonusField) : null,
                RequiresSentHero = res.RequiresSentHero,
                Source = ContentSource.Official
            };
            result.NameKey = await SeedTranslationAsync(res.Name.En, res.Name.Fr);
            result.DescriptionKey = await SeedTranslationAsync(res.Description.En, res.Description.Fr);
            if (res.ShortDescription is { } shortDescription)
                result.ShortDescriptionKey = await SeedTranslationAsync(shortDescription.En, shortDescription.Fr);
            var resultEntity = result.ToEntity();
            await _db.InsertAsync(resultEntity);

            foreach (var outcome in res.Outcomes)
            {
                var branchTextKey = outcome.BranchText is { } branchText
                    ? await SeedTranslationAsync(branchText.En, branchText.Fr) : null;
                var nextGameNoteTextKey = outcome.NextGameNoteText is { } nextGameNoteText
                    ? await SeedTranslationAsync(nextGameNoteText.En, nextGameNoteText.Fr) : null;
                await _db.InsertAsync(new ExplorationOutcomeEntity
                {
                    ExplorationResultId = resultEntity.Id,
                    SubRollMin = outcome.SubRollMin,
                    SubRollMax = outcome.SubRollMax,
                    Kind = Enum.Parse<ExplorationOutcomeKind>(outcome.Kind),
                    GoldFormula = outcome.GoldFormula,
                    EquipmentItemOfficialId = outcome.EquipmentItemId,
                    ItemQuantityFormula = outcome.ItemQuantityFormula,
                    FoundValueFormula = outcome.FoundValueFormula,
                    MaterialRuleOfficialId = outcome.MaterialRuleId,
                    SecondaryEquipmentItemOfficialId = outcome.SecondaryEquipmentItemId,
                    AlternativeEquipmentItemOfficialId = outcome.AlternativeEquipmentItemId,
                    Note = outcome.Note,
                    BranchTextKey = branchTextKey,
                    StatTestPass = outcome.StatTestPass,
                    CausesSickness = outcome.CausesSickness,
                    RequiresDoubleRoll = outcome.RequiresDoubleRoll,
                    CausesDeath = outcome.CausesDeath,
                    TriggersArtefactRoll = outcome.TriggersArtefactRoll,
                    RestrictedToWarbandArchetypeOfficialIdsCsv = outcome.RestrictedToWarbandArchetypeIds is { Count: > 0 } warbandIds
                        ? string.Join(",", warbandIds) : null,
                    GrantsNextExplorationBonusDie = outcome.GrantsNextExplorationBonusDie,
                    GrantsLeaderExperience = outcome.GrantsLeaderExperience,
                    GrantsDistributedHeroExperienceFormula = outcome.GrantsDistributedHeroExperienceFormula,
                    GrantsFreeHenchmanArchetypeOfficialId = outcome.GrantsFreeHenchmanArchetypeId,
                    GrantsOptionalEquippedHenchman = outcome.GrantsOptionalEquippedHenchman,
                    NextGameNoteTextKey = nextGameNoteTextKey,
                    GrantsWeaponBlessing = outcome.GrantsWeaponBlessing,
                    GrantsCatacombReroll = outcome.GrantsCatacombReroll,
                    GrantsFreeHiredSword = outcome.GrantsFreeHiredSword
                });
            }
        }
    }

    private async Task SeedMagicSchoolsAsync()
    {
        foreach (var school in await LoadSeedArrayAsync<MagicSchoolWithSpellsSeedData>("MagicSchools.json"))
        {
            var schoolId = await FindOrCreateMagicSchoolAsync(new MagicSchoolSeedData { Id = school.Id, Name = school.Name, Description = school.Description });

            foreach (var sp in school.Spells)
            {
                var spell = new Spell
                {
                    OfficialId = sp.Id,
                    MagicSchoolId = schoolId,
                    RollValue = sp.RollValue,
                    Difficulty = sp.Difficulty,
                    Source = ContentSource.Official
                };
                spell.NameKey = await SeedTranslationAsync(sp.Name.En, sp.Name.Fr);
                spell.DescriptionKey = sp.Description is null ? null : await SeedTranslationAsync(sp.Description.En, sp.Description.Fr);
                await _db.InsertAsync(spell.ToEntity());
            }
        }
    }

    /// <summary>Allocates a fresh translation key and writes the English (seed data's authoring
    /// language) and, when supplied, French values for it directly - bypasses LibraryService (which
    /// also does the Official-&gt;Modified flip check, irrelevant for a brand new insert).</summary>
    private async Task<string> SeedTranslationAsync(string en, string? fr)
    {
        var key = Guid.NewGuid().ToString("N");
        await _db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = "en", Value = en });
        if (!string.IsNullOrEmpty(fr))
            await _db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = "fr", Value = fr });
        return key;
    }

    /// <summary>English Name -> already-created SpecialRuleEntity id, for this seeding pass only (the
    /// whole SeedOfficialContentAsync run happens once, gated by "catalog empty" - no need to also check
    /// the DB for pre-existing rows). Lets e.g. "Leader" attached from 4 different warbands' JSON files
    /// resolve to the SAME catalog row instead of 4 duplicates - a rule meant to be shared keeps the same
    /// id ("rule.leader") in every file.</summary>
    private readonly Dictionary<string, int> _specialRuleIdsByOfficialId = new();

    private async Task<int> FindOrCreateSpecialRuleAsync(SpecialRuleSeedData seed)
    {
        if (_specialRuleIdsByOfficialId.TryGetValue(seed.Id, out var existingId))
            return existingId;

        var rule = new SpecialRule { OfficialId = seed.Id, Source = ContentSource.Official, CostMultiplier = seed.CostMultiplier, Abbreviation = seed.Abbreviation, Rarity = seed.Rarity, IsResaleUpgrade = seed.IsResaleUpgrade, HatredTargetsSpellcasters = seed.HatredTargetsSpellcasters };
        rule.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        rule.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = rule.ToEntity();
        await _db.InsertAsync(entity);

        // Target WarbandArchetypes may not be seeded yet (this rule can attach from a band-level array
        // that seeds before the target band's own file) - resolved in the same deferred pass as
        // Equipment/Skill/Mutation's RestrictedToWarbandIds, see SeedOfficialContentAsync.
        if (seed.HatredTargetWarbandIds is { Count: > 0 } hatredTargets)
            _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.SpecialRule, entity.Id, hatredTargets));

        _specialRuleIdsByOfficialId[seed.Id] = entity.Id;
        return entity.Id;
    }

    /// <summary>Official id -> already-created MutationEntity id, same rationale/scope as
    /// _specialRuleIdsByOfficialId - lets the identical rulebook Mutations list (p.76), reused verbatim
    /// across every Chaos-adjacent warband's JSON, resolve to one shared catalog row.</summary>
    private readonly Dictionary<string, int> _mutationIdsByOfficialId = new();

    private async Task<int> FindOrCreateMutationAsync(MutationSeedData seed, int? warbandArchetypeId)
    {
        if (_mutationIdsByOfficialId.TryGetValue(seed.Id, out var existingId))
            return existingId;

        var mutation = new Mutation { OfficialId = seed.Id, Source = ContentSource.Official, Cost = seed.Cost };
        mutation.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        mutation.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = mutation.ToEntity();
        await _db.InsertAsync(entity);

        if (seed.RestrictedToThisWarband && warbandArchetypeId is not null)
            await _db.InsertAsync(new WarbandArchetypeMutationEntity { WarbandArchetypeId = warbandArchetypeId.Value, MutationId = entity.Id });

        if (seed.RestrictedToWarbandIds is { Count: > 0 } muWarbandIds)
            _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.Mutation, entity.Id, muWarbandIds));

        _mutationIdsByOfficialId[seed.Id] = entity.Id;
        return entity.Id;
    }

    /// <summary>Official id -> already-created MagicSchoolEntity id, same rationale/scope as
    /// _specialRuleIdsByOfficialId - a school like "Necromancy" is declared once per warband's
    /// WarbandSeedData.MagicSchools and then referenced by its Spell entries via
    /// SpellSeedData.MagicSchoolId.</summary>
    private readonly Dictionary<string, int> _magicSchoolIdsByOfficialId = new();

    private async Task<int> FindOrCreateMagicSchoolAsync(MagicSchoolSeedData seed)
    {
        if (_magicSchoolIdsByOfficialId.TryGetValue(seed.Id, out var existingId))
            return existingId;

        var school = new MagicSchool { OfficialId = seed.Id, Source = ContentSource.Official };
        school.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        school.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = school.ToEntity();
        await _db.InsertAsync(entity);

        _magicSchoolIdsByOfficialId[seed.Id] = entity.Id;
        return entity.Id;
    }

    /// <summary>Id officiel -> RaceEntity id, pour le seed des bandes (WarbandSeedData.RaceId).</summary>
    private readonly Dictionary<string, int> _raceIdsByOfficialId = new();

    private async Task SeedRacesAsync()
    {
        foreach (var seed in await LoadSeedArrayAsync<RaceSeedData>("Races.json"))
        {
            var race = new Race { OfficialId = seed.Id, Source = ContentSource.Official };
            race.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
            race.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
            var entity = race.ToEntity();
            await _db.InsertAsync(entity);
            _raceIdsByOfficialId[seed.Id] = entity.Id;
        }
    }

    /// <summary>Id officiel -> RacialProfileEntity id, pour le seed des guerriers (WarriorSeedData.RacialProfileId).</summary>
    private readonly Dictionary<string, int> _racialProfileIdsByOfficialId = new();

    private async Task SeedRacialProfilesAsync()
    {
        foreach (var seed in await LoadSeedArrayAsync<RacialProfileSeedData>("RacialProfiles.json"))
        {
            var profile = new RacialProfile
            {
                OfficialId = seed.Id,
                Source = ContentSource.Official,
                Movement = seed.Movement,
                MovementOverride = seed.MovementOverride,
                WeaponSkill = seed.WeaponSkill,
                BallisticSkill = seed.BallisticSkill,
                Strength = seed.Strength,
                Toughness = seed.Toughness,
                Wounds = seed.Wounds,
                Initiative = seed.Initiative,
                Attacks = seed.Attacks,
                Leadership = seed.Leadership
            };
            profile.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
            profile.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
            var entity = profile.ToEntity();
            await _db.InsertAsync(entity);
            _racialProfileIdsByOfficialId[seed.Id] = entity.Id;
        }
    }

    /// <summary>English Name -> already-created EquipmentItemEntity id - populated by both the common
    /// pool (SeedEquipmentAsync, plain insert, no dedup needed since Equipment.json is hand-authored
    /// duplicate-free) and by SeedWarbandFromJsonAsync's own find-or-create Equipment loop (needed for
    /// Rare items shared by exactly a couple of bands with different restrictions, e.g. Holy Tome -
    /// Warrior-Priest for Witch Hunters, Heroines for Sisters of Sigmar - one catalog row, two sets of
    /// restriction rows). Also consumed when resolving EquipmentListSeedData.ItemNames.</summary>
    private readonly Dictionary<string, int> _equipmentIdsByOfficialId = new();

    /// <summary>English Name -> SkillEntity id, populated by SeedSkillsAsync - same purpose as
    /// _equipmentIdsByOfficialId, needed to resolve DramatisPersonaSeedData.SkillNames (a Dramatis
    /// Persona's fixed known-skills list, not a WarriorArchetype pick-from-category Advance table).</summary>
    private readonly Dictionary<string, int> _skillIdsByOfficialId = new();

    /// <summary>Warband JSON file stem (e.g. "Reiklanders", from SeedWarbandFromJsonAsync's fileName
    /// without extension) -> WarbandArchetypeEntity id, populated as each of the 15 warband files seeds.
    /// Lets a common-catalog entry (Equipment/Skill/Mutation) declared BEFORE any warband exists still
    /// name several bands via RestrictedToWarbandNames - see _pendingSharedRestrictions.</summary>
    private readonly Dictionary<string, int> _warbandArchetypeIdsByOfficialId = new();

    private enum SharedRestrictionKind { Equipment, Skill, Mutation, SpecialRule, HiredSword, SkillHatredTarget }

    private record struct PendingSharedRestriction(SharedRestrictionKind Kind, int ItemId, List<string> WarbandOfficialIds);

    /// <summary>Common-catalog restrictions naming several bands (RestrictedToWarbandNames) can't resolve
    /// a WarbandArchetypeId at the point they're seeded (SeedEquipmentAsync/SeedSkillsAsync/
    /// FindOrCreateMutationAsync all run before any warband file) - collected here and resolved in one
    /// pass at the end of SeedOfficialContentAsync, once every band exists.</summary>
    private readonly List<PendingSharedRestriction> _pendingSharedRestrictions = new();
}
